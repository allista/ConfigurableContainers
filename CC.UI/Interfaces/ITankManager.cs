using System.Collections.Generic;

namespace CC.UI
{
    public interface ITankManagerCapabilities
    {
        bool AddRemoveEnabled { get; }
        bool ConfirmRemove { get; }
        bool TypeChangeEnabled { get; }
        bool VolumeChangeEnabled { get; }
        bool FillEnabled { get; }
        bool EmptyEnabled { get; }
    }

    public interface ITankManager
    {
        string Title { get; }
        IList<string> SupportedTypes { get; }
        IList<string> SupportedTankConfigs { get; }
        float Volume { get; }
        float AvailableVolume { get; }
        IReadOnlyCollection<ITankInfo> Tanks { get; }
        ITankManagerCapabilities Capabilities { get; }

        string OnVolumeChanged(string tankType, float volume);

        string GetTypeInfo(string tankType);

        bool AddTank(string tankType, float volume);
        bool RemoveTank(ITankInfo tank);

        bool AddTankConfig(string configName);
        bool RemoveTankConfig(string configName);
    }
}
