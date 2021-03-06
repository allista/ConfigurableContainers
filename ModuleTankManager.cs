﻿//   ModuleTankManager.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using CC.UI;
using JetBrains.Annotations;

namespace AT_Utils
{
    public class ModuleTankManager : AbstractResourceTank, ITankManagerHost, ITankManagerCapabilities
    {
        private SwitchableTankManager tank_manager;

        public SwitchableTankManager GetTankManager()
        {
            return tank_manager;
        }

        public override string GetInfo()
        {
            var info = $"<b>Max. Volume: {Utils.formatVolume(Volume)}</b>\n";
            if(ModuleSave != null)
                info += SwitchableTankManager.GetInfo(this, ModuleSave);
            return info.Trim();
        }

        protected override float TankCost(float defaultCost)
        {
            if(ModuleSave == null || tank_manager != null)
                return 0;
            var volumes = ConfigNodeObject.FromConfig<VolumeConfiguration>(ModuleSave);
            return volumes.Cost();
        }

        protected override float TankMass(float defaultMass)
        {
            if(ModuleSave == null || tank_manager != null)
                return 0;
            var volumes = ConfigNodeObject.FromConfig<VolumeConfiguration>(ModuleSave);
            return volumes.AddMass();
        }

        protected override float ResourcesCost(bool maxAmount = true)
        {
            if(ModuleSave == null || tank_manager != null)
                return 0;
            var volumes = ConfigNodeObject.FromConfig<VolumeConfiguration>(ModuleSave);
            return volumes.ResourceCost(maxAmount);
        }

        protected override float ResourcesMass(bool maxAmount = true)
        {
            if(ModuleSave == null || tank_manager != null)
                return 0;
            var volumes = ConfigNodeObject.FromConfig<VolumeConfiguration>(ModuleSave);
            return volumes.ResourceMass(maxAmount);
        }

        private void init_tank_manager()
        {
            if(tank_manager != null)
                return;
            tank_manager = new SwitchableTankManager(this);
            if(ModuleSave == null)
            {
                this.Log("ModuleSave is null. THIS SHOULD NEVER HAPPEN!");
                return;
            }
            ModuleSave.SetValue("Volume", Volume);
            tank_manager.Load(ModuleSave);
            var used_volume = tank_manager.TanksVolume;
            if(used_volume > Volume)
            {
                this.Log(
                    "WARNING: Volume limit is less than the total volume " + "of preconfigured tanks: {} - {} = {}",
                    Volume,
                    used_volume,
                    Volume - used_volume);
                Volume = used_volume;
            }
            tank_manager.Volume = Volume;
        }

        protected override void init_from_part()
        {
            if(ModuleSave == null)
                ModuleSave = new ConfigNode("MODULE");
            var volume = VolumeConfiguration.FromResources(part.Resources);
            if(volume == null)
            {
                Utils.Message("TankManager module is added to a part with unknown resource!\n"
                              + "This is an error in MM patch.\n"
                              + "TankManager module is disabled.");
                this.EnableModule(false);
                part.Modules.Remove(this);
                return;
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
            else
                ModuleSave.CopyTo(node);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            init_tank_manager();
#if DEBUG
            tank_manager.EnablePartControls = false;
#else
            tank_manager.EnablePartControls = !HighLogic.LoadedSceneIsEditor && tank_manager.TanksCount < 2;
#endif
            var editTankEvent = Events[nameof(EditTanks)];
            Utils.EnableEvent(editTankEvent, !tank_manager.EnablePartControls);
            if(HighLogic.LoadedSceneIsFlight)
                editTankEvent.guiName = "Manage Tanks";
        }

        private void OnDestroy()
        {
            tank_manager?.UI?.Close();
        }

        public void Rescale(float relative_scale, bool update_amounts = false)
        {
            Volume *= relative_scale;
            if(tank_manager == null)
                return;
            // temporarily set tank manager volume to max.float
            // to be able to rescale all tanks without clamping
            tank_manager.Volume = float.MaxValue;
            tank_manager.RescaleTanks(relative_scale, update_amounts);
            // then check if the resulting tanks volume exceeds the rescaled total volume
            // and increase the later if needed
            if(tank_manager.TanksVolume > Volume)
                Volume = tank_manager.TanksVolume;
            // finally, update tank manager volume
            tank_manager.Volume = Volume;
        }

        //interface for ProceduralParts
        [UsedImplicitly]
        [KSPEvent(guiActive = false, active = true)]
        private void OnPartVolumeChanged(BaseEventDetails data)
        {
            var volName = data.Get<string>("volName");
            var newTotalVolume = (float)data.Get<double>("newTotalVolume");
            if(volName == "Tankage" && !newTotalVolume.Equals(Volume))
                Rescale(newTotalVolume / Volume, HighLogic.LoadedSceneIsEditor);
        }

        //interface for TweakScale
        [UsedImplicitly]
        [KSPEvent(guiActive = false, active = true)]
        private void OnPartScaleChanged(BaseEventDetails data)
        {
            var scale = data.Get<float>("factorRelative");
            if(!scale.Equals(1))
                Rescale(scale * scale * scale, HighLogic.LoadedSceneIsEditor);
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

        #region ITankManagerCapabilities
        public bool AddRemoveEnabled => HighLogic.LoadedSceneIsEditor;
        public bool ConfirmRemove => !HighLogic.LoadedSceneIsEditor;
        public bool TypeChangeEnabled => HighLogic.LoadedSceneIsEditor;
        public bool VolumeChangeEnabled => HighLogic.LoadedSceneIsEditor;
        public bool FillEnabled => HighLogic.LoadedSceneIsEditor;
        public bool EmptyEnabled => HighLogic.LoadedSceneIsEditor;
        #endregion

        #region GUI
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Edit Tanks", active = true)]
        public void EditTanks() => tank_manager?.UI.Toggle(this);

        private void LateUpdate()
        {
            tank_manager?.UI.OnLateUpdate();
        }
        #endregion
    }

    public class TankManagerUpdater : ModuleUpdater<ModuleTankManager>
    {
        protected override void on_rescale(ModulePair<ModuleTankManager> mp, Scale scale)
        {
            if(!scale.relative.volume.Equals(1))
                mp.module.Rescale(scale.relative.volume, true);
        }
    }
}
