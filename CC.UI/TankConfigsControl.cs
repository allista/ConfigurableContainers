using AT_Utils.UI;
using UnityEngine.UI;

namespace CC.UI
{
    public class TankConfigsControl : TankManagerUIPart
    {
        public InputField
            configNameField;

        public Dropdown
            configsDropdown;

        public TooltipTrigger
            configsDropdownTooltip;

        public Button
            addConfigButton,
            updateConfigButton,
            deleteConfigButton;

        public AddTankControl addTankControl;

        private bool controlsEnabled;

        private ITankManager tankManager;

        private void Awake()
        {
            configsDropdown.onValueChanged.AddListener(updateConfigsDropdownTooltip);
            addConfigButton.onClick.AddListener(onAddConfig);
            updateConfigButton.onClick.AddListener(onUpdateConfig);
            deleteConfigButton.onClick.AddListener(onDeleteConfig);
            configNameField.onValueChanged.AddListener(onConfigNameChanged);
            EnableControls(false);
        }

        private void OnDestroy()
        {
            configsDropdown.onValueChanged.RemoveAllListeners();
            addConfigButton.onClick.RemoveAllListeners();
            updateConfigButton.onClick.RemoveAllListeners();
            deleteConfigButton.onClick.RemoveAllListeners();
            configNameField.onValueChanged.RemoveAllListeners();
        }

        public void EnableControls(bool enable)
        {
            controlsEnabled = enable;
            addConfigButton.SetInteractable(controlsEnabled && !string.IsNullOrEmpty(configNameField.text));
            updateConfigButton.SetInteractable(controlsEnabled);
        }

        public void SetTankManager(ITankManager manager)
        {
            if(tankManager == manager)
                return;
            tankManager = manager;
            if(tankManager == null)
                return;
            updateConfigsDropdown();
            EnableControls(tankManager.Tanks.Count > 0);
        }

        private void updateConfigsDropdown()
        {
            configsDropdown.options = UI_Utils.namesToOptions(tankManager.SupportedTankConfigs, false);
            updateConfigsDropdownTooltip(configsDropdown.value);
            addTankControl.UpdateTankTypes();
        }

        private void updateConfigsDropdownTooltip(int index) =>
            configsDropdownTooltip.SetText(tankManager.GetTypeInfo(tankManager.SupportedTankConfigs[index]));

        private void onConfigNameChanged(string newConfigName)
        {
            addConfigButton.SetInteractable(controlsEnabled && !string.IsNullOrEmpty(newConfigName));
        }

        private void onUpdateConfig()
        {
            var tankConfig = tankManager.SupportedTankConfigs[configsDropdown.value];
            tankManager.AddTankConfig(tankConfig);
            updateConfigsDropdown();
        }

        private void onAddConfig()
        {
            var tankConfig = configNameField.text;
            if(string.IsNullOrEmpty(tankConfig))
                return;
            if(!tankManager.AddTankConfig(tankConfig))
                return;
            configNameField.text = "";
            updateConfigsDropdown();
        }

        private void onDeleteConfig()
        {
            var tankConfig = tankManager.SupportedTankConfigs[configsDropdown.value];
            if(!tankManager.RemoveTankConfig(tankConfig))
                return;
            updateConfigsDropdown();
        }
    }
}
