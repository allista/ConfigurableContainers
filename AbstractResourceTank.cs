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
	public abstract class AbstractResourceTank : PartModule, IPartCostModifier
	{
		/// <summary>
		/// The config node provided to OnLoad.
		/// </summary>
		public ConfigNode ModuleSave;

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
		/// The difference between tha Part.cost and the initial value of the GetModuleCost.
		/// Used when the DoPatchCost flag is set.
		/// </summary>
		[KSPField(isPersistant = true)] public float CostPatch = 0;

		/// <summary>
		/// This is called within the GetModuleCost to calculate the cost of the tank.
		/// </summary>
		protected abstract float TankCost(float defaultCost);

		/// <summary>
		/// This is called within the GetModuleCost to calculate the cost of tank resurces.
		/// </summary>
		/// <param name="maxAmount">If true, returns the cost of maxAmount of resources; of currnt amount otherwise.</param>
		protected abstract float ResourcesCost(bool maxAmount = true);

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
		#endregion
	}
}

