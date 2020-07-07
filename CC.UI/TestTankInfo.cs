using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CC.UI
{
#if DEBUG
    public class TestTankManager : ITankManager
    {
        public string Title { get; }
        public IList<string> AllTankTypeNames { get; }
        public float TotalVolume { get; }
        public float AvailableVolume { get; private set; }
        public float AvailableVolumePercent { get; private set; }

        public IList<ITankInfo> Tanks { get; }

        private void updateAvailableVolume()
        {
            AvailableVolume = TotalVolume - Tanks.Aggregate(0f, (res, t) => res + t.Volume);
            AvailableVolumePercent = AvailableVolume / TotalVolume * 100;
        }

        public ITankInfo AddTank(string tankType, float volume)
        {
            var tankInfo = new TestTankInfo(tankType, Mathf.Min(volume, AvailableVolume), this);
            Tanks.Add(tankInfo);
            AvailableVolume = TotalVolume - Tanks.Aggregate(0f, (res, t) => res + t.Volume);
            return tankInfo;
        }

        public bool RemoveTank(ITankInfo tank) => Tanks.Remove(tank);

        public TestTankManager()
        {
            AllTankTypeNames = new List<string> { "LiquidChemicals", "Components", "Soil" };
            TotalVolume = 15.79089f;
            AvailableVolume = TotalVolume;
            AvailableVolumePercent = 100;
            Tanks = new List<ITankInfo>();
            Title = "Big Resource Tank";
        }
    }

    public class TestTankInfo : ITankInfo
    {
        public ITankManager Manager { get; }
        public string TankTypeName { get; private set; }
        public IList<string> AllResourceNames { get; }
        public string ResourceName { get; private set; }
        public float UsefulVolumeRatio { get; }
        public float Volume { get; private set; }
        public float MaxAmount => Volume * UnitsPerVolume * UsefulVolumeRatio;
        public float Amount { get; private set; }
        public float UnitsPerVolume { get; }
        public float Density { get; }


        public TestTankInfo(string tankType, float volume, ITankManager manager)
        {
            Manager = manager;
            AllResourceNames = new List<string> { "MonoPropellant", "Oxidizer", "MaterialKits", "SpecializedParts" };
            TankTypeName = tankType;
            ResourceName = AllResourceNames[0];
            UsefulVolumeRatio = 1;
            UnitsPerVolume = 1500f;
            Density = 0.0012f;
            Volume = volume;
            Amount = MaxAmount * 0.78f;
        }

        public void SetVolume(float newVolume)
        {
            newVolume = Math.Min(Manager.TotalVolume, newVolume);
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
