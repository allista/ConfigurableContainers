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

        public ITankInfo Tank { get; private set; }

        public void SetTank(ITankInfo newTank)
        {
            Tank = newTank;
            if(Tank == null)
                return;
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            resourceVolume.text = FormatUtils.formatVolume(Tank.Volume);
            resourceMaxAmount.text = FormatUtils.formatBigValue((float)Tank.MaxAmount, "u");
            resourceAmount.text = FormatUtils.formatBigValue((float)Tank.Amount, "u");
            if(Tank.ResourceDensity > 0)
            {
                editMaxMassButton.gameObject.SetActive(true);
                resourceMass.gameObject.SetActive(true);
                resourceMaxMass.text = FormatUtils.formatMass((float)(Tank.MaxAmount * Tank.ResourceDensity));
                resourceMass.text = FormatUtils.formatMass((float)(Tank.Amount * Tank.ResourceDensity));
            }
            else
            {
                editMaxMassButton.gameObject.SetActive(false);
                resourceMass.gameObject.SetActive(false);
            }
            tankFullness.text = (Tank.Amount / Tank.MaxAmount).ToString("P1");
            tankTypeDropdown.options = UI_Utils.namesToOptions(Tank.SupportedTypes);
            resourceDropdown.options = UI_Utils.namesToOptions(Tank.SupportedResources);
            if(!string.IsNullOrEmpty(Tank.TankType))
                tankTypeDropdown.SetValueWithoutNotify(Tank.SupportedTypes.IndexOf(Tank.TankType));
            if(!string.IsNullOrEmpty(Tank.CurrentResource))
                resourceDropdown.SetValueWithoutNotify(Tank.SupportedResources.IndexOf(Tank.CurrentResource));
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
            if(!Tank.Manager.RemoveTank(Tank))
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
            if(Tank == null)
                return;
            showEditor(
                "m3",
                Tank.Volume,
                Tank.Volume + Tank.Manager.AvailableVolume,
                changeTankVolume
            );
        }

        private void changeTankVolume(float newVolume)
        {
            if(Tank == null)
                return;
            Tank.SetVolume(newVolume, true);
            hideEditor();
            UpdateDisplay();
        }

        private void showMaxAmountEditor()
        {
            if(Tank == null)
                return;
            showEditor(
                "u",
                (float)Tank.MaxAmount,
                Tank.ResourceAmountInVolume(Tank.Volume + Tank.Manager.AvailableVolume),
                changeTankMaxAmount
            );
        }

        private void changeTankMaxAmount(float newMaxAmount)
        {
            if(Tank == null)
                return;
            changeTankVolume(Tank.VolumeForResourceAmount(newMaxAmount));
        }

        private void showMaxMassEditor()
        {
            if(Tank == null)
                return;
            showEditor(
                "t",
                (float)(Tank.MaxAmount * Tank.ResourceDensity),
                Tank.ResourceAmountInVolume(Tank.Volume + Tank.Manager.AvailableVolume) * Tank.ResourceDensity,
                changeTankMaxMass
            );
        }

        private void changeTankMaxMass(float newMaxMass)
        {
            if(Tank == null)
                return;
            changeTankMaxAmount(newMaxMass / Tank.ResourceDensity);
        }

        private void fillTank()
        {
            if(Tank == null)
                return;
            Tank.SetAmount((float)Tank.MaxAmount);
            UpdateDisplay();
        }

        private void emptyTank()
        {
            if(Tank == null)
                return;
            Tank.SetAmount(0);
            UpdateDisplay();
        }

        private void changeTankType(int index)
        {
            if(Tank == null)
                return;
            Tank.ChangeTankType(Tank.Manager.SupportedTypes[index]);
            UpdateDisplay();
        }

        private void changeResource(int index)
        {
            if(Tank == null)
                return;
            Tank.ChangeResource(Tank.SupportedResources[index]);
            UpdateDisplay();
        }
    }
}
