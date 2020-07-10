using System;
using System.Collections.Generic;
using AT_Utils.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CC.UI
{
    public class AddTankControl : TankManagerUIPart
    {
        public enum VolumeUnits { CUBIC_METERS, PARTS }

        private static readonly Dictionary<VolumeUnits, string> unitNames = new Dictionary<VolumeUnits, string>
        {
            { VolumeUnits.CUBIC_METERS, "m3" }, { VolumeUnits.PARTS, "%" }
        };

        private static readonly int numUnits = Enum.GetNames(typeof(VolumeUnits)).Length;

        public Dropdown tankTypeDropdown;
        public InputField volumeField;
        public Button unitsSwitchButton, maxVolumeButton, halfVolumeButton, addButton;
        public Text unitsLabel;
        public Colorizer volumeFieldColorizer;
        public TooltipTrigger volumeFieldTooltip;
        public TooltipTrigger
            tankTypeTooltip;


        public ITankManager tankManager;
        public VolumeUnits currentUnits = VolumeUnits.CUBIC_METERS;

        public void SetTankManager(ITankManager newTankManager)
        {
            if(newTankManager == tankManager)
                return;
            tankManager = newTankManager;
            if(tankManager == null)
                return;
            updateTankTypes();
        }

        private void Awake()
        {
            updateUnitsLabel();
            tankTypeDropdown.onValueChanged.AddListener(updateTankTypeDropdownTooltip);
            volumeField.onValueChanged.AddListener(onVolumeChange);
            unitsSwitchButton.onClick.AddListener(onUnitsSwitch);
            maxVolumeButton.onClick.AddListener(setMaxVolume);
            halfVolumeButton.onClick.AddListener(setHalfVolume);
            addButton.onClick.AddListener(addTank);
            volumeNotOk("Enter the volume to create a new tank");
        }

        private void OnDestroy()
        {
            tankTypeDropdown.onValueChanged.RemoveAllListeners();
            volumeField.onValueChanged.RemoveAllListeners();
            unitsSwitchButton.onClick.RemoveAllListeners();
            maxVolumeButton.onClick.RemoveAllListeners();
            halfVolumeButton.onClick.RemoveAllListeners();
            addButton.onClick.RemoveAllListeners();
        }

        private void onUnitsSwitch()
        {
            var oldUnits = currentUnits;
            currentUnits = (VolumeUnits)(((int)currentUnits + 1) % numUnits);
            updateUnitsLabel();
            if(string.IsNullOrEmpty(volumeField.text) || !float.TryParse(volumeField.text, out var volume))
                return;
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch(currentUnits)
            {
                case VolumeUnits.CUBIC_METERS when oldUnits == VolumeUnits.PARTS:
                    setVolume(volume / 100);
                    break;
                case VolumeUnits.PARTS when oldUnits == VolumeUnits.CUBIC_METERS:
                    setVolume(tankManager.AvailableVolume > 0
                        ? volume / tankManager.AvailableVolume
                        : 0);
                    break;
            }
        }

        private void updateUnitsLabel()
        {
            unitsLabel.text = unitNames[currentUnits];
        }

        private void updateTankTypes()
        {
            tankTypeDropdown.options = UI_Utils.namesToOptions(tankManager.SupportedTypes);
            updateTankTypeDropdownTooltip(tankTypeDropdown.value);
        }

        private void updateTankTypeDropdownTooltip(int index) =>
            tankTypeTooltip.SetText(tankManager.GetTypeInfo(tankManager.SupportedTypes[index]));

        private void setMaxVolume() => setVolume(1, true);

        private void setHalfVolume() => setVolume(0.5f, true);

        private void setVolume(float part, bool updateState = false)
        {
            volumeField.SetTextWithoutNotify(currentUnits == VolumeUnits.CUBIC_METERS
                ? (tankManager.AvailableVolume * part).ToString("R")
                : (part * 100).ToString("R"));
            if(!updateState)
                return;
            if(tankManager.AvailableVolume > 0)
                volumeOk();
            else
                volumeNotOk("No free space left");
        }


        private void volumeNotOk(string error)
        {
            volumeFieldColorizer.SetColor(Colors.Danger);
            volumeFieldTooltip.SetText(error);
            addButton.SetInteractable(false);
        }

        private void volumeOk()
        {
            volumeFieldColorizer.SetColor(Colors.Neutral);
            volumeFieldTooltip.SetText("Volume of the new tank");
            addButton.SetInteractable(true);
        }

        private void onVolumeChange(string value)
        {
            if(!float.TryParse(value, out var newValue))
            {
                volumeNotOk("Entered value is not a number");
                return;
            }
            if(newValue <= 0)
            {
                volumeNotOk("Enter positive number");
                return;
            }
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch(currentUnits)
            {
                case VolumeUnits.CUBIC_METERS:
                    if(newValue > tankManager.AvailableVolume)
                    {
                        volumeNotOk("Entered volume is greater than the available volume");
                        return;
                    }
                    break;
                case VolumeUnits.PARTS:
                    var maxParts = tankManager.AvailableVolumePercent;
                    if(newValue > maxParts)
                    {
                        volumeNotOk("Entered volume is greater than the available volume");
                        return;
                    }
                    break;
            }
            volumeOk();
        }

        private void addTank()
        {
            if(!float.TryParse(volumeField.text, out var tankVolume))
                return;
            var tankType = tankManager.SupportedTypes[tankTypeDropdown.value];
            if(currentUnits == VolumeUnits.PARTS)
                tankVolume = Mathf.Clamp(tankManager.Volume * tankVolume / 100,
                    0,
                    tankManager.AvailableVolume);
            if(!tankManager.AddTank(tankType, tankVolume))
                return;
            volumeField.SetTextWithoutNotify("");
            addButton.SetInteractable(false);
            managerUI.UpdateDisplay();
        }
    }
}
