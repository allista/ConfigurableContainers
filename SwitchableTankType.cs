﻿//   SwitchableTankType.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections.Generic;
using System.Linq;

namespace AT_Utils
{
    /// <summary>
    ///     Type of a switchable tank.
    ///     Defines resources that a tank of this type can hold,
    ///     how much units of each resource a liter of volume contains,
    ///     and portion of part's volume that can be used by the tank.
    /// </summary>
    public class SwitchableTankType : ConfigNodeObject
    {
        public new const string NODE_NAME = "TANKTYPE";

        /// <summary>
        ///     The additional mass of a tank of this type per volume.
        /// </summary>
        [Persistent]
        public float AddMassPerVolume = 0f;

        /// <summary>
        ///     If the resources in this tank should boiloff with time.
        /// </summary>
        [Persistent]
        public bool Boiloff;

        /// <summary>
        ///     If the resources in this tank should be actively cooled until below the boiloff temperature.
        /// </summary>
        [Persistent]
        public bool Cooling;

        private string info;

        /// <summary>
        ///     The name of the tank type.
        ///     It is possible to edit these nodes with MM using NODE[name] syntax.
        /// </summary>
        [Persistent]
        public string name;

        /// <summary>
        ///     The string list of resources a tank of this type can hold. Format:
        ///     ResourceName1 units_per_liter; ResourceName2 units_per_liter2; ...
        /// </summary>
        [Persistent]
        public string PossibleResources;

        /// <summary>
        ///     The cost of a tank of this type per tank surface.
        /// </summary>
        [Persistent]
        public float TankCostPerSurface = 1f;

        /// <summary>
        ///     The cost of a tank of this type per tank volume.
        /// </summary>
        [Persistent]
        public float TankCostPerVolume = 0f;

        /// <summary>
        ///     The portion of a part's volume the tank can use.
        /// </summary>
        [Persistent]
        public float UsefulVolumeRatio = 1f;

        public SortedList<string, TankResource> Resources { get; private set; }
        public bool Valid => Resources != null && Resources.Count > 0;
        public IList<string> ResourceNames => Resources.Keys;
        public TankResource DefaultResource => Resources.Values[0];

        public TankResource this[string resourceName]
        {
            get
            {
                try
                {
                    return Resources[resourceName];
                }
                catch
                {
                    return null;
                }
            }
        }

        public string Info
        {
            get
            {
                if(!Valid)
                    return "";
                if(info != null)
                    return info;
                var _info = StringBuilderCache.Acquire();
                _info.AppendLine("Tank can hold:");
                foreach(var r in ResourceNames)
                    _info.AppendLine(
                        $"- <b>{Resources[r].Name}</b>: {Utils.formatUnits(Resources[r].UnitsPerLiter)}/L");
                var useful_volume = UsefulVolume(100);
                if(useful_volume < 100)
                    _info.AppendLine($"<b>Only {useful_volume:F0}% of the volume</b> is used for resources.");
                if(Boiloff || Cooling)
                    _info.AppendLine("Tank is thermally <b>insulated</b>.\nEquipped with <b>boil-off</b> valve.");
                if(Cooling)
                    _info.AppendLine("Equipped with <b>Active Cooling System</b>.");
                info = _info.ToStringAndRelease().Trim();
                return info;
            }
        }

        public float Cost(float volume)
        {
            return volume * TankCostPerVolume + Utils.CubeSurface(volume) * TankCostPerSurface;
        }

        public float AddMass(float volume)
        {
            return volume * AddMassPerVolume;
        }

        public float GetEffectiveVolumeRatio()
        {
            var useful_volume = UsefulVolumeRatio;
            if(Boiloff || Cooling)
                useful_volume -= CryogenicsParams.Instance.InsulationVolumeFraction;
            return useful_volume < 0 ? 0 : useful_volume;
        }

        public float UsefulVolume(float volume) => volume * GetEffectiveVolumeRatio();

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            Resources = TankResource.ParseResourcesToSortedList(PossibleResources);
        }

