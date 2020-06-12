using System;
using System.Collections.Generic;

namespace CC.UI
{
#if DEBUG
    public class TestTankInfo : ITankInfo
    {
        public IList<string> AllTankTypeNames { get; }
        public string TankTypeName { get; private set; }
        public IList<string> AllResourceNames { get; }
        public string ResourceName { get; private set; }
        public float UsefulVolumeRatio { get; }
        public float TotalVolume { get; }
        public float Volume { get; private set; }
        public float MaxAmount => Volume * UnitsPerVolume * UsefulVolumeRatio;
        public float Amount { get; private set; }
        public float UnitsPerVolume { get; }
        public float Density { get; }

        public TestTankInfo()
        {
            AllTankTypeNames = new List<string> { "LiquidChemicals", "Components", "Soil" };
            AllResourceNames = new List<string> { "MonoPropellant", "Oxidizer", "MaterialKits", "SpecializedParts" };
            TankTypeName = AllTankTypeNames[0];
            ResourceName = AllResourceNames[0];
            TotalVolume = 15.79089f;
            UsefulVolumeRatio = 1;
            UnitsPerVolume = 1500f;
            Density = 0.0012f;
            Volume = 7;
            Amount = MaxAmount * 0.78f;
            
        }

        public void SetVolume(float newVolume)
        {
            newVolume =Math.Min(TotalVolume, newVolume);
            Amount *= newVolume / Volume;
            Volume = newVolume;
        }

        public void ChangeTankType(string tankTypeName)
        {
            TankTypeName = tankTypeName;
        }

        public void ChangeResource(string resourceName)
        {
            ResourceName = resourceName;
        }

        public void SetAmount(float newAmount)
        {
            Amount = Math.Min(MaxAmount, newAmount);
        }
    }
#endif
}
