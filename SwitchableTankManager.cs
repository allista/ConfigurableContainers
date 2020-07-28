//   SwitchableTankManager.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections.Generic;
using System.Linq;
using CC.UI;
using UnityEngine;

namespace AT_Utils
{
    public interface ITankManagerHost
    {
        SwitchableTankManager GetTankManager();
    }

    public class SwitchableTankManager : ConfigNodeObject, ITankManager
    {
        public new const string NODE_NAME = "TANKMANAGER";
        public const string MANAGED = "MANAGED";
        private static readonly DefaultCapabilities defaultCapabilities = new DefaultCapabilities();

        private readonly PartModule host;
        public Part part => host.part;
        public ITankManagerCapabilities Capabilities => host as ITankManagerCapabilities ?? defaultCapabilities;

        private readonly List<ModuleSwitchableTank> tanks = new List<ModuleSwitchableTank>();
        public IReadOnlyCollection<ModuleSwitchableTank> Tanks => tanks;
        IReadOnlyCollection<ITankInfo> ITankManager.Tanks => tanks;

        public readonly SwitchableTankManagerUI UI;
        [Persistent] public Vector3 uiPos = Vector3.zero;

        public delegate void TankAction(ModuleSwitchableTank tank);

        public delegate void TankFailedAction(string tankType, float volume);

        public delegate string TankValidator(string tankType, float volume);

        public TankValidator onNewTankVolumeChanged = delegate { return null; };
        string ITankManager.OnVolumeChanged(string tankType, float volume) => onNewTankVolumeChanged(tankType, volume);
        public TankValidator onValidateNewTank = delegate { return null; };
        public TankAction onTankAdded = delegate { };
        public TankAction onTankRemoved = delegate { };
        public TankFailedAction onTankFailedToAdd = delegate { };

        private bool enable_part_controls;

        private string[] exclude;

        /// <summary>
        ///     Excluded tank types. If empty, all types are supported.
        /// </summary>
        [Persistent]
        public string ExcludeTankTypes = string.Empty;

        private string[] include;

        /// <summary>
        ///     Supported tank types. If empty, all types are supported. Overrides ExcludedTankTypes.
        /// </summary>
        [Persistent]
        public string IncludeTankTypes = string.Empty;

        private int max_id = -1;

        public List<string> SupportedTypes = new List<string>();
        IList<string> ITankManager.SupportedTypes => SupportedTypes;
        IList<string> ITankManager.SupportedTankConfigs => VolumeConfigsLibrary.UserConfigs.Keys;

        /// <summary>
        ///     Maximum total volume of all tanks in m^3. It is used for reference and in tank controls.
        /// </summary>
        public float Volume
        {
            get => volume;
            set
            {
                volume = value;
                InvalidateCaches();
            }
        }

        private float volume;

        private float tanks_volume = -1;
        private float availableVolume = -1;
        private float availableVolumePercent = -1;

        public float AvailableVolume
        {
            get
            {
                if(availableVolume < 0)
                    availableVolume = Volume - TanksVolume;
                return availableVolume;
            }
        }

        public float AvailableVolumePercent
        {
            get
            {
                if(availableVolumePercent < 0)
                    availableVolumePercent = AvailableVolume / Volume;
                return availableVolume;
            }
        }

        public bool EnablePartControls
        {
            get => enable_part_controls;
            set
            {
                if(value == enable_part_controls)
                    return;
                enable_part_controls = value;
                tanks.ForEach(t => t.EnablePartControls = enable_part_controls);
            }
        }

        public int TanksCount => tanks.Count;
        public float TotalCost => tanks.Aggregate(0f, (c, t) => c + t.Cost);

        public string Title => part.Title();

        public float TanksVolume
        {
            get
            {
                if(tanks_volume < 0)
                    tanks_volume = tanks.Aggregate(0f, (v, t) => v + t.Volume);
                return tanks_volume;
            }
        }

        public SwitchableTankManager(PartModule host)
        {
            this.host = host;
            UI = new SwitchableTankManagerUI(this);
        }

        ~SwitchableTankManager()
        {
            UI?.Close();
        }

        public ModuleSwitchableTank GetTank(int id)
        {
            return tanks.Find(t => t.id == id);
        }

