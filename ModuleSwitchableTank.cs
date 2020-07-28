//   ModuleSwitchableTank.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections.Generic;
using System.Linq;
using CC.UI;
using JetBrains.Annotations;
using UnityEngine;

namespace AT_Utils
{
    /// <summary>
    ///     This is a different approach than in ModularFuelTanks, more suitable for "cargo" resources than fuels:
    ///     Such tank may contain only one type of resources, but this type may be switched in-flight,
    ///     if the part has zero amount of the current resource.
    /// </summary>
    public class ModuleSwitchableTank : AbstractResourceTank, ITankInfo
    {
        private const string RES_MANAGED = "Res";
        private const string RES_UNMANAGED = "N/A";

        private readonly List<ModuleSwitchableTank> other_tanks = new List<ModuleSwitchableTank>();

        private ResourceBoiloff boiloff;

        [UsedImplicitly]
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Boiloff")]
        public string BoiloffDisplay = "0";

        /// <summary>
        ///     If a tank type can be selected in editor.
        /// </summary>
        [KSPField]
        public bool ChooseTankType;

        private ActiveCooling cooler;

        [UsedImplicitly]
        [KSPField(isPersistant = true,
            guiActive = false,
            guiActiveEditor = false,
            guiName = "Cooling",
            guiFormat = "P1")]
        public double CoolingDisplay;

        [UsedImplicitly]
        [KSPField(isPersistant = true,
            guiActive = false,
            guiActiveEditor = false,
            guiName = "T(Res)",
            guiFormat = "0.0°C")]
        public double CoreTemperatureDisplay;

        private string current_resource_name = string.Empty;

