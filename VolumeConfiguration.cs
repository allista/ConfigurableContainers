//   VolumeConfiguration.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;
using AT_Utils;
using UnityEngine;

namespace AT_Utils
{
	public class BaseVolume : ConfigNodeObject
	{ 
		new public const string NODE_NAME = "VOLUME";
		[Persistent] public float Volume;

		public virtual string Info(float volume_conversion = 1)
		{ return string.Format("{0}\n", Utils.formatVolume(Volume*volume_conversion)); }

		public virtual float Cost(float volume_conversion = 1) { return 0f; }

		public virtual float ResourceCost(bool maxAmount = true, float volume_conversion = 1) { return 0f; }

		public virtual bool ContainsType(string tank_type)
		{ return false; }
	}

	public class TankVolume : BaseVolume
	{
		new public const string NODE_NAME = "TANK";

		[Persistent] public string TankType;
		[Persistent] public string CurrentResource;
		[Persistent] public float  InitialAmount;

		public SwitchableTankType Type 
		{ get { return SwitchableTankType.GetTankType(TankType); } }

		public override float Cost(float volume_conversion = 1)
		{ 
			var t = Type;
			return t == null ? 0 : Utils.CubeSurface(Volume) * t.TankCostPerSurface;
		}

		public override float ResourceCost(bool maxAmount = true, float volume_conversion = 1) 
		{ 
			try
			{
				var t = Type;
				var res = t.Resources[CurrentResource];
				var res_def = PartResourceLibrary.Instance.GetDefinition(res.Name);
				var cost = res_def.unitCost * res.UnitsPerLiter * t.UsefulVolumeRatio * Volume * volume_conversion * 1000;
				return maxAmount? cost : cost * InitialAmount;
			}
			catch { return 0; }
		}

		public override string Info(float volume_conversion = 1)
		{
			var info = " - " + TankType;
			if(!string.IsNullOrEmpty(CurrentResource)) 
				info += " : "+CurrentResource;
			info += string.Format("\n   {0} {1:F1}", 
			                      Utils.formatVolume(Volume*volume_conversion), 
			                      Cost(volume_conversion));
			if(InitialAmount > 0)
				info += string.Format("+{0:F1}", ResourceCost(false, volume_conversion));
			info += " Cr";
			return info+"\n";
		}

		public override bool ContainsType(string tank_type)
		{ return TankType == tank_type; }
	}

	public class VolumeConfiguration : BaseVolume
	{
		new public const string NODE_NAME = "TANKCONF";

		/// <summary>
		/// The name of a configuration.
		/// </summary>
		[Persistent] public string name = "";

		public List<BaseVolume> Volumes = new List<BaseVolume>();
		public float TotalVolume { get { return Volumes.Aggregate(0f, (v, t) => v+t.Volume); } }
		public bool Valid { get { return Volumes.Count > 0 && TotalVolume > 0; } }

		public override void Load(ConfigNode node)
		{
			Volumes.Clear();
			base.Load(node);
			var volumes = node.GetNodes();
			for(int i = 0; i < volumes.Length; i++)
			{
				var v = volumes[i];
				if(v.name == TankVolume.NODE_NAME)
				{
					var preset = VolumeConfigsLibrary.GetConfig(v.GetValue("name"));
					if(preset != null) Volumes.Add(preset.Clone<VolumeConfiguration>());
					else Volumes.Add(ConfigNodeObject.FromConfig<TankVolume>(v));
				}
				else if(v.name == NODE_NAME)
					Volumes.Add(ConfigNodeObject.FromConfig<VolumeConfiguration>(v));
			}
		}

		public override void Save(ConfigNode node)
		{
			base.Save(node);
			Volumes.ForEach(t => t.Save(node.AddNode(t.NodeName)));
		}

		public override string Info(float volume_conversion = 1)
		{
			volume_conversion = Volume*volume_conversion/TotalVolume;
			return Volumes.Aggregate("", (s, v) => s+v.Info(volume_conversion));
		}

		public override float Cost(float volume_conversion = 1)
		{ 
			volume_conversion = Volume*volume_conversion/TotalVolume;
			return Volumes.Aggregate(0f, (s, v) => s+v.Cost(volume_conversion));
		}

		public override float ResourceCost(bool maxAmount = true, float volume_conversion = 1)
		{ 
			volume_conversion = Volume*volume_conversion/TotalVolume;
			return Volumes.Aggregate(0f, (s, v) => s+v.ResourceCost(maxAmount, volume_conversion));
		}

		public override bool ContainsType(string tank_type)
		{ return Volumes.Any(v => v.ContainsType(tank_type)); }

		public bool ContainsTypes(string[] tank_types)
		{ return tank_types.Any(ContainsType); }
	}

	public class VolumeConfigsLibrary : CustomConfig
	{
		public const string USERFILE = "VolumeConfigs.user";
		public static string UserFile { get { return GameDataFolder("ConfigurableContainers", USERFILE); } }