        string ITankManager.GetTypeInfo(string tankType)
        {
            var info = SwitchableTankType.GetTankTypeInfo(tankType);
            return string.IsNullOrEmpty(info)
                ? VolumeConfigsLibrary.GetConfigInfo(tankType)
                : info;
        }

        public void InvalidateCaches()
        {
            tanks_volume = -1;
            availableVolume = -1;
            availableVolumePercent = -1;
        }

        public void ClampNewVolume(float oldVolume, ref float newVolume)
        {
            if(oldVolume > newVolume)
                return;
            if(newVolume - oldVolume > AvailableVolume)
                newVolume = oldVolume + AvailableVolume;
        }

        public static string GetInfo(PartModule host, ConfigNode node)
        {
            var mgr = new SwitchableTankManager(host);
            return mgr.GetInfo(node);
        }

        public string GetInfo(ConfigNode node)
        {
            base.Load(node);
            init_supported_types();
            var info = StringBuilderCache.Acquire();
            info.AppendLine(SwitchableTankType.TypesInfo(include, exclude));
            var volumes = FromConfig<VolumeConfiguration>(node);
            // ReSharper disable once InvertIf
            if(volumes.Valid)
            {
                info.AppendLine("\n<b>Preconfigured Tanks:</b>\n");
                info.AppendLine(volumes.Info());
            }
            return info.ToStringAndRelease().Trim();
        }

        private void init_supported_types()
        {
            exclude = Utils.ParseLine(ExcludeTankTypes, Utils.Comma);
            include = Utils.ParseLine(IncludeTankTypes, Utils.Comma);
            SupportedTypes = SwitchableTankType.TankTypeNames(include, exclude);
            SupportedTypes.AddRange(VolumeConfigsLibrary.AllConfigNames(include, exclude));
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            if(tanks.Count == 0)
                return;
            tanks.ForEach(t => t.Save(node.AddNode(TankVolume.NODE_NAME)));
            node.AddValue(MANAGED, true);
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            tanks.Clear();
            InvalidateCaches();
            init_supported_types();
            if(node.HasValue(MANAGED))
            {
                var existing_tanks = part.Modules.GetModules<ModuleSwitchableTank>();
                foreach(var n in node.GetNodes(TankVolume.NODE_NAME))
                {
                    n.AddValue(MANAGED, true);
                    n.AddValue("name", nameof(ModuleSwitchableTank));
                    n.SetValue("ExcludeTankTypes", ExcludeTankTypes);
                    n.SetValue("IncludeTankTypes", IncludeTankTypes);
                    ModuleSwitchableTank tank;
                    var id = n.HasValue("id") ? int.Parse(n.GetValue("id")) : -1;
                    if(id >= 0 && (tank = existing_tanks.Find(t => t.id == id)) != null)
                    {
                        tank.Load(n);
                        max_id = Mathf.Max(max_id, id);
                    }
                    else
                    {
                        tank = part.AddModule(n) as ModuleSwitchableTank;
                        if(tank != null)
                            tank.id = ++max_id;
                    }
                    if(tank != null)
                    {
                        tank.EnablePartControls = EnablePartControls;
                        tank.manager = this;
                        tanks.Add(tank);
                    }
                    else
                    {
                        Utils.Error("SwitchableTankManager: unable to load module from config:\n{}", n);
                    }
                }
                tanks.ForEach(t => t.OnStart(part.GetModuleStartState()));
            }
            else if(node.HasValue("Volume"))
            {
                var cfg = FromConfig<VolumeConfiguration>(node);
                Utils.Debug("Loading tank manager config: {}", cfg); //debug
                AddConfiguration(cfg, cfg.Volume, false, false, true);
            }
        }

