//   SwitchableTankManager.cs
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
	public interface ITankManager { SwitchableTankManager GetTankManager(); }

	public class SwitchableTankManager : ConfigNodeObject
	{
		new public const string NODE_NAME = "TANKMANAGER";
		public const string MANAGED = "MANAGED";

		readonly Part part;
		readonly PartModule host;
		readonly List<ModuleSwitchableTank> tanks = new List<ModuleSwitchableTank>();
		int max_id = -1;

		/// <summary>
		/// Maximum total volume of all tanks in m^3. It is used for reference and in tank controls.
		/// </summary>
		public float Volume = -1;

		/// <summary>
		/// If true, tanks may be added and removed.
		/// </summary>
		[Persistent] public bool AddRemoveEnabled = true;

		/// <summary>
		/// If true, type of tanks may be changed.
		/// </summary>
		[Persistent] public bool TypeChangeEnabled = true;

		/// <summary>
		/// Excluded tank types. If empty, all types are supported.
		/// </summary>
		[Persistent] public string ExcludeTankTypes = string.Empty;
		string[] exclude;

		/// <summary>
		/// Supported tank types. If empty, all types are supported. Overrides ExcludedTankTypes.
		/// </summary>
		[Persistent] public string IncludeTankTypes = string.Empty;
		string[] include;

		public List<string> SupportedTypes = new List<string>();

		bool enable_part_controls;
		public bool EnablePartControls
		{
			get { return enable_part_controls; }
			set 
			{ 
				enable_part_controls = value;
				tanks.ForEach(t => t.EnablePartControls = enable_part_controls);
			}
		}

		public ModuleSwitchableTank GetTank(int id) { return tanks.Find(t => t.id == id); }
		public int TanksCount { get { return tanks.Count; } }
		public float TotalCost { get { return tanks.Aggregate(0f, (c, t) => c+t.Cost); } }
		public IEnumerable<float> TanksVolumes { get { return tanks.Select(t => t.Volume); } }
		public float TotalVolume 
		{ 
			get 
			{ 
				if(total_volume < 0)
					total_volume = tanks.Aggregate(0f, (v, t) => v+t.Volume); 
				return total_volume;
			} 
		}
		float total_volume = -1;


		public static string GetInfo(PartModule host, ConfigNode node)
		{
			var mgr = new SwitchableTankManager(host);
			return mgr.GetInfo(node);
		}

		public string GetInfo(ConfigNode node)
		{
			base.Load(node);
			var info = "";
			if(TypeChangeEnabled) 
				info += SwitchableTankType.TypesInfo(include, exclude);
			var volumes = ConfigNodeObject.FromConfig<VolumeConfiguration>(node);
			if(volumes.Valid)
				info = string.Concat(info, "Preconfigured Tanks:\n", volumes.Info());
			return info;
		}

		public SwitchableTankManager(PartModule host) 
		{ 
			part = host.part;
			this.host = host;
			tank_types_list.Items = SwitchableTankType.TankTypeNames();
		}

		void init_supported_types()
		{
			exclude = Utils.ParseLine(ExcludeTankTypes, Utils.Comma);
			include = Utils.ParseLine(IncludeTankTypes, Utils.Comma);
			SupportedTypes = SwitchableTankType.TankTypeNames(include, exclude);
			SupportedTypes.AddRange(VolumeConfigsLibrary.AllConfigNames(include, exclude));
			tank_types_list.Items = SupportedTypes;
		}

		public override void Save(ConfigNode node)
		{
			base.Save(node);
			if(tanks.Count == 0) return;
			tanks.ForEach(t => t.Save(node.AddNode(TankVolume.NODE_NAME)));
			node.AddValue(MANAGED, true);
		}

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			tanks.Clear();
			total_volume = -1;
			init_supported_types();
			if(node.HasValue(SwitchableTankManager.MANAGED))
			{
				var existing_tanks = part.Modules.GetModules<ModuleSwitchableTank>();
				foreach(var n in node.GetNodes(TankVolume.NODE_NAME))
				{
					n.AddValue(MANAGED, true);
					n.AddValue("name", typeof(ModuleSwitchableTank).Name);
					n.SetValue("ExcludeTankTypes", ExcludeTankTypes);
					n.SetValue("IncludeTankTypes", IncludeTankTypes);
					ModuleSwitchableTank tank;
					var id = n.HasValue("id")? int.Parse(n.GetValue("id")) : -1;
					if(id >= 0 && (tank = existing_tanks.Find(t => t.id == id)) != null)
					{ tank.Load(n); max_id = Mathf.Max(max_id, id); }
					else 
					{
						tank = part.AddModule(n) as ModuleSwitchableTank;
						tank.id = ++max_id;
					}
					if(tank != null) 
					{
						tank.EnablePartControls = EnablePartControls;
						tanks.Add(tank);
					}
					else Utils.Log("SwitchableTankManager: unable to load module from config:\n{}", n);
				}
				tanks.ForEach(t => t.OnStart(part.StartState()));
			}
			else 
			{
				var cfg = ConfigNodeObject.FromConfig<VolumeConfiguration>(node);
				AddConfiguration(cfg, cfg.Volume);
			}
		}

		/// <summary>
		/// Adds a tank of the provided type and value to the part, if possible.
		/// </summary>
		/// <returns><c>true</c>, if tank was added, <c>false</c> otherwise.</returns>
		/// <param name="tank_type">Tank type.</param>
		/// <param name="volume">Tank volume.</param>
		/// <param name="resource">Current resource name.</param>
		/// <param name="amount">Initial amount of a resource in the tank: [0, 1]</param>
		/// <param name="update_counterparts">If counterparts are to be updated.</param>
		public bool AddTank(string tank_type, float volume, string resource = "", float amount = 0, bool update_counterparts = true)
		{
			if(!AddRemoveEnabled) return false;
			if(!SwitchableTankType.HaveTankType(tank_type))
			{
				Utils.Log("SwitchableTankManager: no such tank type: {}", tank_type);
				return false;
			}
			var tank = part.AddModule(typeof(ModuleSwitchableTank).Name) as ModuleSwitchableTank;
			if(tank == null) return false;
			tank.id = ++max_id;
			tank.Volume = volume;
			tank.TankType = tank_type;
			tank.EnablePartControls = EnablePartControls;
			tank.IncludeTankTypes = IncludeTankTypes;
			tank.ExcludeTankTypes = ExcludeTankTypes;
			tank.InitialAmount = Mathf.Clamp01(amount);
			if(!string.IsNullOrEmpty(resource)) tank.CurrentResource = resource;
			tank.OnStart(part.StartState());
			tanks.ForEach(t => t.RegisterOtherTank(tank));
			tanks.Add(tank);
			total_volume = -1;
			if(update_counterparts)
				update_symmetry_managers(m => m.AddTank(tank_type, volume, resource, amount, false));
			return true;
		}

		/// <summary>
		/// Adds tanks accodring to the configuration.
		/// </summary>
		/// <returns><c>true</c>, if configuration was added, <c>false</c> otherwise.</returns>
		/// <param name="cfg">Predefined configuration of tanks.</param>
		/// <param name="volume">Total volume of the configuration.</param>
		/// <param name="update_counterparts">If counterparts are to be updated.</param>
		public bool AddConfiguration(VolumeConfiguration cfg, float volume, bool update_counterparts = true)
		{
			if(!AddRemoveEnabled || !cfg.Valid) return false;
			var V = cfg.TotalVolume;
			foreach(var v in cfg.Volumes)
			{
				var t = v as TankVolume;
				if(t != null) 
				{ 
					AddTank(t.TankType, volume*v.Volume/V, t.CurrentResource, t.InitialAmount, update_counterparts);
					continue;
				}
				var c = v as VolumeConfiguration;
				if(c != null)
				{
					AddConfiguration(c, volume*v.Volume/V, update_counterparts);
					continue;
				}
			}
			return true;
		}

		/// <summary>
		/// Searches for a named tank type or configuration and adds tanks accordingly.
		/// </summary>
		/// <returns><c>true</c>, if configuration was added, <c>false</c> otherwise.</returns>
		/// <param name="name">A name of a tank type or tank configuration.</param>
		/// <param name="volume">Total volume of the configuration.</param>
		/// <param name="update_counterparts">If counterparts are to be updated.</param>
		public bool AddVolume(string name, float volume, bool update_counterparts = true)
		{
			if(!AddRemoveEnabled) return false;
			if(AddTank(name, volume, update_counterparts:update_counterparts)) return true;
			var cfg = VolumeConfigsLibrary.GetConfig(name);
			if(cfg == null)
			{
				Utils.Log("SwitchableTankManager: no such tank configuration: {}", name);
				return false;
			}
			return AddConfiguration(cfg, volume, update_counterparts);
		}

		/// <summary>
		/// Removes the tank from the part, if possible. Removed tank is destroyed immidiately, 
		/// so the provided reference becomes invalid.
		/// </summary>
		/// <returns><c>true</c>, if tank was removed, <c>false</c> otherwise.</returns>
		/// <param name="tank">Tank to be removed.</param>
		/// <param name="update_counterparts">If counterparts are to be updated.</param>
		public bool RemoveTank(ModuleSwitchableTank tank, bool update_counterparts = true)
		{
			if(!AddRemoveEnabled) return false;
			if(!tanks.Contains(tank)) return false;
			if(!tank.TryRemoveResource()) return false;
			tanks.Remove(tank);
			tanks.ForEach(t => t.UnregisterOtherTank(tank));
			part.RemoveModule(tank);
			total_volume = -1;
			if(update_counterparts)
				update_symmetry_managers(m => m.RemoveTank(m.GetTank(tank.id), false));
			return true;
		}

		/// <summary>
		/// Multiplies the Volume property of each tank by specified value.
		/// Amounts of resources are not rescaled.
		/// </summary>
		/// <param name="relative_scale">Relative scale. Should be in [0, +inf] interval.</param>
		public void RescaleTanks(float relative_scale)
		{
			if(relative_scale <= 0) return;
			tanks.ForEach(t =>  t.Volume *= relative_scale);
			total_volume = -1;
		}

		void update_symmetry_managers(Action<SwitchableTankManager> action)
		{
			if(part.symmetryCounterparts.Count == 0) return;
			var ind = part.Modules.IndexOf(host);
			foreach(var p in part.symmetryCounterparts)
			{
				var manager_module = p.Modules.GetModule(ind) as ITankManager;
				if(manager_module == null)
				{
					Utils.Log("SwitchableTankManager: counterparts should have ITankManager module at {} position, " +
					          "but {} does not. This should never happen!", ind, p);
					continue;
				}
				var manager = manager_module.GetTankManager();
				if(manager == null)
				{
					Utils.Log("SwitchableTankManager: WARNING, trying to update " +
					          "ITankManager {} with uninitialized SwitchableTankManager", manager_module);
					continue;
				}
				action(manager);
			}
		}

		void update_symmetry_tanks(ModuleSwitchableTank tank, Action<ModuleSwitchableTank> action)
		{
			update_symmetry_managers
			(m => 
			{
				var tank1 = m.GetTank(tank.id);
				if(tank1 == null)
					Utils.Log("SwitchableTankManager: WARNING, no tank with {} id", tank.id);
				else action(tank1);
			});
		}

		#region GUI
		const int scroll_width  = 600;
		const int scroll_height = 200;
		const string eLock      = "SwitchableTankManager.EditingTanks";
		Vector2 tanks_scroll    = Vector2.zero;
		Rect eWindowPos = new Rect(Screen.width/2-scroll_width/2, scroll_height, scroll_width, scroll_height);
		DropDownList tank_types_list = new DropDownList();
		Func<string, float, float> add_tank_delegate;
		Action<ModuleSwitchableTank> remove_tank_delegate;
		string volume_field = "0.0";
		string config_name = "";

		public bool Closed { get; private set; }

		void close_button()
		{ 	
			Closed = GUILayout.Button("Close", Styles.normal_button, GUILayout.ExpandWidth(true));
			if(Closed) Utils.LockIfMouseOver(eLock, eWindowPos, false);
		}

		void tank_type_gui(ModuleSwitchableTank tank)
		{
			if(TypeChangeEnabled && SupportedTypes.Count > 1) 
			{
				var choice = Utils.LeftRightChooser(tank.TankType, 120);
				string new_type = null;
				if(choice < 0) new_type = tank.SupportedTypes.Prev(tank.TankType);
				else if(choice > 0) new_type = tank.SupportedTypes.Next(tank.TankType);
				if(!string.IsNullOrEmpty(new_type))
				{
					tank.TankType = new_type;
					update_symmetry_tanks(tank, t => t.TankType = tank.TankType);
				}
			}
			else GUILayout.Label(tank.TankType, Styles.boxed_label, GUILayout.Width(120));
		}

		void tank_resource_gui(ModuleSwitchableTank tank)
		{
			if(tank.Type.Resources.Count > 1)
			{
				var choice = Utils.LeftRightChooser(tank.CurrentResource, 120);
				TankResource new_res = null;
				if(choice < 0) new_res = tank.Type.Resources.Prev(tank.CurrentResource);
				else if(choice > 0) new_res = tank.Type.Resources.Next(tank.CurrentResource);
				if(new_res != null) 
				{
					tank.CurrentResource = new_res.Name;
					update_symmetry_tanks(tank, t => t.CurrentResource = tank.CurrentResource);
				}
			}
			else GUILayout.Label(tank.CurrentResource, Styles.boxed_label, GUILayout.Width(120));
		}

		void tank_management_gui(ModuleSwitchableTank tank)
		{
			GUILayout.BeginHorizontal();
			tank_type_gui(tank);
			tank_resource_gui(tank);
			GUILayout.FlexibleSpace();
			if(HighLogic.LoadedSceneIsEditor && Volume > TotalVolume)
			{
				if(GUILayout.Button(new GUIContent(Utils.formatVolume(tank.Volume), 
				                                   "Expand the tank to fill the remaining volume"), 
				                    Styles.add_button, GUILayout.ExpandWidth(true)))
				{
					tank.Volume += Volume-TotalVolume;
					tank.UpdateMaxAmount();
					total_volume = -1;
				}
			}
			else GUILayout.Label(Utils.formatVolume(tank.Volume), Styles.boxed_label, GUILayout.ExpandWidth(true));
			var usage = tank.Usage;
			GUILayout.Label("Filled: "+usage.ToString("P1"), Styles.fracStyle(usage), GUILayout.Width(95));
			if(HighLogic.LoadedSceneIsEditor)
			{
				if(GUILayout.Button(new GUIContent("F", "Fill the tank with the resource"),
				                    Styles.add_button, GUILayout.Width(20)) && tank.Resource != null)
					tank.Resource.amount = tank.Resource.maxAmount;
				if(GUILayout.Button(new GUIContent("E", "Empty the tank"), 
				                    Styles.active_button, GUILayout.Width(20)) && tank.Resource != null)
					tank.Resource.amount = 0;
			}
			if(AddRemoveEnabled)
			{
				if(GUILayout.Button(new GUIContent("X", "Delete the tank"), 
				                    Styles.danger_button, GUILayout.Width(20)))
				{
					if(HighLogic.LoadedSceneIsEditor) tank.Resource.amount = 0;
					remove_tank_delegate(tank);
				}
			}
			GUILayout.EndHorizontal();
		}

		void tank_control_gui(ModuleSwitchableTank tank)
		{
			GUILayout.BeginHorizontal();
			tank_resource_gui(tank);
			GUILayout.FlexibleSpace();
			GUILayout.Label(Utils.formatVolume(tank.Volume), Styles.boxed_label, GUILayout.ExpandWidth(true));
			var usage = tank.Usage;
			GUILayout.Label("Filled: "+usage.ToString("P1"), Styles.fracStyle(usage), GUILayout.Width(95));
			GUILayout.EndHorizontal();
		}

		void add_tank_gui_start(Rect windowPos)
		{
			if(!AddRemoveEnabled) return;
			tank_types_list.styleListBox  = Styles.list_box;
			tank_types_list.styleListItem = Styles.list_item;
			tank_types_list.windowRect    = windowPos;
			tank_types_list.DrawBlockingSelector();
		}

		void add_tank_gui()
		{
			if(!AddRemoveEnabled) return;
			GUILayout.BeginVertical();
			//tank properties
			GUILayout.BeginHorizontal();
			GUILayout.Label("Tank Type:", GUILayout.Width(70));
			tank_types_list.DrawButton();
			var tank_type = tank_types_list.SelectedValue;
			GUILayout.Label("Volume:", GUILayout.Width(50));
			volume_field = GUILayout.TextField(volume_field, GUILayout.ExpandWidth(true), GUILayout.MinWidth(50));
			GUILayout.Label("m3", GUILayout.Width(20));
			float volume = -1;
			var volume_valid = float.TryParse(volume_field, out volume); 
			volume = add_tank_delegate(tank_type, volume);
			GUILayout.EndHorizontal();
			//warning label
			GUILayout.BeginHorizontal();
			if(volume_valid) volume_field = volume.ToString("n3");
			else GUILayout.Label("Volume should be a number.", Styles.red);
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}

		void add_tank_gui_end()
		{
			if(!AddRemoveEnabled) return;
			tank_types_list.DrawDropDown();
			tank_types_list.CloseOnOutsideClick();
		}

		void volume_configs_gui()
		{
			if(!AddRemoveEnabled) return;
			GUILayout.BeginHorizontal();
			VolumeConfiguration cfg = null;
			GUILayout.Label("Configuration Name:", GUILayout.ExpandWidth(false));
			config_name = GUILayout.TextField(config_name, GUILayout.ExpandWidth(true), GUILayout.MinWidth(50));
			if(GUILayout.Button(VolumeConfigsLibrary.HaveUserConfig(config_name)? "Save" : "Add", 
			                    Styles.add_button, GUILayout.ExpandWidth(false)) && 
			   !string.IsNullOrEmpty(config_name))
			{
				//add new config
				var node = new ConfigNode();
				Save(node);
				cfg = ConfigNodeObject.FromConfig<VolumeConfiguration>(node);
				if(cfg.Valid) 
				{
					cfg.name = config_name;
					cfg.Volume = TotalVolume;
					VolumeConfigsLibrary.AddOrSave(cfg);
					init_supported_types();
				}
				else Utils.Log("Configuration is invalid:\n{}\nThis should never happen!", node);
			}
			cfg = null;
			var choice = Utils.LeftRightChooser(config_name, 120);
			if(choice < 0) cfg = VolumeConfigsLibrary.UserConfigs.Prev(config_name);
			else if(choice > 0) cfg = VolumeConfigsLibrary.UserConfigs.Next(config_name);
			if(cfg != null) config_name = cfg.name;
			if(GUILayout.Button("Delete", Styles.danger_button, GUILayout.ExpandWidth(false)) && 
			   !string.IsNullOrEmpty(config_name))
			{
				//remove config
				if(VolumeConfigsLibrary.RemoveConfig(config_name))
					init_supported_types();
				config_name = "";
			}
			GUILayout.EndHorizontal();
		}

		public void TanksManagerGUI(int windowId)
		{
			add_tank_gui_start(eWindowPos);
			GUILayout.BeginVertical();
			add_tank_gui();
			tanks_scroll = GUILayout.BeginScrollView(tanks_scroll, 
				GUILayout.Width(scroll_width), 
				GUILayout.Height(scroll_height));
			GUILayout.BeginVertical();
			tanks.ForEach(tank_management_gui);
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
			volume_configs_gui();
			close_button();
			GUILayout.EndVertical();
			add_tank_gui_end();
			GUIWindowBase.TooltipsAndDragWindow(eWindowPos);
		}

		public void TanksControlGUI(int windowId)
		{
			GUILayout.BeginVertical();
			tanks_scroll = GUILayout.BeginScrollView(tanks_scroll, 
			                                         GUILayout.Width(scroll_width), 
			                                         GUILayout.Height(scroll_height));
			GUILayout.BeginVertical();
			tanks.ForEach(tank_control_gui);
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
			close_button();
			GUILayout.EndVertical();
			GUIWindowBase.TooltipsAndDragWindow(eWindowPos);
		}

		/// <summary>
		/// Draws the tank manager GUI in a separate window.
		/// </summary>
		/// <returns>New window position.</returns>
		/// <param name="windowId">Window ID.</param>
		/// <param name="title">Window title.</param>
		/// <param name="add_tank">This function should take selected tank type and value, 
		/// check them and if appropriate add new tank by calling AddTank method.</param>
		/// <param name="remove_tank">This function should take selected tank 
		/// and if possible remove it using RemoveTank method.</param>
		public void DrawTanksManagerWindow(int windowId, string title, 
		                                   Func<string, float, float> add_tank, 
		                                   Action<ModuleSwitchableTank> remove_tank)
		{
			add_tank_delegate = add_tank;
			remove_tank_delegate = remove_tank;
			Utils.LockIfMouseOver(eLock, eWindowPos, !Closed);
			eWindowPos = GUILayout.Window(windowId, 
			                              eWindowPos, TanksManagerGUI, title,
			                              GUILayout.Width(scroll_width),
			                              GUILayout.Height(scroll_height)).clampToScreen();
		}

		/// <summary>
		/// Draws the tanks control GUI in a separate window.
		/// </summary>
		/// <returns>New window position.</returns>
		/// <param name="windowId">Window ID.</param>
		/// <param name="title">Window title.</param>
		public void DrawTanksControlWindow(int windowId, string title)
		{
			Utils.LockIfMouseOver(eLock, eWindowPos, !Closed);
			eWindowPos = GUILayout.Window(windowId, 
			                              eWindowPos, TanksControlGUI, title,
			                              GUILayout.Width(scroll_width),
			                              GUILayout.Height(scroll_height)).clampToScreen();
		}

		public void UnlockEditor()
		{ Utils.LockIfMouseOver(eLock, eWindowPos, false); }
		#endregion
	}
}