		static VolumeConfigsLibrary instance;
		static VolumeConfigsLibrary Instance 
		{ 
			get 
			{ 
				if(instance == null) instance = new VolumeConfigsLibrary();
				return instance;
			}
		}

		/// <summary>
		/// The library of tank configurations provided by mods.
		/// </summary>
		public static SortedList<string, VolumeConfiguration> PresetConfigs 
		{ 
			get
			{
				if(presets == null)
				{
					var nodes = GameDatabase.Instance.GetConfigNodes(VolumeConfiguration.NODE_NAME);
					presets = new SortedList<string, VolumeConfiguration>(nodes.Length);
					foreach(ConfigNode n in nodes)
					{
						#if DEBUG
						Utils.Log("Parsing preset tank configuration:\n{}", n);
						#endif
						var cfg = ConfigNodeObject.FromConfig<VolumeConfiguration>(n);
						if(!cfg.Valid)
						{
							var msg = string.Format("ConfigurableContainers: configuration \"{0}\" is INVALID.", cfg.name);
							Utils.Message(6, msg);
							Utils.Log(msg);
							continue;
						}
						try { presets.Add(cfg.name, cfg); }
						catch
						{ 
							Utils.Log("SwitchableTankType: ignoring duplicate configuration of '{}' configuration. " +
							          "Use ModuleManager to change the existing one.", cfg.name); 
						}
					}
				}
				return presets;
			}
		}
		static SortedList<string, VolumeConfiguration> presets;

		/// <summary>
		/// The library of tank configurations saved by the user.
		/// </summary>
		/// <value>The user configs.</value>
		public static SortedList<string, VolumeConfiguration> UserConfigs 
		{
			get
			{
				if(user_configs == null)
				{
					user_configs = new SortedList<string, VolumeConfiguration>();
					var node = LoadNode(UserFile);
					#if DEBUG
					Utils.Log("Loading user configurations from:\n{}\n{}", UserFile, node);
					#endif
					if(node != null)
					{
						foreach(var n in node.GetNodes(VolumeConfiguration.NODE_NAME))
						{
							var cfg = ConfigNodeObject.FromConfig<VolumeConfiguration>(n);
							if(!cfg.Valid)
							{
								var msg = string.Format("ConfigurableContainers: configuration \"{0}\" is INVALID.", cfg.name);
								Utils.Message(6, msg);
								Utils.Log(msg);
								continue;
							}
							else
							{
								if(SwitchableTankType.HaveTankType(cfg.name)) cfg.name += " [cfg]";
								if(PresetConfigs.ContainsKey(cfg.name)) cfg.name += " [usr]";
								add_unique(cfg, user_configs);
							}
						}
					}
				}
				return user_configs;
			}
		}
		static SortedList<string, VolumeConfiguration> user_configs;

		static void add_unique(VolumeConfiguration cfg, IDictionary<string, VolumeConfiguration> db)
		{
			int index = 1;
			var basename = cfg.name;
			while(db.ContainsKey(cfg.name)) 
				cfg.name = string.Concat(basename, " ", index++);
			db.Add(cfg.name, cfg);
		}

		static bool save_user_configs()
		{
			if(UserConfigs.Count == 0) return false;
			var node = new ConfigNode();
			foreach(var c in UserConfigs)
				c.Value.Save(node.AddNode(VolumeConfiguration.NODE_NAME));
			return SaveNode(node, UserFile);
		}

		public static bool AddConfig(VolumeConfiguration cfg)
		{
			add_unique(cfg, UserConfigs);
			return save_user_configs();
		}

		public static bool AddOrSave(VolumeConfiguration cfg)
		{
			if(UserConfigs.ContainsKey(cfg.name))
				UserConfigs[cfg.name] = cfg;
			else UserConfigs.Add(cfg.name, cfg);
			return save_user_configs();
		}

		public static bool RemoveConfig(string cfg_name)
		{ return UserConfigs.Remove(cfg_name) && save_user_configs(); }

		public static List<string> AllConfigNames(string[] include, string[] exclude)
		{
			var names = new List<string>();
			if(include != null && include.Length > 0)
				exclude = SwitchableTankType.TankTypeNames(null, include).ToArray();
			if(exclude != null && exclude.Length > 0)
			{
				names.AddRange(from cfg in PresetConfigs 
				               where cfg.Value.ContainsTypes(exclude)
				               select cfg.Value.name);
				names.AddRange(from cfg in UserConfigs 
				               where cfg.Value.ContainsTypes(exclude)
				               select cfg.Value.name);
			}
			else 
			{
				names.AddRange(PresetConfigs.Keys);
				names.AddRange(UserConfigs.Keys);
			}
			return names;
		}

		public static VolumeConfiguration GetConfig(string name)
		{
			if(string.IsNullOrEmpty(name)) return null;
			VolumeConfiguration cfg;
			if(PresetConfigs.TryGetValue(name, out cfg)) return cfg;
			if(UserConfigs.TryGetValue(name, out cfg)) return cfg;
			return null;
		}

		public static bool HaveUserConfig(string name)
		{ return UserConfigs.ContainsKey(name); }
	}
}