        /// <summary>
        ///     The name of a currently selected resource. Can be changed in flight if resource amount is zero.
        /// </summary>
        [UI_ChooseOption]
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = RES_MANAGED)]
        public string CurrentResource = string.Empty;

        string ITankInfo.CurrentResource => CurrentResource;

        [UsedImplicitly]
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Cooling Cost")]
        public string EcDisplay = "0";

        private bool enable_part_controls = true;

        private string[] exclude;

        /// <summary>
        ///     Excluded tank types. If empty, all types are supported.
        /// </summary>
        [KSPField]
        public string ExcludeTankTypes = string.Empty;

        [KSPField(isPersistant = true)] public int id = -1;

        private string[] include;

        /// <summary>
        ///     Supported tank types. If empty, all types are supported. Overrides ExcludedTankTypes.
        /// </summary>
        [KSPField]
        public string IncludeTankTypes = string.Empty;

        /// <summary>
        ///     The initial partial amount of the CurrentResource.
        ///     Should be in the [0, 1] interval.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float InitialAmount;

        [KSPField(isPersistant = true)] public bool managed;
        private string previous_resource = string.Empty;

        private TankResource resource_info;
        public List<string> SupportedTypes = new List<string>();
        IList<string> ITankInfo.SupportedTypes => SupportedTypes;


        private SwitchableTankType tank_type;

        /// <summary>
        ///     The type of the tank. Types are defined in separate config nodes. Cannot be changed in flight.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Type")]
        [UI_ChooseOption(scene = UI_Scene.Editor)]
        public string TankType;

        string ITankInfo.TankType => TankType;
        public IList<string> SupportedResources => tank_type?.ResourceNames ?? new List<string>();

        public SwitchableTankManager manager;
        public ITankManager Manager => manager;

        public bool EnablePartControls
        {
            get => enable_part_controls;
            set
            {
                if(enable_part_controls == value)
                    return;
                enable_part_controls = value;
                disable_part_controls();
                init_type_control();
                init_res_control();
            }
        }

        public SwitchableTankType Type => tank_type;

        /// <summary>
        ///     Cost of an empty tank of current type and volume
        /// </summary>
        public float Cost => tank_type?.Cost(Volume) ?? 0;

        /// <summary>
        ///     Additional mass of an empty tank of current type and volume
        /// </summary>
        public float AddMass => tank_type?.AddMass(Volume) ?? 0;

        public PartResource Resource { get; private set; }
        public float Usage => Resource != null ? (float)(Resource.amount / Resource.maxAmount) : 0;
        public float ResourceDensity => Resource != null ? Resource.info.density : 0;
        public float ResourceMass => Resource != null ? (float)(Resource.amount * Resource.info.density) : 0;
        public float ResourceMaxMass => Resource != null ? (float)(Resource.maxAmount * Resource.info.density) : 0;
        public string ResourceInUse => Resource != null ? Resource.resourceName : string.Empty;
        float ITankInfo.Volume => Volume;
        bool ITankInfo.Valid => Resource != null;

        public double Amount
        {
            get => Resource?.amount ?? 0;
            set
            {
                if(Resource != null)
                    Resource.amount = Utils.Clamp(value, 0, Resource.maxAmount);
            }
        }

        public double MaxAmount
        {
            get => Resource?.maxAmount ?? 0;
            set
            {
                if(Resource != null)
                    Resource.maxAmount = value;
            }
        }

        public float MaxResourceInVolume => ResourceAmountInVolume(Volume);

        public float ResourceAmountInVolume(float volume) =>
            tank_type == null || resource_info == null
                ? 0
                : tank_type.UsefulVolume(volume) * resource_info.UnitsPerVolume;

        public float VolumeForResourceAmount(float amount)
        {
            if(tank_type == null || resource_info == null)
                return 0;
            var effectiveVolumeRatio = tank_type.GetEffectiveVolumeRatio();
            return effectiveVolumeRatio > 0
                ? amount / resource_info.UnitsPerVolume / effectiveVolumeRatio
                : float.PositiveInfinity;
        }

        public override string GetInfo()
        {
            var info = StringBuilderCache.Acquire();
            info.AppendLine($"<b>Tank Volume: {Utils.formatVolume(Volume)}</b>");
            init_supported_types();
            if(ChooseTankType)
                info.AppendLine(SwitchableTankType.TypesInfo(include, exclude));
            if(init_tank_type())
                info.AppendLine(tank_type.Info);
            return info.ToStringAndRelease().Trim();
        }

        protected override float TankMass(float defaultMass)
        {
            if(tank_type == null && !init_tank_type())
                return 0;
            return AddMass;
        }

        protected override float TankCost(float defaultCost)
        {
            if(tank_type == null && !init_tank_type())
                return 0;
            return Cost;
        }

        protected override float ResourcesCost(bool maxAmount = true)
        {
            float cost;
            if(Resource != null)
            {
                cost = (float)Resource.maxAmount * Resource.info.unitCost;
            }
            else
            {
                if(tank_type == null && !init_tank_type())
                    return 0;
                resource_info = tank_type[CurrentResource];
                if(resource_info == null)
                    return 0;
                cost = MaxResourceInVolume * resource_info.Resource.unitCost;
            }
            return maxAmount ? cost : cost * InitialAmount;
        }

        protected override float ResourcesMass(bool maxAmount = true)
        {
            float mass;
            if(Resource != null)
            {
                mass = (float)Resource.maxAmount * Resource.info.density;
            }
            else
            {
                if(tank_type == null && !init_tank_type())
                    return 0;
                resource_info = tank_type[CurrentResource];
                if(resource_info == null)
                    return 0;
                mass = MaxResourceInVolume * resource_info.Resource.density;
            }
            return maxAmount ? mass : mass * InitialAmount;
        }

        private void OnDestroy()
        {
            Utils.UpdateEditorGUI();
        }

        private void init_supported_types()
        {
            include = Utils.ParseLine(IncludeTankTypes, Utils.Comma);
            exclude = Utils.ParseLine(ExcludeTankTypes, Utils.Comma);
            SupportedTypes = SwitchableTankType.TankTypeNames(include, exclude);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            init_supported_types();
            //get other tanks in this part
            other_tanks.AddRange(
                from t in part.Modules.GetModules<ModuleSwitchableTank>()
                where t != this
                select t);
            //initialize tank type chooser
            disable_part_controls();
            if(state == StartState.Editor)
                init_type_control();
            init_tank_type();
            init_resource();
            init_res_control();
            Fields[nameof(TankType)].OnValueModified += onTankTypeChange;
            Fields[nameof(CurrentResource)].OnValueModified += onCurrentResourceChange;
            StartCoroutine(slow_update());
        }

        protected override void init_from_part()
        {
            //if part has multiple resources, we're in trouble
            if(part.Resources.Count > 1)
            {
                Utils.Message("SwitchableTank module is added to a part with multiple resources!\n"
                              + "This is an error in MM patch.\n"
                              + "SwitchableTank module is disabled.");
                this.EnableModule(false);
                part.Modules.Remove(this);
            }
            var res = part.Resources[0];
            var tank = TankVolume.FromResource(res);
            if(tank == null)
            {
                Utils.Message("SwitchableTank module is added to a part with unknown resource!\n"
                              + "This is an error in MM patch.\n"
                              + "SwitchableTank module is disabled.");
                this.EnableModule(false);
                part.Modules.Remove(this);
                return;
            }
            DoCostPatch = false;
            DoMassPatch = true;
            TankType = tank.TankType;
            CurrentResource = tank.CurrentResource;
            Volume = tank.Volume;
            InitialAmount = tank.InitialAmount;
//            this.Log("Initialized from part in flight: TankType {}, CurrentResource {}, Volume {}, InitialAmount {}", 
//                     TankType, CurrentResource, Volume, InitialAmount);//debug
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
//            this.Log("OnLoad: ModuleSave: {}", node);//debug
            //if the config comes from TankManager, save its config
            if(node.HasValue(SwitchableTankManager.MANAGED))
            {
                ModuleSave = node;
                managed = true;
            }
            //if the node is not from a TankManager, but we have a saved config, reload it
            else if(ModuleSave != null && ModuleSave.HasValue(SwitchableTankManager.MANAGED))
            {
                Load(ModuleSave);
            }
            //if it is a managed tank, but config does not come from TankManager
            else if(managed)
            {
                part.Modules.Remove(this);
            }
            //this is a stand-alone tank; save initial MODULE configuration
            else if(ModuleSave == null)
            {
                ModuleSave = node;
                //FIXME: does not work, because MM does not add this value
                //if its an existing part and CC was just added by MM patch
                ModuleSaveFromPrefab |= node.GetValue("MM_REINITIALIZE") != null;
            }
        }

        public override void OnSave(ConfigNode node)
        {
            if(Resource != null)
                InitialAmount = (float)(Resource.amount / Resource.maxAmount);
            base.OnSave(node);
            boiloff?.SaveInto(node);
        }

        /// <summary>
        ///     Adds the given SwitchableTank to the list of all tanks
        ///     whose CurrentResource is checked upon resource switching.
        /// </summary>
        public void RegisterOtherTank(ModuleSwitchableTank tank)
        {
            if(!other_tanks.Contains(tank))
                other_tanks.Add(tank);
        }

        /// <summary>
        ///     Removes the given SwitchableTank from the list of all tanks
        ///     whose CurrentResource is checked upon resource switching.
        /// </summary>
        public bool UnregisterOtherTank(ModuleSwitchableTank tank)
        {
            return other_tanks.Remove(tank);
        }

        /// <summary>
        ///     If some resource is currently managed by the tank, checks
        ///     if its amount is zero and, if so, removes the resource from the part.
        /// </summary>
        /// <returns>
        ///     <c>true</c>, if resource was removed or was not present,
        ///     <c>false</c> otherwise.
        /// </returns>
        public bool TryRemoveResource()
        {
            if(Resource == null)
                return true;
            if(HighLogic.LoadedSceneIsEditor)
                Resource.amount = 0;
            if(Resource.amount > 0)
            {
                Utils.Message("Tank is in use");
                CurrentResource = Resource.resourceName;
                if(tank_type != null)
                    TankType = tank_type.name;
                return false;
            }
            part.RemoveResource(Resource.resourceName);
            current_resource_name = string.Empty;
            Resource = null;
            return true;
        }

        private void init_type_control()
        {
            if(!enable_part_controls || !ChooseTankType || SupportedTypes.Count <= 1)
                return;
            var tank_names = SupportedTypes.Select(Utils.ParseCamelCase).ToArray();
            Utils.SetupChooser(tank_names, SupportedTypes.ToArray(), Fields[nameof(TankType)]);
            Utils.EnableField(Fields[nameof(TankType)]);
        }

        private void update_cooler_control()
        {
            if(cooler == null)
                return;
            Events["ToggleCooler"].guiName = cooler.Enabled
                ? $"Disable {current_resource_name} Cooling"
                : $"Enable {current_resource_name} Cooling";
        }

        private void update_boiloff_control()
        {
            if(boiloff != null && boiloff.Valid)
            {
                Fields["CoreTemperatureDisplay"].guiActive = true;
                Fields["CoreTemperatureDisplay"].guiName = current_resource_name;
                Fields["BoiloffDisplay"].guiActiveEditor = true;
                Fields["BoiloffDisplay"].guiName = current_resource_name + " Boiloff";
                if(cooler != null)
                {
                    Fields["CoolingDisplay"].guiActive = true;
                    Fields["CoolingDisplay"].guiName = current_resource_name + " Cooling";
                    Fields["EcDisplay"].guiActiveEditor = true;
                    Fields["EcDisplay"].guiName = current_resource_name + " Cooling Cost";
                    Events["ToggleCooler"].active = true;
                    update_cooler_control();
                }
                else
                {
                    Fields["CoolingDisplay"].guiActive = false;
                    Events["ToggleCooler"].active = false;
                }
            }
            else
            {
                Fields["CoreTemperatureDisplay"].guiActive = false;
                Fields["CoolingDisplay"].guiActive = false;
                Fields["EcDisplay"].guiActiveEditor = false;
                Events["ToggleCooler"].active = false;
            }
        }

        private void init_res_control()
        {
            var f = Fields[nameof(CurrentResource)];
            if(tank_type == null || !enable_part_controls || tank_type.Resources.Count <= 1)
            {
                Utils.EnableField(f, false);
            }
            else
            {
                var res_values = tank_type.ResourceNames.ToArray();
                var res_names = tank_type.ResourceNames.Select(Utils.ParseCamelCase).ToArray();
                Utils.SetupChooser(res_names, res_values, Fields[nameof(CurrentResource)]);
                Utils.EnableField(f);
            }
            update_boiloff_control();
            part.UpdatePartMenu();
        }

        private void update_res_control()
        {
            Fields[nameof(CurrentResource)].guiName = Resource == null ? RES_UNMANAGED : RES_MANAGED;
            update_boiloff_control();
            part.UpdatePartMenu();
        }

        private void disable_part_controls()
        {
            Utils.EnableField(Fields[nameof(TankType)], false);
            Utils.EnableField(Fields[nameof(CurrentResource)], false);
        }

        private bool init_tank_type()
        {
            if(Volume < 0)
                Volume = Metric.Volume(part);
            if(tank_type != null)
                return true;
            boiloff = null;
            //if tank type is not provided, use the first one from the library
            if(string.IsNullOrEmpty(TankType))
                TankType = SwitchableTankType.TankTypeNames(include, exclude)[0];
            //select tank type from the library
            if(!SwitchableTankType.TankTypes.TryGetValue(TankType, out tank_type))
                Utils.Message(6,
                    "No \"{0}\" tank type in the library.\n" + "Configuration of \"{1}\" is INVALID.",
                    TankType,
                    this.Title());
            if(tank_type == null)
                return false;
            //initialize current resource
            if(CurrentResource == string.Empty || !tank_type.Resources.ContainsKey(CurrentResource))
                CurrentResource = tank_type.DefaultResource.Name;
            //initialize boiloff/cooling
            if(!tank_type.Boiloff && !tank_type.Cooling)
                return true;
            boiloff = tank_type.Boiloff ? new ResourceBoiloff(this) : new ActiveCooling(this);
            if(ModuleSave != null)
                boiloff.LoadFrom(ModuleSave);
            cooler = boiloff as ActiveCooling;
            return true;
        }

        private bool change_tank_type()
        {
            //check if the tank is in use
            if(tank_type != null && Resource != null && Resource.amount > 0)
            {
                if(HighLogic.LoadedSceneIsEditor)
                {
                    Resource.amount = 0;
                }
                else
                {
                    Utils.Message("Cannot change tank type while tank is in use");
                    TankType = tank_type.name;
                    return false;
                }
            }
            //setup new tank type
            tank_type = null;
            var result = init_tank_type() && switch_resource();
            init_res_control();
            return result;
        }

        /// <summary>
        ///     Check if the resource 'res' is managed by any other tank.
        /// </summary>
        /// <returns><c>true</c>, if resource is used, <c>false</c> otherwise.</returns>
        /// <param name="res">resource name</param>
        private bool resource_in_use(string res)
        {
            return other_tanks.Any(t => t.ResourceInUse == res);
        }

        /// <summary>
        ///     Sets the maxAmount of the current resource to MaxResourceInVolume.
        ///     Optionally updates the amount of current resource.
        /// </summary>
        /// <param name="update_amount">If set to <c>true</c> also updates amount.</param>
        public void UpdateMaxAmount(bool update_amount = false)
        {
            if(Resource == null)
                return;
            var max_amount = Resource.maxAmount;
            Resource.maxAmount = MaxResourceInVolume;
            if(Resource.amount > Resource.maxAmount)
                Resource.amount = Resource.maxAmount;
            else if(update_amount && max_amount > 0)
                Resource.amount *= Resource.maxAmount / max_amount;
            part.UpdatePartMenu();
        }

        /// <summary>
        ///     Sets the volume of the tank and updates maxAmount of the current resource.
        ///     Optionally updates the amount of current resource.
        /// </summary>
        /// <param name="volume">New tank volume.</param>
        /// <param name="update_amount">If set to <c>true</c> also updates amount.</param>
        public void SetVolume(float volume, bool update_amount = false)
        {
            if(volume < 0)
                volume = 0;
            manager?.ClampNewVolume(Volume, ref volume);
            Volume = volume;
            UpdateMaxAmount(update_amount);
            boiloff?.UpdateInsulation();
            manager?.InvalidateCaches();
        }

        /// <summary>
        /// Change current tank type
        /// </summary>
        /// <param name="tankTypeName">The name of the new TankType.</param>
        public void ChangeTankType(string tankTypeName)
        {
            Fields[nameof(TankType)].SetValue(tankTypeName, this);
        }

        /// <summary>
        /// Change current resource
        /// </summary>
        /// <param name="resourceName">The name of the new Resource.</param>
        public void ChangeResource(string resourceName)
        {
            Fields[nameof(CurrentResource)].SetValue(resourceName, this);
        }

        /// <summary>
        /// Set current amount of the Resource, subject to clamping at Resource.maxAmount.
        /// This a part of ITankInfo implementation.
        /// </summary>
        /// <param name="newAmount">New amount of the current Resource.</param>
        public void SetAmount(float newAmount)
        {
            Amount = newAmount;
        }

        /// <summary>
        ///     Forces the switch of the current resource, even if the new resource belongs to another
        ///     tank type, in which case the type is also switched. If the amount of current resource
        ///     is not zero, it is discarded. After the switch the tank remains empty.
        /// </summary>
        /// <returns><c>true</c>, if resource was successfully switched, <c>false</c> otherwise.</returns>
        /// <param name="new_resource">New resource name.</param>
        public bool ForceSwitchResource(string new_resource)
        {
            //if nothing to do, return true
            if(Resource != null && Resource.resourceName == new_resource)
                return true;
            //if the new resource is in the current tank type
            if(tank_type != null && tank_type.Resources.ContainsKey(new_resource))
            {
                if(Resource != null)
                    Resource.amount = 0;
                CurrentResource = new_resource;
                if(!switch_resource())
                    return false;
                update_res_control();
                return true;
            }
            var new_type = SwitchableTankType.FindTankType(new_resource);
            if(new_type == null)
                return false;
            if(Resource != null)
                Resource.amount = 0;
            TankType = new_type.name;
            CurrentResource = new_resource;
            return change_tank_type();
        }

        private bool init_resource()
        {
            if(Resource != null)
            {
                CurrentResource = Resource.resourceName;
                return false;
            }
            if(tank_type == null)
                return false;
            //check if this is tank initialization or switching
            var initializing = previous_resource == string.Empty;
            previous_resource = CurrentResource;
            //check if the resource is in use by another tank
            if(resource_in_use(CurrentResource))
            {
                Utils.Message(6, "A part cannot have more than one resource of any type");
#if DEBUG
                this.Log("this tank: {}\nothers: {}",
                    CurrentResource,
                    other_tanks.Select(t => t.GetInstanceID() + ": " + t.CurrentResource));
#endif
                return false;
            }
            //get definition of the next not-managed resource
            resource_info = tank_type[CurrentResource];
            var maxAmount = MaxResourceInVolume;
            //if there is such resource already, just plug it in
            var part_res = part.Resources[resource_info.Name];
            if(part_res != null)
            {
                Resource = part_res;
                //do not change resource amount/maxAmount in flight, unless we have none
                if(HighLogic.LoadedSceneIsEditor || Resource.amount.Equals(0))
                {
                    Resource.maxAmount = maxAmount;
                    if(Resource.amount > Resource.maxAmount)
                        Resource.amount = Resource.maxAmount;
                }
            }
            else //create the new resource
            {
                var node = new ConfigNode("RESOURCE");
                node.AddValue("name", resource_info.Name);
                node.AddValue("amount", initializing ? maxAmount * InitialAmount : 0);
                node.AddValue("maxAmount", maxAmount);
                Resource = part.AddResource(node);
            }
            current_resource_name = Utils.ParseCamelCase(CurrentResource);
            boiloff?.SetResource(Resource);
            if(part.Events != null)
                part.SendEvent("resource_changed");
            return true;
        }

        private bool switch_resource()
        {
            return TryRemoveResource() && init_resource();
        }

        [UsedImplicitly]
        [KSPEvent]
        private void resource_changed()
        {
            if(Resource != null)
                return;
            switch_resource();
        }

        //interface for ProceduralParts
        [UsedImplicitly]
        [KSPEvent(guiActive = false, active = true)]
        private void OnPartVolumeChanged(BaseEventDetails data)
        {
            if(managed)
                return;
            var volName = data.Get<string>("volName");
            var newTotalVolume = (float)data.Get<double>("newTotalVolume");
            if(volName == "Tankage" && !newTotalVolume.Equals(Volume))
                SetVolume(newTotalVolume, HighLogic.LoadedSceneIsEditor);
        }

        //interface for TweakScale
        [UsedImplicitly]
        [KSPEvent(guiActive = false, active = true)]
        private void OnPartScaleChanged(BaseEventDetails data)
        {
            if(managed)
                return;
            var scale = data.Get<float>("factorRelative");
            if(!scale.Equals(1))
                SetVolume(Volume * scale * scale * scale, HighLogic.LoadedSceneIsEditor);
        }

        [UsedImplicitly]
        [KSPEvent(guiActive = true, guiName = "Disable Cooling", active = false)]
        private void ToggleCooler()
        {
            if(cooler == null)
                return;
            cooler.Enabled = !cooler.Enabled;
            update_cooler_control();
        }

