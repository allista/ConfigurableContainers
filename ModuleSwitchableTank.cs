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
	public class ModuleSwitchableTank : AbstractResourceTank
	{
		const string   RES_MANAGED = "Res";
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
				init_res_control();
			}
		}

		[KSPField(isPersistant = true)] public int id = -1;

		/// <summary>
		/// If a tank type can be selected in editor.
		/// </summary>
		[KSPField] public bool ChooseTankType;

		/// <summary>
		/// Excluded tank types. If empty, all types are supported.
		/// </summary>
		[KSPField] public string ExcludeTankTypes = string.Empty;
		string[] exclude;

		/// <summary>
		/// Supported tank types. If empty, all types are supported. Overrides ExcludedTankTypes.
		/// </summary>
		[KSPField] public string IncludeTankTypes = string.Empty;
		string[] include;

		/// <summary>
		/// The type of the tank. Types are defined in separate config nodes. Cannot be changed in flight.
		/// </summary>
		[KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Type")]
		[UI_ChooseOption(scene = UI_Scene.Editor)]
		public string TankType;
		SwitchableTankType tank_type;
		public SwitchableTankType Type { get { return tank_type; } }
		public List<string> SupportedTypes = new List<string>();

		/// <summary>
		/// Cost of an empty tank of current type and volume
		/// </summary>
		public float Cost { get { return tank_type != null? Utils.CubeSurface(Volume)*tank_type.TankCostPerSurface : 0; } }

		/// <summary>
		/// The initial partial amount of the CurrentResource.
		/// Should be in the [0, 1] interval.
		/// </summary>
		[KSPField(isPersistant = true)] public float InitialAmount;

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
		public PartResource Resource { get { return current_resource; } }

		readonly List<ModuleSwitchableTank> other_tanks = new List<ModuleSwitchableTank>();

		public override string GetInfo()
		{
			var info = "";
			init_supported_types();
			if(ChooseTankType) 
				info += SwitchableTankType.TypesInfo(include, exclude);
			if(!init_tank_type()) return info;
			info += tank_type.Info;
			info += "Tank Volume: " + Utils.formatVolume(Volume);
			return info;
		}

		protected override float TankCost(float defaultCost)
		{ 
			if(tank_type == null && !init_tank_type()) return 0;
			return Cost;
		}

		protected override float ResourcesCost(bool maxAmount = true)
		{
			var cost = 0f;
			if(current_resource != null)
				cost = (float)current_resource.maxAmount*current_resource.info.unitCost;
			else
			{
				if(tank_type == null && !init_tank_type()) return 0;
				var res = tank_type[CurrentResource];
				if(res == null) return 0;
				cost = Volume * tank_type.UsefulVolumeRatio * res.UnitsPerLiter*1000f * res.Resource.unitCost;
			}
			return maxAmount? cost : cost * InitialAmount;
		}

		void OnDestroy() { Utils.UpdateEditorGUI(); }

		void init_supported_types()
		{
			include = Utils.ParseLine(IncludeTankTypes, Utils.Comma);
			exclude = Utils.ParseLine(ExcludeTankTypes, Utils.Comma);
			SupportedTypes = SwitchableTankType.TankTypeNames(include, exclude);
		}

		public override void OnStart(StartState state)
		{
			init_supported_types();
			//get other tanks in this part
			other_tanks.AddRange(from t in part.Modules.GetModules<ModuleSwitchableTank>()
								 where t != this select t);
			//initialize tank type chooser
			disable_part_controls();
			if(state == StartState.Editor) 
				init_type_control();
			init_tank_type();
			init_resource();
			init_res_control();
			StartCoroutine(slow_update());
		}

		public override void OnLoad(ConfigNode node)
		{
			//if the tank is managed, save its config
			if(node.HasValue(SwitchableTankManager.MANAGED)) ModuleSave = node;
			//if the node is not from a TankManager, but we have a saved config, reload it
			else if(ModuleSave != null && 
			        ModuleSave.HasValue(SwitchableTankManager.MANAGED))	
			{ Load(ModuleSave); return; }
			//deprecated config conversion
			if(node.HasNode(SwitchableTankType.NODE_NAME))
			{
				var tn = node.GetNode(SwitchableTankType.NODE_NAME);
				if(tn.HasValue("name")) TankType = tn.GetValue("name");
			}
		}

		public override void OnSave(ConfigNode node)
		{
			if(current_resource != null)
				InitialAmount = (float)(current_resource.amount/current_resource.maxAmount);
			base.OnSave(node);
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

		void init_type_control()
		{
			if(!enable_part_controls || !ChooseTankType || SupportedTypes.Count <= 1) return;
			var tank_names = SupportedTypes.Select(Utils.ParseCamelCase).ToArray();
			Utils.SetupChooser(tank_names, SupportedTypes.ToArray(), Fields["TankType"]);
			Utils.EnableField(Fields["TankType"]);
		}

		void init_res_control()
		{
			if(tank_type == null || !enable_part_controls || tank_type.Resources.Count <= 1) 
				Utils.EnableField(Fields["CurrentResource"], false);
			else
			{
				var res_values = tank_type.ResourceNames.ToArray();
				var res_names  = tank_type.ResourceNames.Select(Utils.ParseCamelCase).ToArray();
				Utils.SetupChooser(res_names, res_values, Fields["CurrentResource"]);
				Utils.EnableField(Fields["CurrentResource"]);
			}
			update_part_menu();
			Utils.UpdateEditorGUI();
		}

		void update_res_control()
		{
			Fields["CurrentResource"].guiName = current_resource == null ? RES_UNMANAGED : RES_MANAGED;
			update_part_menu();
			Utils.UpdateEditorGUI();
		}

		void disable_part_controls()
		{
			Utils.EnableField(Fields["TankType"], false);
			Utils.EnableField(Fields["CurrentResource"], false);
		}

		bool init_tank_type()
		{
			if(Volume < 0) Volume = Metric.Volume(part);
			if(tank_type != null) return true;
			//if tank type is not provided, use the first one from the library
			if(string.IsNullOrEmpty(TankType))
			{ TankType = SwitchableTankType.TankTypeNames(include, exclude)[0]; }
			//select tank type from the library
			if(!SwitchableTankType.TankTypes.TryGetValue(TankType, out tank_type))
				Utils.Message(6, "No \"{0}\" tank type in the library.\n" +
				              "Configuration of \"{1}\" is INVALID.", 
				              TankType, this.Title());
			if(tank_type == null) return false;
			//initialize current resource
			if(CurrentResource == string.Empty || 
			   !tank_type.Resources.ContainsKey(CurrentResource)) 
				CurrentResource = tank_type.DefaultResource.Name;
			return true;
		}

		void change_tank_type()
		{
			//check if the tank is in use
			if(tank_type != null && 
			   current_resource != null &&
			   current_resource.amount > 0)
			{ 
				Utils.Message("Cannot change tank type while tank is in use");
				TankType = tank_type.name;
			}
			//setup new tank type
			tank_type = null;
			init_tank_type();
			switch_resource();
			init_res_control();
		}

		/// <summary>
		/// Check if the resource 'res' is managed by any other tank.
		/// </summary>
		/// <returns><c>true</c>, if resource is used, <c>false</c> otherwise.</returns>
		/// <param name="res">resource name</param>
		bool resource_in_use(string res)
		{ return other_tanks.Any(t => t.ResourceInUse == res); }

		bool init_resource()
		{
			if(current_resource != null)
			{
				CurrentResource = current_resource.resourceName;
				return false;
			}
			if(tank_type == null) return false;
			//check if this is tank initialization or switching
			var initializing = previous_resource == string.Empty;
			previous_resource = CurrentResource;
			//check if the resource is in use by another tank
			if(resource_in_use(CurrentResource)) 
			{
				Utils.Message(6, "A part cannot have more than one resource of any type");
				return false;
			}
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
			if(part.Events != null) part.SendEvent("resource_changed");
			return true;
		}

		bool switch_resource()
		{ return TryRemoveResource() && init_resource(); }

		[KSPEvent]
		void resource_changed()
		{
			if(current_resource != null) return;
			switch_resource();
		}

		IEnumerator<YieldInstruction> slow_update()
		{
			while(true)
			{
				if(HighLogic.LoadedSceneIsEditor)
				{
					if(tank_type == null || tank_type.name != TankType) 
						change_tank_type();
				}
				else if(tank_type != null && tank_type.name != TankType)
				{
					Utils.Message("Cannot change the type of the already constructed tank");
					TankType = tank_type.name;
				}
				if(CurrentResource != previous_resource)
				{ switch_resource(); update_res_control(); }
				yield return new WaitForSeconds(0.1f);
			}
		}
	}

	public class SwitchableTankUpdater : ModuleUpdater<ModuleSwitchableTank>
	{
		protected override void on_rescale(ModulePair<ModuleSwitchableTank> mp, Scale scale)
		{ mp.module.Volume *= scale.relative.cube * scale.relative.aspect;	}
	}
}

