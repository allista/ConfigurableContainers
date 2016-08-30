//   ModuleSwitchableTank.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AT_Utils
{
	/// <summary>
	/// This is a different approach than in ModularFuelTanks, more suitable for "cargo" resources than fuels:
	/// Such tank may contain only one type of resources, but this type may be switched in-flight, 
	/// if the part has zero amount of the current resource.
	/// </summary>
	public class ModuleSwitchableTank : PartModule, IPartCostModifier
	{
		const string   RES_MANAGED = "RES";
		const string RES_UNMANAGED = "N/A";

		bool enable_part_controls = true;
		public bool EnablePartControls 
		{ 
			get { return enable_part_controls; } 
			set 
			{ 
				enable_part_controls = value;
				disable_part_controls();
				init_type_control(); 
				if(tank_type != null)
					init_res_control();
			}
		}
		[KSPField(isPersistant = true)] public int id = -1;

		/// <summary>
		/// If a tank type can be selected in editor.
		/// </summary>
		[KSPField] public bool ChooseTankType;

		/// <summary>
		/// The type of the tank. Types are defined in separate config nodes. Cannot be changed in flight.
		/// </summary>
		[KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Tank Type")]
		[UI_ChooseOption(scene = UI_Scene.Editor)]
		public string TankType;
		SwitchableTankType tank_type;
		public SwitchableTankType Type { get { return tank_type; } }

		/// <summary>
		/// The volume of a tank in m^3. It is defined in a config or calculated from the part volume in editor.
		/// Cannot be changed in flight.
		/// </summary>
		[KSPField(isPersistant = true)] public float Volume = -1f;

		/// <summary>
		/// Cost of an empty tank of current type and volume
		/// </summary>
		public float Cost { get { return tank_type != null? Volume*tank_type.TankCostPerVolume : 0; } }

		/// <summary>
		/// The initial partial amount of the CurrentResource.
		/// Should be in the [0, 1] interval.
		/// </summary>
		[KSPField] public float InitialAmount;

		/// <summary>
		/// The name of a currently selected resource. Can be changed in flight if resource amount is zero.
		/// </summary>
		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = RES_MANAGED)]
		[UI_ChooseOption]
		public string CurrentResource = string.Empty;
		PartResource current_resource;
		string previous_resource = string.Empty;
		public float Usage { get { return current_resource != null? (float)(current_resource.amount/current_resource.maxAmount) : 0; } }
		public string ResourceInUse { get { return current_resource != null? CurrentResource : string.Empty; } }

		readonly List<ModuleSwitchableTank> other_tanks = new List<ModuleSwitchableTank>();

		public ConfigNode ModuleSave;

		public override string GetInfo()
		{
			var info = "";
			if(ChooseTankType) 
				info += SwitchableTankType.TypesInfo;
			if(!init_tank_type()) return info;
			info += tank_type.Info;
			init_tank_volume();
			info += "Tank Volume: " + Utils.formatVolume(Volume);
			return info;
		}

		#region IPart*Modifiers
		public virtual float GetModuleCost(float defaultCost, ModifierStagingSituation sit) 
		{ 
			return Cost + 
				(current_resource == null? 0 : (float)current_resource.maxAmount*current_resource.info.unitCost);
		}
		public virtual ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
		#endregion

		void OnDestroy() { Utils.UpdateEditorGUI(); }

		public override void OnStart(StartState state)
		{
			//get other tanks in this part
			other_tanks.AddRange(from t in part.Modules.OfType<ModuleSwitchableTank>()
								 where t != this select t);
			//initialize tank type chooser
			disable_part_controls();
			if(state == StartState.Editor) init_type_control();
			init_tank_volume();
			init_tank_type();
			StartCoroutine(slow_update());
		}

		public override void OnLoad(ConfigNode node)
		{
			//if the tank is managed, save its config
			if(node.HasValue(SwitchableTankManager.MANAGED)) ModuleSave = node;
			//if the nod is not from a TankManager, but we have a saved config, reload it
			else if(ModuleSave != null && 
			        ModuleSave.HasValue(SwitchableTankManager.MANAGED))	Load(ModuleSave);
			//deprecated config conversion
			if(node.HasNode(SwitchableTankType.NODE_NAME))
			{
				var tn = node.GetNode(SwitchableTankType.NODE_NAME);
				if(tn.HasValue("name")) TankType = tn.GetValue("name");
			}
		}

		//workaround for ConfigNode non-serialization
		public byte[] _module_save;
		public void OnBeforeSerialize()
		{ _module_save = ConfigNodeWrapper.SaveConfigNode(ModuleSave); }
		public void OnAfterDeserialize() 
		{ ModuleSave = ConfigNodeWrapper.RestoreConfigNode(_module_save); }

		/// <summary>
		/// Adds the given SwitchableTank to the list of all tanks 
		/// whose CurrentResource is checked upon resource switching.
		/// </summary>
		public void RegisterOtherTank(ModuleSwitchableTank tank)
		{ if(!other_tanks.Contains(tank)) other_tanks.Add(tank); }

		/// <summary>
		/// Remoes the given SwitchableTank from the list of all tanks 
		/// whose CurrentResource is checked upon resource switching.
		/// </summary>
		public bool UnregisterOtherTank(ModuleSwitchableTank tank)
		{ return other_tanks.Remove(tank); }

		/// <summary>
		/// If some resource is currently managed by the tank, checks 
		/// if its amount is zero and, if so, removes the resource from the part.
		/// </summary>
		/// <returns><c>true</c>, if resource was removed or was not present, 
		/// <c>false</c> otherwise.</returns>
		public bool TryRemoveResource()
		{
			if(current_resource == null) return true;
		   	if(current_resource.amount > 0)
			{ 
				Utils.Message("Tank is in use");
				CurrentResource = current_resource.resourceName;
				if(tank_type != null) TankType = tank_type.name;
				return false;
			}
			part.Resources.list.Remove(current_resource); 
			Destroy(current_resource);
			current_resource = null;
			return true;
		}

		void update_part_menu()
		{ 
			MonoUtilities.RefreshContextWindows(part);
			Utils.UpdateEditorGUI();
		}

		void init_tank_volume()
		{ if(Volume < 0) Volume = Metric.Volume(part); }

		void init_type_control()
		{
			if(!enable_part_controls || !ChooseTankType || 
			   SwitchableTankType.TankTypes.Count <= 1) return;
			var names = SwitchableTankType.TankTypeNames;
			var tank_types = names.ToArray();
			var tank_names = names.Select(Utils.ParseCamelCase).ToArray();
			Utils.SetupChooser(tank_names, tank_types, Fields["TankType"]);
			Utils.EnableField(Fields["TankType"]);
		}

		void init_res_control()
		{
			if(!enable_part_controls || tank_type.Resources.Count <= 1) return;
			var res_values = tank_type.ResourceNames.ToArray();
			var res_names  = tank_type.ResourceNames.Select(Utils.ParseCamelCase).ToArray();
			Utils.SetupChooser(res_names, res_values, Fields["CurrentResource"]);
			Utils.EnableField(Fields["CurrentResource"]);
		}

		void disable_part_controls()
		{
			Utils.EnableField(Fields["TankType"], false);
			Utils.EnableField(Fields["CurrentResource"], false);
		}

		bool init_tank_type()
		{
			//check if the tank is in use
			if( tank_type != null && 
				current_resource != null &&
				current_resource.amount > 0)
				{ 
					Utils.Message("Cannot change tank type while tank is in use");
					TankType = tank_type.name;
					return false;
				}
			//setup new tank type
			tank_type = null;
			//if tank type is not provided, use the first one from the library
			if(string.IsNullOrEmpty(TankType))
			{ TankType = SwitchableTankType.TankTypeNames[0]; }
			//select tank type from the library
			if(!SwitchableTankType.TankTypes.TryGetValue(TankType, out tank_type))
				Utils.Message(6, "Hangar: No \"{0}\" tank type in the library.\n" +
				              "Configuration of \"{1}\" is INVALID.", 
				              TankType, this.Title());
			//switch off the UI
			Utils.EnableField(Fields["CurrentResource"], false);
			Utils.UpdateEditorGUI();
			if(tank_type == null) return false;
			//initialize new tank UI if needed
			init_res_control();
			//initialize current resource
			if(CurrentResource == string.Empty || 
				!tank_type.Resources.ContainsKey(CurrentResource)) 
				CurrentResource = tank_type.DefaultResource.Name;
			switch_resource();
			return true;
		}

		/// <summary>
		/// Check if resource 'res' is managed by any other tank.
		/// </summary>
		/// <returns><c>true</c>, if resource is used, <c>false</c> otherwise.</returns>
		/// <param name="res">resource name</param>
		bool resource_in_use(string res)
		{
			bool in_use = false;
			foreach(var t in other_tanks)
			{
				in_use |= t.ResourceInUse == res;
				if(in_use) break;
			}
			return in_use;
		}

		[KSPEvent]
		void resource_changed(string resource)
		{
			if(current_resource != null) return;
			switch_resource(false);
		}

		bool switch_resource(bool update_menu = true)
		{
			if(tank_type == null) return false;
			//remove the old resource, if any
			if(!TryRemoveResource()) return false;
			//check if this is tank initialization or real switching
			var initializing = previous_resource == string.Empty;
			//now the state is already changed
			previous_resource = CurrentResource;
			//check if the resource is in use by another tank
			if(resource_in_use(CurrentResource)) 
			{
				Utils.Message(6, "A part cannot have more than one resource of any type");
				Fields["CurrentResource"].guiName = RES_UNMANAGED;
				if(update_menu) update_part_menu();
				return false;
			}
			Fields["CurrentResource"].guiName = RES_MANAGED;
			//get definition of the next not-managed resource
			var res = tank_type[CurrentResource];
			//calculate maxAmount (FIXME)
			var maxAmount = Volume * tank_type.UsefulVolumeRatio * res.UnitsPerLiter*1000f;
			//if there is such resource already, just plug it in
			var part_res = part.Resources[res.Name];
			if(part_res != null) 
			{ 
				current_resource = part_res;
				current_resource.maxAmount = maxAmount;
				if(current_resource.amount > current_resource.maxAmount)
					current_resource.amount = current_resource.maxAmount;
			}
			else //create the new resource
			{
				var node = new ConfigNode("RESOURCE");
				node.AddValue("name", res.Name);
				node.AddValue("amount", initializing? maxAmount*InitialAmount : 0);
				node.AddValue("maxAmount", maxAmount);
				current_resource = part.Resources.Add(node);
			}
			part.SendEvent("resource_changed");
			if(update_menu) update_part_menu();
			return true;
		}

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				if(HighLogic.LoadedSceneIsEditor)
				{
					if(tank_type == null || tank_type.name != TankType)
						init_tank_type();
				}
				else if(tank_type != null && tank_type.name != TankType)
				{
					Utils.Message("Cannot change the type of the already constructed tank");
					TankType = tank_type.name;
				}
				if(CurrentResource != previous_resource)
					switch_resource();
				yield return new WaitForSeconds(0.1f);
			}
		}
	}

	public class SwitchableTankUpdater : ModuleUpdater<ModuleSwitchableTank>
	{
		protected override void on_rescale(ModulePair<ModuleSwitchableTank> mp, Scale scale)
		{ mp.module.Volume *= scale.relative.cube * scale.relative.aspect;	}
	}

	public class SwitchableTankInfo : ConfigNodeObject
	{
		[Persistent] public float  Volume;
		[Persistent] public string TankType;
		[Persistent] public string CurrentResource;

		public SwitchableTankType Type 
		{ 
			get 
			{ 
				SwitchableTankType t;
				return SwitchableTankType.TankTypes.TryGetValue(TankType, out t) ? t : null;
			}
		}

		public float Cost 
		{ 
			get 
			{ 
				var t = Type;
				return t == null ? 0 : Volume * t.TankCostPerVolume;
			}
		}

		public static string Info(ConfigNode n)
		{
			var ti = new SwitchableTankInfo(); ti.Load(n);
			var info = " - " + ti.TankType;
			if(ti.CurrentResource != string.Empty) info += " : "+ti.CurrentResource;
			info += string.Format("\n      {0} {1:F1} Cr", Utils.formatVolume(ti.Volume), ti.Cost);
			return info+"\n";
		}
	}
}