#if DEBUG
        [UsedImplicitly]
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Reload Cryogenics", active = true)]
        private void ReloadCryogenics()
        {
            CryogenicsParams.Reload();
            if(boiloff != null && Resource != null)
                boiloff.SetResource(Resource);
        }
#endif

        private void onCurrentResourceChange(object value)
        {
            if(CurrentResource == previous_resource)
                return;
            switch_resource();
            update_res_control();
        }

        private void onTankTypeChange(object value)
        {
            if(HighLogic.LoadedSceneIsEditor)
            {
                if(tank_type == null || tank_type.name != TankType)
                    change_tank_type();
                if(boiloff != null && boiloff.Valid)
                    BoiloffDisplay = $"~{Utils.formatSmallValue((float)boiloff.BoiloffAt300K * 3600, "u/h")}";
                if(cooler != null && boiloff != null && boiloff.Valid)
                    EcDisplay = $"~{Utils.formatSmallValue((float)cooler.PowerConsumptionAt300K, "Ec/s")}";
            }
            else if(tank_type != null && tank_type.name != TankType)
            {
                Utils.Message("Cannot change the type of the already constructed tank");
                TankType = tank_type.name;
            }
        }

        private IEnumerator<YieldInstruction> slow_update()
        {
            while(true)
            {
                if(HighLogic.LoadedSceneIsFlight)
                {
                    //temperature display
                    if(boiloff != null)
                        CoreTemperatureDisplay = boiloff.CoreTemperature + CryogenicsParams.AbsZero;
                    if(cooler != null)
                    {
                        CoolingDisplay = cooler.IsCooling ? cooler.CoolingEfficiency : 0;
                        update_cooler_control();
                    }
#if DEBUG
                    PartTemperatureDisplay = (part.temperature + CryogenicsParams.AbsZero).ToString("F1");
                    SkinTemperatureDisplay = (part.skinTemperature + CryogenicsParams.AbsZero).ToString("F1");
#endif
                }
                yield return new WaitForSeconds(0.1f);
            }
            // ReSharper disable once IteratorNeverReturns
        }

        private void FixedUpdate()
        {
            if(HighLogic.LoadedSceneIsFlight)
                boiloff?.FixedUpdate();
        }

#if DEBUG
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Part", guiUnits = "°C")]
        public string PartTemperatureDisplay = "0";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Skin", guiUnits = "°C")]
        public string SkinTemperatureDisplay = "0";
#endif
    }

    public class SwitchableTankUpdater : ModuleUpdater<ModuleSwitchableTank>
    {
        protected override void on_rescale(ModulePair<ModuleSwitchableTank> mp, Scale scale)
        {
            if(!scale.relative.volume.Equals(1))
                mp.module.SetVolume(mp.module.Volume * scale.relative.volume, true);
        }
    }
}
