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
        public IList<string> SupportedTypes { get; }
        public IList<string> SupportedTankConfigs { get; }
        public float Volume { get; }
        public float AvailableVolume { get; private set; }
        public float AvailableVolumePercent { get; private set; }

        private readonly List<TestTankInfo> Tanks;
        IReadOnlyCollection<ITankInfo> ITankManager.Tanks => Tanks;

        private class TestTankManagerCapabilities : ITankManagerCapabilities
        {
            public bool AddRemoveEnabled => true;
            public bool ConfirmRemove => true;
            public bool TypeChangeEnabled => true;
            public bool VolumeChangeEnabled => true;
            public bool FillEnabled => true;
            public bool EmptyEnabled => true;
        }

        private readonly TestTankManagerCapabilities Capabilities = new TestTankManagerCapabilities();
        ITankManagerCapabilities ITankManager.Capabilities => Capabilities;

        public void UpdateAvailableVolume()
        {
            AvailableVolume = Volume - Tanks.Aggregate(0f, (res, t) => res + t.Volume);
            AvailableVolumePercent = AvailableVolume / Volume * 100;
        }

        public string OnVolumeChanged(string tankType, float volume) => null;

        public string GetTypeInfo(string tankType) => $"{tankType}: This is a test info.";

        public bool AddTank(string tankType, float volume)
        {
            var tankInfo = new TestTankInfo(tankType, Mathf.Min(volume, AvailableVolume), this);
            Tanks.Add(tankInfo);
            UpdateAvailableVolume();
            return true;
        }

        public bool RemoveTank(ITankInfo tank) => Tanks.Remove(tank as TestTankInfo);

        public bool AddTankConfig(string configName)
        {
            if(!SupportedTankConfigs.Contains(configName))
                SupportedTankConfigs.Add(configName);
            return true;
        }

        public bool RemoveTankConfig(string configName) => SupportedTankConfigs.Remove(configName);

        public TestTankManager()
        {
            SupportedTypes = new List<string>
            {
                "LiquidChemicals",
                "Components",
                "Soil",
                "Type 1",
                "Type 2",
                "Type 3",
                "Type 4",
                "Type 5"
            };
            SupportedTankConfigs = new List<string> { "LFO", "LoX", "CA" };
            Volume = 15.79089f;
            AvailableVolume = Volume;
            AvailableVolumePercent = 100;
            Tanks = new List<TestTankInfo>();
            Title = "Big Resource Tank - with a long long, very long name indeed!";
        }
    }

    public class TestTankInfo : ITankInfo
    {
        private TestTankManager Manager;
        ITankManager ITankInfo.Manager => Manager;
        public string TankType { get; private set; }
        public IList<string> SupportedResources { get; }
        public IList<string> SupportedTypes => Manager.SupportedTypes;
        public string CurrentResource { get; private set; }
        private float UsefulVolumeRatio { get; }
        public float Volume { get; private set; }
        public double MaxAmount => ResourceAmountInVolume(Volume);
        public double Amount { get; private set; }
        private float UnitsPerVolume { get; }
        public float ResourceDensity { get; }
        public bool Valid => CurrentResource != "Oxidizer";
        public float ResourceAmountInVolume(float volume) => volume * UnitsPerVolume * UsefulVolumeRatio;

        public float VolumeForResourceAmount(float amount) => amount / UnitsPerVolume / UsefulVolumeRatio;


        public TestTankInfo(string tankType, float volume, TestTankManager manager)
        {
            Manager = manager;
            SupportedResources = new List<string> { "MonoPropellant", "Oxidizer", "MaterialKits", "SpecializedParts" };
            TankType = tankType;
            CurrentResource = SupportedResources[0];
            UsefulVolumeRatio = 1;
            UnitsPerVolume = 1500f;
            ResourceDensity = 0.0012f;
            Volume = volume;
            Amount = MaxAmount * 0.78f;
        }

        public void SetVolume(float volume, bool update_amount)
        {
            volume = Mathf.Clamp(volume, 0, Manager.Volume);
            if(update_amount)
                Amount *= volume / Volume;
            Volume = volume;
            Manager.UpdateAvailableVolume();
        }

        public void ChangeTankType(string tankTypeName)
        {
            TankType = tankTypeName;
        }

        public void ChangeResource(string resourceName)
        {
            CurrentResource = resourceName;
        }

        public void SetAmount(float newAmount)
        {
            Amount = Math.Min(MaxAmount, newAmount);
        }
    }
#endif
}
