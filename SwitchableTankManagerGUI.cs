//   SwitchableTankManagerGUI.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using UnityEngine;

namespace AT_Utils
{
	public partial class SwitchableTankManager
	{
		public delegate float AddTankDelegate(string tank_type, float volume, bool percent);
		const int scroll_width  = 600;
		const int scroll_height = 200;
		const string eLock      = "SwitchableTankManager.EditingTanks";
		Vector2 tanks_scroll    = Vector2.zero;
		Rect eWindowPos = new Rect(Screen.width/2-scroll_width/2, scroll_height, scroll_width, scroll_height);
		DropDownList tank_types_list = new DropDownList();
		AddTankDelegate add_tank;
		Action<ModuleSwitchableTank> remove_tank;
		string volume_field = "0.0";
		string config_name = "";
		bool percent;

		class TankWrapper
		{
			SwitchableTankManager manager;
			ModuleSwitchableTank tank;

			public ModuleSwitchableTank Tank { get { return tank; } }
			public static implicit operator ModuleSwitchableTank(TankWrapper wrapper)
			{ return wrapper.tank; }

			FloatField VolumeField = new FloatField(min:0);
			bool edit;

			public TankWrapper(ModuleSwitchableTank tank, SwitchableTankManager manager) 
			{ 
				this.tank = tank; 
				this.manager = manager;
				VolumeField.Value = tank.Volume;
			}

			public void SetVolume(float vol, bool update_amount)
			{
				Tank.SetVolume(vol, update_amount);
				VolumeField.Value = Tank.Volume;
			}

			void tank_type_gui()
			{
				if(manager.TypeChangeEnabled && manager.SupportedTypes.Count > 1) 
				{
					var choice = Utils.LeftRightChooser(tank.TankType, 160);
					string new_type = null;
					if(choice < 0) new_type = tank.SupportedTypes.Prev(tank.TankType);
					else if(choice > 0) new_type = tank.SupportedTypes.Next(tank.TankType);
					if(!string.IsNullOrEmpty(new_type))
					{
						tank.TankType = new_type;
						manager.update_symmetry_tanks(tank, t => t.TankType = tank.TankType);
					}
				}
				else GUILayout.Label(tank.TankType, Styles.boxed_label, GUILayout.Width(170));
			}

			void tank_resource_gui()
			{
				if(tank.Type.Resources.Count > 1)
				{
					var choice = Utils.LeftRightChooser(tank.CurrentResource, 160);
					TankResource new_res = null;
					if(choice < 0) new_res = tank.Type.Resources.Prev(tank.CurrentResource);
					else if(choice > 0) new_res = tank.Type.Resources.Next(tank.CurrentResource);
					if(new_res != null) 
					{
						tank.CurrentResource = new_res.Name;
						manager.update_symmetry_tanks(tank, t => t.CurrentResource = tank.CurrentResource);
					}
				}
				else GUILayout.Label(tank.CurrentResource, Styles.boxed_label, GUILayout.Width(170));
			}

			public void ManageGUI()
			{
				GUILayout.BeginHorizontal();
				tank_type_gui();
				tank_resource_gui();
				GUILayout.FlexibleSpace();
				if(HighLogic.LoadedSceneIsEditor && manager.Volume > 0)
				{
					if(edit)
					{
						if(VolumeField.Draw("m3", true, manager.Volume/20, "F2"))
						{
							var max_volume = tank.Volume+manager.Volume-manager.TotalVolume;
							if(VolumeField.Value > max_volume) 
								VolumeField.Value = max_volume;
							if(VolumeField.IsSet)
							{
								tank.Volume = VolumeField.Value;
								tank.UpdateMaxAmount(true);
								manager.total_volume = -1;
								edit = false;
							}
						}
					}
					else edit |= GUILayout.Button(new GUIContent(Utils.formatVolume(tank.Volume), "Edit tank volume"), 
					                              Styles.add_button, GUILayout.ExpandWidth(true));
				}
				else GUILayout.Label(Utils.formatVolume(tank.Volume), Styles.boxed_label, GUILayout.ExpandWidth(true));
				if(!edit)
				{
					var usage = tank.Usage;
					GUILayout.Label("Filled: "+usage.ToString("P1"), Styles.fracStyle(usage), GUILayout.Width(95));
					if(HighLogic.LoadedSceneIsEditor)
					{
						if(GUILayout.Button(new GUIContent("F", "Fill the tank with the resource"),
						                    Styles.add_button, GUILayout.Width(20)))
							tank.Amount = tank.MaxAmount;
						if(GUILayout.Button(new GUIContent("E", "Empty the tank"), 
						                    Styles.active_button, GUILayout.Width(20)))
							tank.Amount = 0;
					}
					if(manager.AddRemoveEnabled)
					{
						if(GUILayout.Button(new GUIContent("X", "Delete the tank"), 
						                    Styles.danger_button, GUILayout.Width(20)))
						{
							if(HighLogic.LoadedSceneIsEditor) tank.Amount = 0;
							manager.remove_tank(tank);
							manager.part.UpdatePartMenu();
						}
					}
				}
				GUILayout.EndHorizontal();
			}

			public void ControlGUI()
			{
				GUILayout.BeginHorizontal();
				tank_resource_gui();
				GUILayout.FlexibleSpace();
				GUILayout.Label(Utils.formatVolume(tank.Volume), Styles.boxed_label, GUILayout.ExpandWidth(true));
				var usage = tank.Usage;
				GUILayout.Label("Filled: "+usage.ToString("P1"), Styles.fracStyle(usage), GUILayout.Width(95));
				GUILayout.EndHorizontal();
			}
		}

		public bool Closed { get; private set; }

		void close_button()
		{ 	
			Closed = GUILayout.Button("Close", Styles.normal_button, GUILayout.ExpandWidth(true));
			if(Closed) Utils.LockIfMouseOver(eLock, eWindowPos, false);
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
			if(GUILayout.Button(new GUIContent(percent? "%" : "m3", "Change between Volume (m3) and Percentage (%)"), 
			                    Styles.normal_button, GUILayout.Width(30))) 
				percent = !percent;
			float volume = -1;
			var volume_valid = float.TryParse(volume_field, out volume);
			if(volume_valid)
			{
				var vol = add_tank(tank_type, volume, percent);
				if(!vol.Equals(volume))
				{
					volume = vol;
					volume_field = volume.ToString();
				}
			}
			GUILayout.EndHorizontal();
			//warning label
			GUILayout.BeginHorizontal();
			if(!volume_valid) GUILayout.Label("Volume should be a number.", Styles.red);
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
			var choice = Utils.LeftRightChooser(config_name, 200);
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
			tanks.ForEach(t => t.ManageGUI());
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
			tanks.ForEach(t => t.ControlGUI());
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
		                                   AddTankDelegate add_tank, 
		                                   Action<ModuleSwitchableTank> remove_tank)
		{
			this.add_tank = add_tank;
			this.remove_tank = remove_tank;
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
	}
}

