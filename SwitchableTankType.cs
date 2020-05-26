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
                    if(nodes != null)
                    {
                        for(int i = 0, nodesLength = nodes.Length; i < nodesLength; i++)
                        {
                            ConfigNode n = nodes[i];
                            #if DEBUG
                            Utils.Log("\n{}", n.ToString());
                            #endif
                            var tank_type = FromConfig<SwitchableTankType>(n);
                            if(!tank_type.Valid)
                            {
                                var msg = string.Format("[ConfigurableContainers] '{0}' tank type has no resources. Skipping.", tank_type.name);
                                Utils.Message(6, msg);
                                Utils.Log(msg);
                                continue;
                            }
                            try
                            {
                                _tank_types.Add(tank_type.name, tank_type);
                            }
                            catch
                            {
                                Utils.Log("SwitchableTankType: ignoring duplicate configuration of {} tank type. " + "Use ModuleManager to change the existing one.", tank_type.name);
                            }
                        }
                    }
                }
                return _tank_types;
            }
        }

        private static SortedList<string, SwitchableTankType> _tank_types;

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
            return TankTypes.TryGetValue(tank_type, out var t) ? t : null;
        }

        /// <summary>
        /// Finds the first tank type containing the specified resource.
        /// </summary>
        /// <returns>The tank type.</returns>
        /// <param name="resource_name">Resource name.</param>
        public static SwitchableTankType FindTankType(string resource_name)
        {
            foreach(var t in TankTypes)
            {
                if(t.Value.Resources.ContainsKey(resource_name))
                    return t.Value;
            }
            return null;
        }

        /// <summary>
        /// Returns TankType.Info for a type, if it exists; string.Empty otherwise.
        /// </summary>
        /// <param name="tank_type">Tank type name.</param>
        public static string GetTankTypeInfo(string tank_type)
        {
            return TankTypes.TryGetValue(tank_type, out var t) ? t.Info : string.Empty;
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

        public new const string NODE_NAME = "TANKTYPE";
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
        /// The cost of a tank of this type per tank surface.
        /// </summary>
        [Persistent] public float  TankCostPerSurface = 1f;
        /// <summary>
        /// The cost of a tank of this type per tank volume.
        /// </summary>
        [Persistent] public float  TankCostPerVolume = 0f;
        /// <summary>
        /// The additional mass of a tank of this type per volume.
        /// </summary>
        [Persistent] public float  AddMassPerVolume = 0f;
        /// <summary>
        /// If the resources in this tank should boiloff with time.
        /// </summary>
        [Persistent] public bool   Boiloff;
        /// <summary>
        /// If the resources in this tank should be actively cooled untill below the boiloff temperature.
        /// </summary>    
        [Persistent] public bool   Cooling;

        public float Cost(float volume)
        { return volume*TankCostPerVolume + Utils.CubeSurface(volume)*TankCostPerSurface; }

        public float AddMass(float volume) { return volume*AddMassPerVolume; }

        public float UsefulVolume(float volume)
        {
            var useful_volume = UsefulVolumeRatio;
            if(Boiloff || Cooling) useful_volume -= CryogenicsParams.Instance.InsulationVolumeFraction;
            if(useful_volume < 0) return 0;
            return volume * useful_volume;
        }

        public SortedList<string, TankResource> Resources { get; private set; }
        public bool Valid => Resources != null && Resources.Count > 0;
        public IList<string> ResourceNames => Resources.Keys;
        public TankResource DefaultResource => Resources.Values[0];

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

        private string info = null;
        public string Info
        { 
            get 
            { 
                if(!Valid) return "";
                if(info == null)
                {
                    info = "";
                    info += "Tank can hold:\n";
                    foreach(var r in ResourceNames)
                        info += string.Format("- {0}: {1}/L\n", 
                            Resources[r].Name, Utils.formatUnits(Resources[r].UnitsPerLiter));
                    var usefull_volume = UsefulVolume(100);
                    if(usefull_volume < 100)
                        info += string.Format("Only {0:F0}% of the volume is used for resources.\n", usefull_volume);
                    if(Boiloff||Cooling) info += "Tank is thermally insulated.\nEquipped with boil-off valve.\n";
                    if(Cooling) info += "Equipped with Active Cooling System.\n";
                }
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

