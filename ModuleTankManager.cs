//   ModuleTankManager.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using UnityEngine;

namespace AT_Utils
{
    public class ModuleTankManager : AbstractResourceTank, ITankManager
    {
        #region Tanks
        SwitchableTankManager tank_manager;
        public SwitchableTankManager GetTankManager() { return tank_manager; }

        public override string GetInfo()
        { 
            var info = string.Format("Max. Volume: {0}\n", Utils.formatVolume(Volume)); 
            if(ModuleSave != null)
                info += SwitchableTankManager.GetInfo(this, ModuleSave);
            return info;
        }

        protected override float TankCost(float defaultCost)
        {
            if(ModuleSave == null || tank_manager != null) return 0;
            var volumes = ConfigNodeObject.FromConfig<VolumeConfiguration>(ModuleSave);
            return volumes.Cost();
        }

        protected override float TankMass(float defaultMass)
        {
            if(ModuleSave == null || tank_manager != null) return 0;
            var volumes = ConfigNodeObject.FromConfig<VolumeConfiguration>(ModuleSave);
            return volumes.AddMass();
        }

        protected override float ResourcesCost(bool maxAmount = true)
        {
            if(ModuleSave == null || tank_manager != null) return 0;
            var volumes = ConfigNodeObject.FromConfig<VolumeConfiguration>(ModuleSave);
            return volumes.ResourceCost(maxAmount);
        }

        protected override float ResourcesMass(bool maxAmount = true)
        {
            if(ModuleSave == null || tank_manager != null) return 0;
            var volumes = ConfigNodeObject.FromConfig<VolumeConfiguration>(ModuleSave);
            return volumes.ResourceMass(maxAmount);
        }

        void init_tank_manager()
        {
            if(tank_manager != null) return;
            tank_manager = new SwitchableTankManager(this);
            if(ModuleSave == null) 
            { 
                this.Log("ModuleSave is null. THIS SHOULD NEVER HAPPEN!"); 
                return; 
            }
            ModuleSave.SetValue("Volume", Volume);
            tank_manager.Load(ModuleSave);
            var used_volume = tank_manager.TotalVolume;
            if(used_volume > Volume) 
            {
                this.Log("WARNING: Volume limit is less than the total volume " +
                         "of preconfigured tanks: {} - {} = {}", 
                         Volume, used_volume, Volume-used_volume);
                Volume = used_volume;
            }
            tank_manager.Volume = Volume;
        }

