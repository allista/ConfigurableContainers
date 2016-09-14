//   SwitchableTankType.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Linq;
using System.Collections.Generic;

namespace AT_Utils
{
	/// <summary>
	/// Type of a switchable tank. 
	/// Defines resources that a tank of this type can hold, 
	/// how much units of each resource a liter of volume contains,
	/// and portion of part's volume that can be used by the tank.
	/// </summary>
	public class SwitchableTankType : ConfigNodeObject
	{
		#region Tank Type Library
		/// <summary>
		/// The library of preconfigured tank types.
		/// </summary>
		public static SortedList<string, SwitchableTankType> TankTypes 
		{ 
			get
			{
				if(_tank_types == null)
				{
					var nodes = GameDatabase.Instance.GetConfigNodes(NODE_NAME);
					_tank_types = new SortedList<string, SwitchableTankType>();
					foreach(ConfigNode n in nodes)
					{
						#if DEBUG
						Utils.Log("\n{}", n.ToString());
						#endif
						var tank_type = ConfigNodeObject.FromConfig<SwitchableTankType>(n);
						if(!tank_type.Valid)
						{
							var msg = string.Format("ConfigurableContainers: configuration of \"{0}\" tank type is INVALID.", tank_type.name);
							Utils.Message(6, msg);
							Utils.Log(msg);
							continue;
						}
						try { _tank_types.Add(tank_type.name, tank_type); }
						catch
						{ 
							Utils.Log("SwitchableTankType: ignoring duplicate configuration of {} tank type. " +
							"Use ModuleManager to change the existing one.", tank_type.name); 
						}
					}
				}
				return _tank_types;
			}
		}
		static SortedList<string, SwitchableTankType> _tank_types;

		/// <summary>
		/// Sorted list of tank type names.
		/// </summary>
		public static List<string> TankTypeNames(string[] include = null, string[] exclude = null) 
		{ 
			IEnumerable<string> names = null;
			if(include != null && include.Length > 0)
				names = TankTypes.Keys.Where(n => include.IndexOf(n) >= 0);
			else if(exclude != null && exclude.Length > 0)
				names = TankTypes.Keys.Where(n => exclude.IndexOf(n) < 0);
			else names = TankTypes.Keys;
			return names.ToList();
		}

		/// <summary>
		/// Determines if the library contains the specified tank type.
		/// </summary>
		/// <param name="tank_type">Tank type name.</param>
		public static bool HaveTankType(string tank_type)
		{ return TankTypes.ContainsKey(tank_type); }

		/// <summary>
		/// Returns the TankType from the library, if it exists; null otherwise.
		/// </summary>
		/// <param name="tank_type">Tank type name.</param>
		public static SwitchableTankType GetTankType(string tank_type)
		{
			SwitchableTankType t;
			return TankTypes.TryGetValue(tank_type, out t) ? t : null;
		}

		/// <summary>
		/// Returns info string describing available tank types
		/// </summary>
		public static string TypesInfo(string[] include = null, string[] exclude = null)
		{
			var info = "Supported Tank Types:\n";
			info += TankTypeNames(include, exclude)
				.Aggregate("", (i, t) => string.Concat(i, "- ", t, "\n"));
			return info;
		}
		#endregion

		new public const string NODE_NAME = "TANKTYPE";
		/// <summary>
		/// The name of the tank type. 
		/// It is possible to edit these nodes with MM using NODE[name] syntax.
		/// </summary>
		[Persistent] public string name;
		/// <summary>
		/// The string list of resources a tank of this type can hold. Format:
		/// ResourceName1 units_per_liter; ResourceName2 units_per_liter2; ...
		/// </summary>
		[Persistent] public string PossibleResources;
		/// <summary>
		/// The portion of a part's volume the tank can use.
		/// </summary>
		[Persistent] public float  UsefulVolumeRatio = 1f;
		/// <summary>
		/// The cost of a tank of this type per tank volume.
		/// </summary>
		[Persistent] public float  TankCostPerSurface = 10f;

		public SortedList<string, TankResource> Resources { get; private set; }
		public bool Valid { get { return Resources != null && Resources.Count > 0; } }
		public IList<string> ResourceNames { get { return Resources.Keys; } }
		public TankResource DefaultResource { get { return Resources.Values[0]; } }

		public TankResource this[string name]
		{
			get
			{
				try { return Resources[name]; }
				catch { return null; }
			}
		}

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			Resources = TankResource.ParseResourcesToSortedList(PossibleResources);
		}

		public string Info
		{ 
			get 
			{ 
				var info = "";
				if(!Valid) return info;
				info += "Tank can hold:\n";
				foreach(var r in ResourceNames)
					info += string.Format("- {0}: {1}/L\n", 
						Resources[r].Name, Utils.formatUnits(Resources[r].UnitsPerLiter));
				return info;
			} 
		}
	}


	/// <summary>
	/// A Part Resource Definition complemented with Units Per Liter ratio.
	/// </summary>
	public class TankResource : ResourceWrapper<TankResource>
	{
		public float UnitsPerLiter { get; private set; }

		public override void LoadDefinition(string resource_definition)
		{
			var upl = load_definition(resource_definition);
			if(Valid) UnitsPerLiter = upl;
		}	
	}
}