        /// <summary>
        ///     Adds a tank of the provided type and value to the part, if possible.
        /// </summary>
        /// <returns><c>true</c>, if tank was added, <c>false</c> otherwise.</returns>
        /// <param name="tankType">Tank type.</param>
        /// <param name="volume">Tank volume.</param>
        /// <param name="resource">Current resource name.</param>
        /// <param name="amount">Initial amount of a resource in the tank: [0, 1]</param>
        /// <param name="update_counterparts">If counterparts are to be updated.</param>
        /// <param name="notify">If onTankAdded action should be invoked.</param>
        /// <param name="force">Force tank creation. No checks for AddRemoveEnabled
        /// or onValidateNewTank are made.</param>
        private bool AddTank(
            string tankType,
            float volume,
            string resource,
            float amount,
            bool update_counterparts,
            bool notify,
            bool force
        )
        {
            if(!SwitchableTankType.HaveTankType(tankType))
            {
                Utils.Error($"SwitchableTankManager: no such tank type: {tankType}");
                return false;
            }
            if(!force)
            {
                if(!Capabilities.AddRemoveEnabled)
                    return false;
                foreach(var validateTank in onValidateNewTank.GetInvocationList().Cast<TankValidator>())
                {
                    var error = validateTank(tankType, volume);
                    if(string.IsNullOrEmpty(error))
                        continue;
                    Utils.Message(error);
                    return false;
                }
            }
            var tank = part.AddModule(nameof(ModuleSwitchableTank)) as ModuleSwitchableTank;
            if(tank == null)
            {
                if(notify)
                    onTankFailedToAdd(tankType, volume);
                return false;
            }
            tank.id = ++max_id;
            tank.managed = true;
            tank.manager = this;
            tank.Volume = volume;
            tank.TankType = tankType;
            tank.EnablePartControls = EnablePartControls;
            tank.IncludeTankTypes = IncludeTankTypes;
            tank.ExcludeTankTypes = ExcludeTankTypes;
            tank.InitialAmount = HighLogic.LoadedSceneIsEditor ? Mathf.Clamp01(amount) : 0;
            if(!string.IsNullOrEmpty(resource))
                tank.CurrentResource = resource;
            try
            {
                tank.OnStart(part.GetModuleStartState());
                tanks.ForEach(t => t.RegisterOtherTank(tank));
            }
            catch
            {
                host.Error($"Unable to initialize {tank.GetID()}: {tankType} : {volume} m3 : res '{resource}'");
                if(notify)
                    onTankFailedToAdd(tankType, volume);
                return false;
            }
            tanks.Add(tank);
            InvalidateCaches();
            if(notify)
                onTankAdded(tank);
            if(update_counterparts)
                update_symmetry_managers(m => m.AddTank(tankType, volume, resource, amount, false, notify, force));
            return true;
        }

        /// <summary>
        ///     Adds tanks according to the configuration.
        /// </summary>
        /// <returns><c>true</c>, if configuration was added, <c>false</c> otherwise.</returns>
        /// <param name="cfg">Predefined configuration of tanks.</param>
        /// <param name="volume">Total volume of the configuration.</param>
        /// <param name="update_counterparts">If counterparts are to be updated.</param>
        /// <param name="notify">If onTankAdded action should be invoked.</param>
        /// <param name="force">Force creation of the tanks.</param>
        private bool AddConfiguration(
            VolumeConfiguration cfg,
            float volume,
            bool update_counterparts,
            bool notify,
            bool force
        )
        {
            if(!((force || Capabilities.AddRemoveEnabled) && cfg.Valid))
                return false;
            var V = cfg.TotalVolume;
            foreach(var v in cfg.Volumes)
                switch(v)
                {
                    case TankVolume t:
                        AddTank(t.TankType,
                            volume * v.Volume / V,
                            t.CurrentResource,
                            t.InitialAmount,
                            update_counterparts,
                            notify,
                            force);
                        continue;
                    case VolumeConfiguration c:
                        AddConfiguration(c, volume * v.Volume / V, update_counterparts, notify, force);
                        continue;
                }
            return true;
        }

        /// <summary>
        ///     Searches for a named tank type or configuration and adds tanks accordingly.
        /// </summary>
        /// <returns><c>true</c>, if configuration was added, <c>false</c> otherwise.</returns>
        /// <param name="name">A name of a tank type or tank configuration.</param>
        /// <param name="volume">Total volume of the configuration.</param>
        /// <param name="update_counterparts">If counterparts are to be updated.</param>
        public bool AddVolume(string name, float volume, bool update_counterparts = true)
        {
            if(!Capabilities.AddRemoveEnabled)
                return false;
            var cfg = VolumeConfigsLibrary.GetConfig(name);
            return cfg == null
                ? AddTank(name, volume, "", 0, update_counterparts, false, false)
                : AddConfiguration(cfg, volume, update_counterparts, false, false);
        }