        protected override void init_from_part()
        {
            if(ModuleSave == null) ModuleSave = new ConfigNode("MODULE");
            var volume = VolumeConfiguration.FromResources(part.Resources);
            if(volume == null)
            {
                Utils.Message("TankManager module is added to a part with unknown resource!\n" +
                              "This is an error in MM patch.\n" +
                              "TankManager module is disabled.");
                this.EnableModule(false);
                part.Modules.Remove(this);
            }
            volume.name = ModuleSave.GetValue("name");
            ModuleSave.RemoveValue("Volume");
            ModuleSave.RemoveNodes(TankVolume.NODE_NAME);
            volume.Save(ModuleSave);
            Volume = volume.Volume;
            DoCostPatch = false;
            DoMassPatch = true;
//            this.Log("ModuleSave was initialized from part in flight: {}", ModuleSave);//debug
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            ModuleSave = node;
            //FIXME: does not work, because MM does not add this value
            //if its an existing part and CC was just added by MM patch
            ModuleSaveFromPrefab |= node.GetValue("MM_REINITIALIZE") != null;
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            if(tank_manager != null)
                tank_manager.Save(node);
            else ModuleSave.CopyTo(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            init_tank_manager();
            tank_manager.EnablePartControls = !HighLogic.LoadedSceneIsEditor && tank_manager.TanksCount < 2;
            Utils.EnableEvent(Events["EditTanks"], !tank_manager.EnablePartControls);
            if(HighLogic.LoadedSceneIsFlight) Events["EditTanks"].guiName = "Manage Tanks";
        }

        public void Rescale(float relative_scale, bool update_amounts = false)
        { 
            if(tank_manager != null) 
                tank_manager.RescaleTanks(relative_scale, update_amounts);
            SetVolume(Volume*relative_scale);
        }

        public void SetVolume(float volume)
        {
            if(tank_manager != null) 
            {
                if(tank_manager.TotalVolume > volume)
                    volume = tank_manager.TotalVolume;
                tank_manager.Volume = volume;
            }
            Volume = volume;
        }

        //interface for ProceduralParts
        [KSPEvent(guiActive=false, active = true)]
        void OnPartVolumeChanged(BaseEventDetails data)
        {
            var volName = data.Get<string>("volName");
            var newTotalVolume = (float)data.Get<double>("newTotalVolume");
            if(volName == "Tankage") 
                Rescale(newTotalVolume/Volume, HighLogic.LoadedSceneIsEditor);
        }

        //interface for TweakScale
        [KSPEvent(guiActive=false, active = true)]
        void OnPartScaleChanged(BaseEventDetails data)
        {
            var scale = data.Get<float>("factorRelative");
            var abs_scale = data.Get<float>("factorAbsolute");
            if(ModuleSaveFromPrefab && scale.Equals(1) && !abs_scale.Equals(1))
                scale = abs_scale;
            if(!scale.Equals(1))
                Rescale(scale*scale*scale);
        }

        //workaround for ConfigNode non-serialization
        public override void OnBeforeSerialize()
        {
            if(tank_manager != null)
            {
                ModuleSave = new ConfigNode();
                Save(ModuleSave);
            }
            base.OnBeforeSerialize();
        }
        #endregion

        #region GUI
        enum TankWindows { None, EditTanks } //maybe we'll need more in the future
        readonly Multiplexer<TankWindows> selected_window = new Multiplexer<TankWindows>();

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Edit Tanks", active = true)]
        public void EditTanks()
        { 
            selected_window.Toggle(TankWindows.EditTanks);
            if(selected_window[TankWindows.EditTanks]) 
                tank_manager.UnlockEditor(); 
        }

        float add_tank(string tank_name, float volume, bool percent)
        {
            if(percent) volume = Volume*volume/100;
            var max  = GUILayout.Button("Max");
            var half = GUILayout.Button("1/2");
            var max_volume = (Volume - tank_manager.TotalVolume);
            if(max || volume > max_volume) volume = max_volume;
            else if(half) volume = max_volume/2;
            if(volume <= 0) GUILayout.Label("Add", Styles.inactive);
            else if(GUILayout.Button("Add", Styles.open_button))
                StartCoroutine(
                    CallbackUtil.DelayedCallback(1, do_add_tank, tank_name, volume));
            return percent? (Volume.Equals(0)? 0 : volume/Volume*100) : volume;
        }
        
        private void do_add_tank(string tank_name, float volume) => 
            tank_manager.AddVolume(tank_name, volume);

        void remove_tank(ModuleSwitchableTank tank) 
        { tank_manager.RemoveTank(tank); }

        public void OnGUI() 
        { 
            if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
            if(!selected_window || tank_manager == null || tank_manager.EnablePartControls) return;
            Styles.Init();
            if(selected_window[TankWindows.EditTanks])
            {
                if(HighLogic.LoadedSceneIsEditor)
                {
                    var title = string.Format("Available Volume: {0} of {1}", 
                                              Utils.formatVolume(Volume - tank_manager.TotalVolume), 
                                              Utils.formatVolume(Volume));
                    tank_manager.DrawTanksManagerWindow(GetInstanceID(), title, add_tank, remove_tank);
                }
                else if(HighLogic.LoadedSceneIsFlight)
                    tank_manager.DrawTanksControlWindow(GetInstanceID(), "Tank Manager");
                if(tank_manager.Closed) selected_window.Off();
            }
        }
        #endregion
    }

    public class TankManagerUpdater : ModuleUpdater<ModuleTankManager>
    {
        protected override void on_rescale(ModulePair<ModuleTankManager> mp, Scale scale)
        { mp.module.Rescale(scale.relative.volume); }
    }
}



