﻿//   SwitchableTankManager.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AT_Utils
{
    public interface ITankManager
    {
        SwitchableTankManager GetTankManager();
    }

    public partial class SwitchableTankManager : ConfigNodeObject
    {
        public new const string NODE_NAME = "TANKMANAGER";
        public const string MANAGED = "MANAGED";
        private readonly PartModule host;

        private readonly Part part;
        private readonly List<TankWrapper> tanks = new List<TankWrapper>();

        /// <summary>
        ///     If true, tanks may be added and removed.
        /// </summary>
        [Persistent]
        public bool AddRemoveEnabled = true;

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

        private float total_volume = -1;

        /// <summary>
        ///     If true, type of tanks may be changed.
        /// </summary>
        [Persistent]
        public bool TypeChangeEnabled = true;

        /// <summary>
        ///     Maximum total volume of all tanks in m^3. It is used for reference and in tank controls.
        /// </summary>
        public float Volume = -1;

        public SwitchableTankManager(PartModule host)
        {
            part = host.part;
            this.host = host;
        }

        public bool EnablePartControls
        {
            get => enable_part_controls;
            set
            {
                if(value == enable_part_controls)
                    return;
                enable_part_controls = value;
                tanks.ForEach(t => t.Tank.EnablePartControls = enable_part_controls);
            }
        }

        public IEnumerable<ModuleSwitchableTank> Tanks { get { return tanks.Select(t => t.Tank); } }
        public int TanksCount => tanks.Count;
        public float TotalCost => tanks.Aggregate(0f, (c, t) => c + t.Tank.Cost);

        public float TotalVolume
        {
            get
            {
                if(total_volume < 0)
                    total_volume = tanks.Aggregate(0f, (v, t) => v + t.Tank.Volume);
                return total_volume;
            }
        }

        public ModuleSwitchableTank GetTank(int id)
        {
            return tanks.Find(t => t.Tank.id == id);
        }

        public void ForceUpdateTotalVolume()
        {
            total_volume = -1;
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
            var info = "";
            if(TypeChangeEnabled)
                info += SwitchableTankType.TypesInfo(include, exclude);
            var volumes = FromConfig<VolumeConfiguration>(node);
            if(volumes.Valid)
                info = string.Concat(info, "Preconfigured Tanks:\n", volumes.Info());
            return info;
        }

        private void init_supported_types()
        {
            exclude = Utils.ParseLine(ExcludeTankTypes, Utils.Comma);
            include = Utils.ParseLine(IncludeTankTypes, Utils.Comma);
            SupportedTypes = SwitchableTankType.TankTypeNames(include, exclude);
            SupportedTypes.AddRange(VolumeConfigsLibrary.AllConfigNames(include, exclude));
            if(SupportedTypes.Count > 0)
                selected_tank_type = SupportedTypes[0];
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            if(tanks.Count == 0)
                return;
            tanks.ForEach(t => t.Tank.Save(node.AddNode(TankVolume.NODE_NAME)));
            node.AddValue(MANAGED, true);
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            tanks.Clear();
            total_volume = -1;
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
                        tanks.Add(new TankWrapper(tank, this));
                    }
                    else
                    {
                        Utils.Error("SwitchableTankManager: unable to load module from config:\n{}", n);
                    }
                }
                tanks.ForEach(t => t.Tank.OnStart(part.GetModuleStartState()));
            }
            else if(node.HasValue("Volume"))
            {
                var cfg = FromConfig<VolumeConfiguration>(node);
                var add_remove = AddRemoveEnabled;
                AddRemoveEnabled = true;
                Utils.Debug("Loading tank manager config: {}", cfg); //debug
                AddConfiguration(cfg, cfg.Volume, false);
                AddRemoveEnabled = add_remove;
            }
        }

        /// <summary>
        ///     Adds a tank of the provided type and value to the part, if possible.
        /// </summary>
        /// <returns><c>true</c>, if tank was added, <c>false</c> otherwise.</returns>
        /// <param name="tank_type">Tank type.</param>
        /// <param name="volume">Tank volume.</param>
        /// <param name="resource">Current resource name.</param>
        /// <param name="amount">Initial amount of a resource in the tank: [0, 1]</param>
        /// <param name="update_counterparts">If counterparts are to be updated.</param>
        public bool AddTank(
            string tank_type,
            float volume,
            string resource = "",
            float amount = 0,
            bool update_counterparts = true
        )
        {
            if(!AddRemoveEnabled)
                return false;
            if(!SwitchableTankType.HaveTankType(tank_type))
            {
                Utils.Error("SwitchableTankManager: no such tank type: {}", tank_type);
                return false;
            }
            var tank = part.AddModule(nameof(ModuleSwitchableTank)) as ModuleSwitchableTank;
            if(tank == null)
                return false;
            tank.id = ++max_id;
            tank.managed = true;
            tank.Volume = volume;
            tank.TankType = tank_type;
            tank.EnablePartControls = EnablePartControls;
            tank.IncludeTankTypes = IncludeTankTypes;
            tank.ExcludeTankTypes = ExcludeTankTypes;
            tank.InitialAmount = HighLogic.LoadedSceneIsEditor ? Mathf.Clamp01(amount) : 0;
            if(!string.IsNullOrEmpty(resource))
                tank.CurrentResource = resource;
            tank.OnStart(part.GetModuleStartState());
            tanks.ForEach(t => t.Tank.RegisterOtherTank(tank));
            tanks.Add(new TankWrapper(tank, this));
            total_volume = -1;
            if(update_counterparts)
                update_symmetry_managers(m => m.AddTank(tank_type, volume, resource, amount, false));
            return true;
        }

        /// <summary>
        ///     Adds tanks according to the configuration.
        /// </summary>
        /// <returns><c>true</c>, if configuration was added, <c>false</c> otherwise.</returns>
        /// <param name="cfg">Predefined configuration of tanks.</param>
        /// <param name="volume">Total volume of the configuration.</param>
        /// <param name="update_counterparts">If counterparts are to be updated.</param>
        public bool AddConfiguration(VolumeConfiguration cfg, float volume, bool update_counterparts = true)
        {
            if(!AddRemoveEnabled || !cfg.Valid)
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
                            update_counterparts);
                        continue;
                    case VolumeConfiguration c:
                        AddConfiguration(c, volume * v.Volume / V, update_counterparts);
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
            if(!AddRemoveEnabled)
                return false;
            var cfg = VolumeConfigsLibrary.GetConfig(name);
            return cfg == null
                ? AddTank(name, volume, update_counterparts: update_counterparts)
                : AddConfiguration(cfg, volume, update_counterparts);
        }

        /// <summary>
        ///     Removes the tank from the part, if possible. Removed tank is destroyed immediately,
        ///     so the provided reference becomes invalid.
        /// </summary>
        /// <returns><c>true</c>, if tank was removed, <c>false</c> otherwise.</returns>
        /// <param name="tank">Tank to be removed.</param>
        /// <param name="update_counterparts">If counterparts are to be updated.</param>
        public bool RemoveTank(ModuleSwitchableTank tank, bool update_counterparts = true)
        {
            if(!AddRemoveEnabled)
                return false;
            var wrapper = tanks.Find(t => t.Tank == tank);
            if(wrapper == null)
                return false;
            if(!tank.TryRemoveResource())
                return false;
            tanks.Remove(wrapper);
            tanks.ForEach(t => t.Tank.UnregisterOtherTank(tank));
            part.RemoveModule(tank);
            total_volume = -1;
            if(update_counterparts)
                update_symmetry_managers(m => m.RemoveTank(m.GetTank(tank.id), false));
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
            tanks.ForEach(t => t.SetVolume(t.Tank.Volume * relative_scale, update_amounts));
            total_volume = -1;
        }

        private void update_symmetry_managers(Action<SwitchableTankManager> action)
        {
            if(part.symmetryCounterparts.Count == 0)
                return;
            var ind = part.Modules.IndexOf(host);
            foreach(var p in part.symmetryCounterparts)
            {
                var manager_module = p.Modules.GetModule(ind) as ITankManager;
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

        private void update_symmetry_tanks(ModuleSwitchableTank tank, Action<ModuleSwitchableTank> action)
        {
            update_symmetry_managers
            (m =>
            {
                var tank1 = m.GetTank(tank.id);
                if(tank1 == null)
                    Utils.Warning($"SwitchableTankManager: no tank with {tank.id} id");
                else
                    action(tank1);
            });
        }
    }
}
