using AT_Utils.UI;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CC.UI
{
    public class TankControlsUI : TankManagerUIPart
    {
        public Button
            deleteButton,
            fullTankButton,
            emptyTankButton,
            editVolumeButton,
            editMaxAmountButton,
            editMaxMassButton;

        public Text
            resourceVolume,
            resourceMaxAmount,
            resourceAmount,
            resourceMaxMass,
            resourceMass,
            tankFullness;

        public Dropdown
            tankTypeDropdown,
            resourceDropdown;

        public PanelledUI
            volumeDisplay;

        public FloatController
            volumeEditor;

        private ITankInfo tank;

        private void updateResourcesDropdown() =>
            resourceDropdown.options = UI_Utils.namesToOptions(tank.SupportedResources);

        public void SetTank(ITankInfo newTank)
        {
            if(newTank == tank)
                return;
            tank = newTank;
            if(tank == null)
                return;
            tankTypeDropdown.options = UI_Utils.namesToOptions(tank.SupportedTypes);
            updateResourcesDropdown();
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            resourceVolume.text = FormatUtils.formatVolume(tank.Volume);
            resourceMaxAmount.text = FormatUtils.formatBigValue((float)tank.MaxAmount, "u");
            resourceAmount.text = FormatUtils.formatBigValue((float)tank.Amount, "u");
            if(tank.ResourceDensity > 0)
            {
                editMaxMassButton.gameObject.SetActive(true);
                resourceMass.gameObject.SetActive(true);
                resourceMaxMass.text = FormatUtils.formatMass((float)(tank.MaxAmount * tank.ResourceDensity));
                resourceMass.text = FormatUtils.formatMass((float)(tank.Amount * tank.ResourceDensity));
            }
            else
            {
                editMaxMassButton.gameObject.SetActive(false);
                resourceMass.gameObject.SetActive(false);
            }
            tankFullness.text = (tank.Amount / tank.MaxAmount).ToString("P1");
            var resourcesDropdownUpdated = false;
            if(!string.IsNullOrEmpty(tank.TankType)
               && (tankTypeDropdown.value >= tank.SupportedTypes.Count
                   || tank.TankType != tank.SupportedTypes[tankTypeDropdown.value]))
            {
                tankTypeDropdown.SetValueWithoutNotify(tank.SupportedTypes.IndexOf(tank.TankType));
                updateResourcesDropdown();
                resourcesDropdownUpdated = true;
            }
            // ReSharper disable once InvertIf
            if(!string.IsNullOrEmpty(tank.CurrentResource)
               && (resourceDropdown.value >= tank.SupportedResources.Count
                   || tank.CurrentResource != tank.SupportedResources[resourceDropdown.value]))
            {
                if(!resourcesDropdownUpdated)
                    updateResourcesDropdown();
                resourceDropdown.SetValueWithoutNotify(tank.SupportedResources.IndexOf(tank.CurrentResource));
            }
        }

        private void Awake()
        {
            volumeDisplay.SetActive(true);
            volumeEditor.SetActive(false);
            editVolumeButton.onClick.AddListener(showVolumeEditor);
            editMaxAmountButton.onClick.AddListener(showMaxAmountEditor);
            editMaxMassButton.onClick.AddListener(showMaxMassEditor);
            tankTypeDropdown.onValueChanged.AddListener(changeTankType);
            resourceDropdown.onValueChanged.AddListener(changeResource);
            fullTankButton.onClick.AddListener(fillTank);
            emptyTankButton.onClick.AddListener(emptyTank);
            deleteButton.onClick.AddListener(deleteSelf);
        }

        private void OnDestroy()
        {
            editVolumeButton.onClick.RemoveAllListeners();
            editMaxAmountButton.onClick.RemoveAllListeners();
            editMaxMassButton.onClick.RemoveAllListeners();
            tankTypeDropdown.onValueChanged.RemoveAllListeners();
            resourceDropdown.onValueChanged.RemoveAllListeners();
            fullTankButton.onClick.RemoveAllListeners();
            emptyTankButton.onClick.RemoveAllListeners();
            deleteButton.onClick.RemoveAllListeners();
        }

        private void deleteSelf()
        {
            if(!tank.Manager.RemoveTank(tank))
                return;
            if(managerUI != null)
                managerUI.UpdateDisplay();
            else
                Destroy(gameObject);
        }

        private void hideEditor()
        {
            volumeEditor.onDoneEditing.RemoveAllListeners();
            volumeEditor.SetActive(false);
            volumeDisplay.SetActive(true);
        }

        private void showEditor(string units, float value, float max, UnityAction<float> onDone)
        {
            volumeEditor.suffix.text = units;
            volumeEditor.Max = max;
            volumeEditor.SetStep(volumeEditor.Max / 10);
            volumeEditor.SetValueWithoutNotify(value);
            volumeEditor.onDoneEditing.AddListener(onDone);
            volumeDisplay.SetActive(false);
            volumeEditor.SetActive(true);
        }

        private void showVolumeEditor()
        {
            if(tank == null)
                return;
            showEditor(
                "m3",
                tank.Volume,
                tank.Volume + tank.Manager.AvailableVolume,
                changeTankVolume
            );
        }

        private void changeTankVolume(float newVolume)
        {
            if(tank == null)
                return;
            tank.SetVolume(newVolume, true);
            hideEditor();
            UpdateDisplay();
        }

        private void showMaxAmountEditor()
        {
            if(tank == null)
                return;
            showEditor(
                "u",
                (float)tank.MaxAmount,
                tank.ResourceAmountInVolume(tank.Volume + tank.Manager.AvailableVolume),
                changeTankMaxAmount
            );
        }

        private void changeTankMaxAmount(float newMaxAmount)
        {
            if(tank == null)
                return;
            changeTankVolume(tank.VolumeForResourceAmount(newMaxAmount));
        }

        private void showMaxMassEditor()
        {
            if(tank == null)
                return;
            showEditor(
                "t",
                (float)(tank.MaxAmount * tank.ResourceDensity),
                tank.ResourceAmountInVolume(tank.Volume + tank.Manager.AvailableVolume) * tank.ResourceDensity,
                changeTankMaxMass
            );
        }

        private void changeTankMaxMass(float newMaxMass)
        {
            if(tank == null)
                return;
            changeTankMaxAmount(newMaxMass / tank.ResourceDensity);
        }

        private void fillTank()
        {
            if(tank == null)
                return;
            tank.SetAmount((float)tank.MaxAmount);
            UpdateDisplay();
        }

        private void emptyTank()
        {
            if(tank == null)
                return;
            tank.SetAmount(0);
            UpdateDisplay();
        }

        private void changeTankType(int index)
        {
            if(tank == null)
                return;
            tank.ChangeTankType(tank.Manager.SupportedTypes[index]);
            updateResourcesDropdown();
            UpdateDisplay();
        }

        private void changeResource(int index)
        {
            if(tank == null)
                return;
            tank.ChangeResource(tank.SupportedResources[index]);
            UpdateDisplay();
        }
    }
}
