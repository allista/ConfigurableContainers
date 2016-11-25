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

namespace AT_Utils
{
	public class BaseVolume : ConfigNodeObject
	{ 
		new public const string NODE_NAME = "VOLUME";
		[Persistent] public float Volume = 1;

		public virtual string Info(float volume_conversion = 1)
		{ return string.Format("{0}\n", Utils.formatVolume(Volume*volume_conversion)); }

		public virtual float AddMass(float volume_conversion = 1) { return 0f; }

		public virtual float Cost(float volume_conversion = 1) { return 0f; }

		public virtual float ResourceCost(bool maxAmount = true, float volume_conversion = 1) { return 0f; }

		public virtual float ResourceMass(bool maxAmount = true, float volume_conversion = 1) { return 0f; }

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

		public override float AddMass(float volume_conversion = 1)
		{ 
			var t = Type;
			return t == null ? 0 : t.AddMass(Volume*volume_conversion);
		}

		public override float Cost(float volume_conversion = 1)
		{ 
			var t = Type;
			return t == null ? 0 : t.Cost(Volume*volume_conversion);
		}

		public override float ResourceCost(bool maxAmount = true, float volume_conversion = 1) 
		{ 
			try
			{
				var t = Type;
				var res = t.Resources[CurrentResource];
				var res_def = PartResourceLibrary.Instance.GetDefinition(res.Name);
				var cost = res_def.unitCost * res.UnitsPerLiter * t.UsefulVolume(Volume) * volume_conversion * 1000;
				return maxAmount? cost : cost * InitialAmount;
			}
			catch { return 0; }
		}

		public override float ResourceMass(bool maxAmount = true, float volume_conversion = 1) 
		{ 
			try
			{
				var t = Type;
				var res = t.Resources[CurrentResource];
				var res_def = PartResourceLibrary.Instance.GetDefinition(res.Name);
				var cost = res_def.density * res.UnitsPerLiter * t.UsefulVolume(Volume) * volume_conversion * 1000;
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
					if(preset != null) 
					{
						float volume;
						if(!float.TryParse(v.GetValue("Volume"), out volume)) volume = 100f;
						var cfg = preset.Clone<VolumeConfiguration>();
						cfg.Volume = volume;
						Volumes.Add(cfg);
					}
					else Volumes.Add(ConfigNodeObject.FromConfig<TankVolume>(v));
				}
				else if(v.name == NODE_NAME)
					Volumes.Add(ConfigNodeObject.FromConfig<VolumeConfiguration>(v));
			}
		}

		public override void Save(ConfigNode node)
		{
			base.Save(node);
			Volumes.ForEach(t => t.SaveInto(node));
		}

		public override string Info(float volume_conversion = 1)
		{
			volume_conversion = Volume*volume_conversion/TotalVolume;
			return Volumes.Aggregate("", (s, v) => s+v.Info(volume_conversion));
		}

		public override float AddMass(float volume_conversion = 1)
		{ 
			volume_conversion = Volume*volume_conversion/TotalVolume;
			return Volumes.Aggregate(0f, (s, v) => s+v.AddMass(volume_conversion));
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

		public override float ResourceMass(bool maxAmount = true, float volume_conversion = 1)
		{ 
			volume_conversion = Volume*volume_conversion/TotalVolume;
			return Volumes.Aggregate(0f, (s, v) => s+v.ResourceMass(maxAmount, volume_conversion));
		}

		public override bool ContainsType(string tank_type)
		{ return Volumes.Any(v => v.ContainsType(tank_type)); }

		public bool ContainsTypes(string[] tank_types)
		{ return tank_types.Any(ContainsType); }
	}
}