        bool ITankManager.AddTank(string tankType, float volume) => AddVolume(tankType, volume);

        /// <summary>
        ///     Removes the tank from the part, if possible. Removed tank is destroyed immediately,
        ///     so the provided reference becomes invalid.
        /// </summary>
        /// <returns><c>true</c>, if tank was removed, <c>false</c> otherwise.</returns>
        /// <param name="tank">Tank to be removed.</param>
        /// <param name="update_counterparts">If counterparts are to be updated.</param>
        /// <param name="notify">If onTankAdded action should be invoked.</param>
        public bool RemoveTank(ModuleSwitchableTank tank, bool update_counterparts = true, bool notify = true)
        {
            if(!Capabilities.AddRemoveEnabled)
                return false;
            if(!tanks.Contains(tank))
                return false;
            if(!tank.TryRemoveResource())
                return false;
            tanks.Remove(tank);
            tanks.ForEach(t => t.UnregisterOtherTank(tank));
            part.RemoveModule(tank);
            InvalidateCaches();
            if(notify)
                onTankRemoved(tank);
            if(update_counterparts)
                update_symmetry_managers(m => m.RemoveTank(m.GetTank(tank.id), false, notify));
            part.UpdatePartMenu();
            return true;
        }

        bool ITankManager.RemoveTank(ITankInfo tank)
        {
            if(tank is ModuleSwitchableTank tankModule)
                return RemoveTank(tankModule);
            return false;
        }

        public bool AddTankConfig(string configName)
        {
            var node = new ConfigNode();
            Save(node);
            var cfg = FromConfig<VolumeConfiguration>(node);
            if(cfg.Valid)
            {
                cfg.name = configName;
                cfg.Volume = TanksVolume;
                VolumeConfigsLibrary.AddOrSave(cfg);
                init_supported_types();
                return true;
            }
            Utils.Log("Configuration is invalid:\n{}\nThis should never happen!", node);
            return false;
        }

        public bool RemoveTankConfig(string configName)
        {
            if(!VolumeConfigsLibrary.RemoveConfig(configName))
                return false;
            init_supported_types();
            return true;
        }

        /// <summary>
        ///     Multiplies the Volume property of each tank by specified value.
        ///     Amounts of resources are not rescaled.
        /// </summary>
        /// <param name="relative_scale">Relative scale. Should be in [0, +inf] interval.</param>
        /// <param name="update_amounts">If true, amount of resource in each tank will also be updated.</param>
        public void RescaleTanks(float relative_scale, bool update_amounts)
        {
            if(relative_scale <= 0)
                return;
            tanks.ForEach(t => t.SetVolume(t.Volume * relative_scale, update_amounts));
            InvalidateCaches();
        }

        private void update_symmetry_managers(Action<SwitchableTankManager> action)
        {
            if(part.symmetryCounterparts.Count == 0)
                return;
            var ind = part.Modules.IndexOf(host);
            foreach(var p in part.symmetryCounterparts)
            {
                var manager_module = p.Modules.GetModule(ind) as ITankManagerHost;
                if(manager_module == null)
                {
                    Utils.Error($"SwitchableTankManager: counterparts should have ITankManager "
                                + $"module at {ind} position, but {p.GetID()} does not. This should never happen!");
                    continue;
                }
                var manager = manager_module.GetTankManager();
                if(manager == null)
                {
                    Utils.Error("SwitchableTankManager: WARNING, trying to update "
                                + $"ITankManager {manager_module.GetID()} with uninitialized SwitchableTankManager");
                    continue;
                }
                action(manager);
            }
        }

        private class DefaultCapabilities : ITankManagerCapabilities
        {
            public bool AddRemoveEnabled => HighLogic.LoadedSceneIsEditor;
            public bool ConfirmRemove => !HighLogic.LoadedSceneIsEditor;
            public bool TypeChangeEnabled => HighLogic.LoadedSceneIsEditor;
            public bool VolumeChangeEnabled => HighLogic.LoadedSceneIsEditor;
            public bool FillEnabled => HighLogic.LoadedSceneIsEditor;
            public bool EmptyEnabled => HighLogic.LoadedSceneIsEditor;
        }
    }
}
