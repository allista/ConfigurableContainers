//   CryogenicsParams.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections.Generic;

namespace AT_Utils
{
    public class CryogenicsParams : ConfigNodeObject
    {
        public new const string NODE_NAME = "CRYOGENICS";

        private const string config_path = "ConfigurableContainers/Cryogenics/";

        /// <summary>
        ///     The absolute zero, 0K.
        /// </summary>
        public const double AbsZero = -273.15;

        private static CryogenicsParams instance;

        private readonly Dictionary<string, CryoResource> Resources = new Dictionary<string, CryoResource>();

        /// <summary>
        ///     How much kJs does 1 electric charge contain?
        /// </summary>
        [Persistent]
        public float ElectricCharge2kJ = 10;

        /// <summary>
        ///     Limits energy transfer between the resource volume and the rest of the part.
        ///     kW/m/K
        /// </summary>
        [Persistent]
        public float InsulationConductivity = 1e-3f;

        /// <summary>
        ///     The fraction of the tank's volume that is used for insulation
        /// </summary>
        [Persistent]
        public float InsulationVolumeFraction = 0.02f;

        /// <summary>
        ///     Maximum total power consumption of any cooler (Ec/s)
        /// </summary>
        [Persistent]
        public float MaxAbsoluteCoolerPower = 500;

        /// <summary>
        ///     Maximum power consumption of a cooler (Ec/s) per unit thermal mass
        /// </summary>
        [Persistent]
        public float MaxSpecificCoolerPower = 1;

        /// <summary>
        ///     If the power supply drops below this fraction, the cooler is automatically disabled
        /// </summary>
        [Persistent]
        public float ShutdownThreshold = 0.99f;

        /// <summary>
        ///     Used when no VaporizationHeat is provided for a resource to estimate it
        /// </summary>
        [Persistent]
        public float SpecificHeat2VaporizationHeat = 1000;

        public static CryogenicsParams Instance
        {
            get
            {
                if(instance != null)
                    return instance;
                instance = new CryogenicsParams();
                var node = GameDatabase.Instance.GetConfigNode(config_path + NODE_NAME);
                if(node != null)
                    instance.Load(node);
                else
                    Utils.Log("CryogenicsParams NODE not found: {}", config_path + NODE_NAME);
                return instance;
            }
        }

        /// <summary>
        ///     Retrieve the cryogenic resource info for the given part resource.
        /// </summary>
        /// <returns>The cryogenic resource info.</returns>
        /// <param name="r">The part resource.</param>
        public CryoResource GetResource(PartResource r)
        {
            return Resources.TryGetValue(r.resourceName, out var res) ? res : null;
        }

        /// <summary>
        ///     Calculates conductivity of insulation of a given spherical volume.
        /// </summary>
        /// <returns>The insulator conductivity in kW/K.</returns>
        /// <param name="volume">Volume of a tank.</param>
        public static double GetInsulatorConductivity(double volume)
        {
            return -Instance.InsulationConductivity
                   * Math.Pow(48 * Math.PI * Math.PI * volume / Instance.InsulationVolumeFraction, 1 / 3f);
        }

#if DEBUG
        public static void Reload()
        {
            var node = ConfigNode.Load(CustomConfig.GameDataFolder("ConfigurableContainers", "Cryogenics.cfg"));
            if(node == null)
            {
                Utils.Log("Unable to read Cryogenics.cfg");
                return;
            }
            if(instance == null)
                instance = new CryogenicsParams();
            instance.LoadFrom(node);
            Utils.Log("CryogenicsParams reloaded:\n{}", instance);
        }
#endif

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            Resources.Clear();
            foreach(var n in node.GetNodes(CryoResource.NODE_NAME))
            {
                var res = FromConfig<CryoResource>(n);
                Resources.Add(res.name, res);
            }
        }

        public class CryoResource : ConfigNodeObject
        {
            public new const string NODE_NAME = "RESOURCE";
            [Persistent] public float BoiloffTemperature = 120;
            [Persistent] public float CoolingEfficiency = 0.3f;

            [Persistent] public string name = "";
            [Persistent] public float VaporizationHeat = -1;

            public double GetVaporizationHeat(PartResource r)
            {
                return VaporizationHeat > 0
                    ? VaporizationHeat
                    : r.info.specificHeatCapacity * Instance.SpecificHeat2VaporizationHeat;
            }
        }
    }
}
