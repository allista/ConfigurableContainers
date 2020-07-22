using AT_Utils.UI;
using UnityEngine.Events;
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

        private bool addUpdateEnabled;

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
            addUpdateEnabled = enable;
            updateControls();
        }

        private void updateControls()
        {
            var noDialogOpen = !DialogFactory.ContextIsActive(this);
            addConfigButton.SetInteractable(addUpdateEnabled
                                            && noDialogOpen
                                            && !string.IsNullOrEmpty(configNameField.text));
            updateConfigButton.SetInteractable(addUpdateEnabled
                                               && noDialogOpen);
            deleteConfigButton.SetInteractable(noDialogOpen
                                               && tankManager != null
                                               && tankManager.SupportedTankConfigs.Count > 0);
            configsDropdown.SetInteractable(noDialogOpen);
            configNameField.SetInteractable(noDialogOpen);
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
            configsDropdown.SetOptionsSafe(UI_Utils.namesToOptions(tankManager.SupportedTankConfigs, false));
            updateConfigsDropdownTooltip(configsDropdown.value);
            addTankControl.UpdateTankTypes();
        }

        private void updateConfigsDropdownTooltip(int index) =>
            configsDropdownTooltip.SetText(tankManager.GetTypeInfo(tankManager.SupportedTankConfigs[index]));

        private void onConfigNameChanged(string newConfigName) =>
            addConfigButton.SetInteractable(addUpdateEnabled && !string.IsNullOrEmpty(newConfigName));

        private void onUpdateConfig() => updateConfig(tankManager.SupportedTankConfigs[configsDropdown.value]);

        private void updateConfig(string tankConfig, UnityAction onSuccess = null)
        {
            DialogFactory.Danger($"Are you sure you want to <b>{Colors.Warning.Tag("overwrite")}</b> "
                                 + $"the <b>{Colors.Selected1.Tag(tankConfig)}</b> preset?",
                () =>
                {
                    if(!tankManager.AddTankConfig(tankConfig))
                        return;
                    updateConfigsDropdown();
                    onSuccess?.Invoke();
                },
                onDestroy: updateControls,
                context: this);
            updateControls();
        }

        private void onAddConfig()
        {
            var tankConfig = configNameField.text;
            if(string.IsNullOrEmpty(tankConfig))
                return;
            if(tankManager.SupportedTankConfigs.Contains(tankConfig))
                updateConfig(tankConfig, () => configNameField.text = "");
            else if(tankManager.AddTankConfig(tankConfig))
            {
                configNameField.text = "";
                updateConfigsDropdown();
            }
        }

        private void onDeleteConfig()
        {
            var tankConfig = tankManager.SupportedTankConfigs[configsDropdown.value];
            DialogFactory.Danger($"Are you sure you want to <b>{Colors.Danger.Tag("delete")}</b> "
                                 + $"the <b>{Colors.Selected1.Tag(tankConfig)}</b> preset?",
                () =>
                {
                    if(tankManager.RemoveTankConfig(tankConfig))
                        updateConfigsDropdown();
                },
                onDestroy: updateControls,
                context: this);
            updateControls();
        }
    }
}
