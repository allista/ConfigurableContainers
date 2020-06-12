using System.Collections.Generic;
using System.Linq;
using AT_Utils.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CC.UI
{
    public interface ITankInfo
    {
        IList<string> AllTankTypeNames { get; }
        string TankTypeName { get; }

        IList<string> AllResourceNames { get; }
        string ResourceName { get; }
        float UsefulVolumeRatio { get; }

        float TotalVolume { get; }
        float Volume { get; }

        float MaxAmount { get; }
        float Amount { get; }
        float UnitsPerVolume { get; }
        float Density { get; }

        void SetVolume(float newVolume);
        void ChangeTankType(string tankTypeName);
        void ChangeResource(string resourceName);
        void SetAmount(float newAmount);
    }

    public class TankControlsUI : MonoBehaviour
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

        public void SetTank(ITankInfo newTank)
        {
            tank = newTank;
            if(tank == null)
                return;
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            resourceVolume.text = FormatUtils.formatVolume(tank.Volume);
            resourceMaxAmount.text = FormatUtils.formatBigValue(tank.MaxAmount, "u");
            resourceAmount.text = FormatUtils.formatBigValue(tank.Amount, "u");
            if(tank.Density > 0)
            {
                resourceMaxMass.gameObject.SetActive(true);
                resourceMass.gameObject.SetActive(true);
                resourceMaxMass.text = FormatUtils.formatBigValue(tank.MaxAmount * tank.Density, "t");
                resourceMass.text = FormatUtils.formatBigValue(tank.Amount * tank.Density, "t");
            }
            else
            {
                resourceMaxMass.gameObject.SetActive(false);
                resourceMass.gameObject.SetActive(false);
            }
            tankFullness.text = (tank.Amount / tank.MaxAmount).ToString("P1");
            tankTypeDropdown.options = namesToOptions(tank.AllTankTypeNames);
            resourceDropdown.options = namesToOptions(tank.AllResourceNames);
            tankTypeDropdown.SetValueWithoutNotify(tank.AllTankTypeNames.IndexOf(tank.TankTypeName));
            resourceDropdown.SetValueWithoutNotify(tank.AllResourceNames.IndexOf(tank.ResourceName));
        }

        private void Awake()
        {
            volumeDisplay.SetActive(true);
            volumeEditor.SetActive(false);
            volumeEditor.doneButton.onClick.AddListener(hideEditor);
            editVolumeButton.onClick.AddListener(showVolumeEditor);
            editMaxAmountButton.onClick.AddListener(showMaxAmountEditor);
            editMaxMassButton.onClick.AddListener(showMaxMassEditor);
            tankTypeDropdown.onValueChanged.AddListener(changeTankType);
            resourceDropdown.onValueChanged.AddListener(changeResource);
            fullTankButton.onClick.AddListener(fillTank);
            emptyTankButton.onClick.AddListener(emptyTank);
        }

#if DEBUG
        private void Start()
        {
            SetTank(new TestTankInfo());
        }
#endif

        private void OnDestroy()
        {
            volumeEditor.doneButton.onClick.RemoveAllListeners();
            editVolumeButton.onClick.RemoveAllListeners();
            editMaxAmountButton.onClick.RemoveAllListeners();
            editMaxMassButton.onClick.RemoveAllListeners();
            tankTypeDropdown.onValueChanged.RemoveAllListeners();
            resourceDropdown.onValueChanged.RemoveAllListeners();
            fullTankButton.onClick.RemoveAllListeners();
            emptyTankButton.onClick.RemoveAllListeners();
        }

        private static List<Dropdown.OptionData> namesToOptions(IEnumerable<string> names) =>
            names.Select(name =>
                    new Dropdown.OptionData(FormatUtils.ParseCamelCase(name)))
                .ToList();

        private void hideEditor()
        {
            volumeEditor.onValueChanged.RemoveAllListeners();
            volumeEditor.SetActive(false);
            volumeDisplay.SetActive(true);
        }

        private void showEditor()
        {
            volumeDisplay.SetActive(false);
            volumeEditor.SetActive(true);
        }

        private void showVolumeEditor()
        {
            if(tank == null)
                return;
            volumeEditor.suffix.text = "m3";
            volumeEditor.Max = tank.TotalVolume;
            volumeEditor.SetStep(volumeEditor.Max / 10);
            volumeEditor.SetValueWithoutNotify(tank.Volume);
            volumeEditor.onValueChanged.AddListener(changeTankVolume);
            showEditor();
        }

        private void changeTankVolume(float newVolume)
        {
            if(tank == null)
                return;
            tank.SetVolume(newVolume);
            UpdateDisplay();
        }

        private void showMaxAmountEditor()
        {
            if(tank == null)
                return;
            volumeEditor.suffix.text = "u";
            volumeEditor.Max = tank.TotalVolume * tank.UnitsPerVolume * tank.UsefulVolumeRatio;
            volumeEditor.SetStep(volumeEditor.Max / 10);
            volumeEditor.SetValueWithoutNotify(tank.MaxAmount);
            volumeEditor.onValueChanged.AddListener(changeTankMaxAmount);
            showEditor();
        }

        private void changeTankMaxAmount(float newMaxAmount)
        {
            if(tank == null)
                return;
            changeTankVolume(newMaxAmount / tank.UnitsPerVolume / tank.UsefulVolumeRatio);
        }

        private void showMaxMassEditor()
        {
            if(tank == null)
                return;
            volumeEditor.suffix.text = "t";
            volumeEditor.Max = tank.TotalVolume * tank.UnitsPerVolume * tank.UsefulVolumeRatio * tank.Density;
            volumeEditor.SetStep(volumeEditor.Max / 10);
            volumeEditor.SetValueWithoutNotify(tank.MaxAmount * tank.Density);
            volumeEditor.onValueChanged.AddListener(changeTankMaxMass);
            showEditor();
        }

        private void changeTankMaxMass(float newMaxMass)
        {
            if(tank == null)
                return;
            changeTankMaxAmount(newMaxMass / tank.Density);
        }

        private void fillTank()
        {
            if(tank == null)
                return;
            tank.SetAmount(tank.MaxAmount);
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
            tank.ChangeTankType(tank.AllTankTypeNames[index]);
            UpdateDisplay();
        }

        private void changeResource(int index)
        {
            if(tank == null)
                return;
            tank.ChangeResource(tank.AllResourceNames[index]);
            UpdateDisplay();
        }
    }
}