        #region Tank Type Library
        /// <summary>
        ///     The library of preconfigured tank types.
        /// </summary>
        public static SortedList<string, SwitchableTankType> TankTypes
        {
            get
            {
                if(_tank_types != null)
                    return _tank_types;
                var nodes = GameDatabase.Instance.GetConfigNodes(NODE_NAME);
                _tank_types = new SortedList<string, SwitchableTankType>();
                for(int i = 0, nodesLength = nodes.Length; i < nodesLength; i++)
                {
                    var n = nodes[i];
#if DEBUG
                    Utils.Log("\n{}", n.ToString());
#endif
                    var tank_type = FromConfig<SwitchableTankType>(n);
                    if(!tank_type.Valid)
                    {
                        var msg =
                            $"[ConfigurableContainers] '{tank_type.name}' tank type has no resources. Skipping.";
                        Utils.Message(6, msg);
                        Utils.Warning(msg);
                        continue;
                    }
                    try
                    {
                        _tank_types.Add(tank_type.name, tank_type);
                    }
                    catch
                    {
                        Utils.Error(
                            $"SwitchableTankType: ignoring duplicate configuration of {tank_type.name} tank type. Use ModuleManager to change the existing one.");
                    }
                }
                return _tank_types;
            }
        }

        private static SortedList<string, SwitchableTankType> _tank_types;

        /// <summary>
        ///     Sorted list of tank type names.
        /// </summary>
        public static List<string> TankTypeNames(string[] include = null, string[] exclude = null)
        {
            IEnumerable<string> names;
            if(include != null && include.Length > 0)
                names = TankTypes.Keys.Where(n => include.IndexOf(n) >= 0);
            else if(exclude != null && exclude.Length > 0)
                names = TankTypes.Keys.Where(n => exclude.IndexOf(n) < 0);
            else
                names = TankTypes.Keys;
            return names.ToList();
        }

        /// <summary>
        ///     Determines if the library contains the specified tank type.
        /// </summary>
        /// <param name="tank_type">Tank type name.</param>
        public static bool HaveTankType(string tank_type)
        {
            return TankTypes.ContainsKey(tank_type);
        }

        /// <summary>
        ///     Returns the TankType from the library, if it exists; null otherwise.
        /// </summary>
        /// <param name="tank_type">Tank type name.</param>
        public static SwitchableTankType GetTankType(string tank_type)
        {
            return TankTypes.TryGetValue(tank_type, out var t) ? t : null;
        }

        /// <summary>
        ///     Finds the first tank type containing the specified resource.
        /// </summary>
        /// <returns>The tank type.</returns>
        /// <param name="resource_name">Resource name.</param>
        public static SwitchableTankType FindTankType(string resource_name)
        {
            foreach(var t in TankTypes)
                if(t.Value.Resources.ContainsKey(resource_name))
                    return t.Value;
            return null;
        }

        /// <summary>
        ///     Returns TankType.Info for a type, if it exists; string.Empty otherwise.
        /// </summary>
        /// <param name="tank_type">Tank type name.</param>
        public static string GetTankTypeInfo(string tank_type)
        {
            return TankTypes.TryGetValue(tank_type, out var t) ? t.Info : string.Empty;
        }

        /// <summary>
        ///     Returns info string describing available tank types
        /// </summary>
        public static string TypesInfo(string[] include = null, string[] exclude = null)
        {
            var info = StringBuilderCache.Acquire();
            info.AppendLine("<b>Supported Tank Types</b>:");
            TankTypeNames(include, exclude).ForEach(t => info.AppendLine($"- {t}"));
            return info.ToStringAndRelease().Trim();
        }
        #endregion
    }


    /// <summary>
    ///     A Part Resource Definition complemented with Units Per Liter ratio.
    /// </summary>
    public class TankResource : ResourceWrapper<TankResource>
    {
        public float UnitsPerLiter { get; private set; }
        public float UnitsPerVolume { get; private set; }

        public PartResourceDefinition def => PartResourceLibrary.Instance.GetDefinition(Name);

        public override void LoadDefinition(string resource_definition)
        {
            var upl = load_definition(resource_definition);
            if(!Valid)
                return;
            UnitsPerLiter = upl;
            UnitsPerVolume = upl * 1000;
        }
    }
}
