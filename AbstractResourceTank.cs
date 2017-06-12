//   AbstractResourceTank.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using UnityEngine;

namespace AT_Utils
{
	public abstract class AbstractResourceTank : SerializableFiledsPartModule, IPartCostModifier, IPartMassModifier, IModuleInfo
	{
		/// <summary>
		/// The config node provided to OnLoad.
		/// </summary>
		[SerializeField] public ConfigNode ModuleSave;

        /// <summary>
        /// If true, the module save was received not in the flight scene.
        /// </summary>
        [KSPField(isPersistant = true)] public bool ModuleSaveFromPrefab;

		/// <summary>
		/// The volume of a tank in m^3. It is defined in a config or calculated from the part volume in editor.
		/// Cannot be changed in flight.
		/// </summary>
		[KSPField(isPersistant = true)] public float Volume = -1f;

		/// <summary>
		/// If set, this flag causes the Module to save the initial difference between the
		/// Part.cost and GetModuleCost value so that the total part cost is unchanged.
		/// </summary>
		[KSPField(isPersistant = true)] public bool DoCostPatch = false;

		/// <summary>
		/// The difference between the Part.cost and the initial value of the GetModuleCost.
		/// Used when the Patch flag is set.
		/// </summary>
		[KSPField(isPersistant = true)] public float CostPatch = 0;

		/// <summary>
		/// If set, this flag causes the Module to save the initial difference between the
		/// Part.mass and GetModuleMass value so that the total part cost is unchanged.
		/// </summary>
		[KSPField(isPersistant = true)] public bool DoMassPatch = false;

		/// <summary>
		/// The difference between the Part.mass and the initial value of the GetModuleMass.
		/// Used when the Patch flag is set.
		/// </summary>
		[KSPField(isPersistant = true)] public float MassPatch = 0;

		/// <summary>
		/// This is called within the GetModuleCost to calculate the cost of the tank.
		/// </summary>
		protected abstract float TankCost(float defaultCost);

		/// <summary>
		/// This is called within the GetModuleCost to calculate the cost of tank resurces.
		/// </summary>
		/// <param name="maxAmount">If true, returns the cost of maxAmount of resources; of currnt amount otherwise.</param>
		protected abstract float ResourcesCost(bool maxAmount = true);

		/// <summary>
		/// This is called within the GetModuleMass to calculate the mass of the tank.
		/// </summary>
		protected abstract float TankMass(float defaultMass);

		/// <summary>
		/// This is called within the GetModuleMass to calculate the mass of tank resurces.
		/// </summary>
		/// <param name="maxAmount">If true, returns the mass of maxAmount of resources; of currnt amount otherwise.</param>
		protected abstract float ResourcesMass(bool maxAmount = true);

		#region IPart*Modifiers
		public virtual float GetModuleCost(float defaultCost, ModifierStagingSituation sit) 
		{ 
			var cost = TankCost(defaultCost);
			if(DoCostPatch) 
			{
				CostPatch = -Mathf.Min(cost+ResourcesCost(false), defaultCost);
				DoCostPatch = false;
			}
			var res = part.partInfo != null && part.partInfo.partPrefab == part? ResourcesCost(false) : ResourcesCost();
			return cost + res + CostPatch;
		}
		public virtual ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

		//this is not called by PartListTooltip.GetPartStats
		public virtual float GetModuleMass(float defaultMass, ModifierStagingSituation sit) 
		{ 
			var mass = TankMass(defaultMass);
			if(DoMassPatch) 
			{
				MassPatch = -Mathf.Min(mass, defaultMass);
				DoMassPatch = false;
			}
			return mass + MassPatch;
		}
		public virtual ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
		#endregion

		#region IModInfo
		public override string GetInfo() { return ""; }

		public virtual string GetModuleTitle() { return KSPUtil.PrintModuleName(moduleName); }

		public virtual string GetPrimaryField()
		{ 
			var info = "<b>Additional Mass:</b>\n";
			var tank = DoMassPatch? 0 : TankMass(part.mass);
			var res  = ResourcesMass(false);
			if(tank > 0) info += Utils.formatMass(tank)+" internals";
			if(res > 0) info += (tank > 0? "+" : "") + Utils.formatMass(res)+" resources";
			return tank > 0 || res > 0? info : "";
		}

		public Callback<Rect> GetDrawModulePanelCallback() { return null; }
		#endregion

        #if DEBUG
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Module")]
        public string ThisModule = "";

        public override void OnAwake()
        {
            base.OnAwake();
            { ThisModule = GetType().Name; }
        }
        #endif

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            ModuleSaveFromPrefab = !HighLogic.LoadedSceneIsFlight;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            //this means the module was added by MM patch to an existing part
            if(HighLogic.LoadedSceneIsFlight && ModuleSaveFromPrefab)
                init_from_part();
        }

        protected abstract void init_from_part();
	}
}

