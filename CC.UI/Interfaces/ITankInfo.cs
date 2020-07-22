using System.Collections.Generic;

namespace CC.UI
{
    public interface ITankInfo
    {
        ITankManager Manager { get; }
        string TankType { get; }

        IList<string> SupportedResources { get; }
        IList<string> SupportedTypes { get; }
        string CurrentResource { get; }
        bool Valid { get; }

        float Volume { get; }

        double Amount { get; }
        double MaxAmount { get; }
        float ResourceDensity { get; }


        float ResourceAmountInVolume(float volume);
        float VolumeForResourceAmount(float amount);
        void SetVolume(float volume, bool update_amount);
        void ChangeTankType(string tankTypeName);
        void ChangeResource(string resourceName);
        void SetAmount(float newAmount);
    }
}
