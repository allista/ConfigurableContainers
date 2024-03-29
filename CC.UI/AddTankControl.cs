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

        public Dropdown
            tankTypeDropdown;

        public TooltipTrigger
            tankTypeTooltip;

        public InputField
            volumeField;

        public Button
            unitsSwitchButton,
            maxVolumeButton,
            halfVolumeButton,
            addButton;

        public Text
            unitsLabel;

        public Colorizer
            volumeFieldColorizer;

        public TooltipTrigger
            volumeFieldTooltip;

        public ITankManager tankManager;
        public VolumeUnits currentUnits = VolumeUnits.CUBIC_METERS;

        public void SetTankManager(ITankManager newTankManager)
        {
            if(newTankManager == tankManager)
                return;
            tankManager = newTankManager;
            if(tankManager == null)
                return;
            UpdateTankTypes();
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

        private float partsToVolume(float value) => tankManager.AvailableVolume * value;

        private float volumeToParts(float value) =>
            tankManager.AvailableVolume > 0
                ? value / tankManager.AvailableVolume
                : 0;

        private string tankType => tankManager.SupportedTypes[tankTypeDropdown.value];

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
                    setVolume(volumeToParts(volume));
                    break;
            }
        }

        private void updateUnitsLabel()
        {
            unitsLabel.text = unitNames[currentUnits];
        }

        public void UpdateTankTypes()
        {
            if(tankManager == null)
                return;
            tankTypeDropdown.SetOptionsSafe(UI_Utils.namesToOptions(tankManager.SupportedTypes));
            updateTankTypeDropdownTooltip(tankTypeDropdown.value);
        }

        private void updateTankTypeDropdownTooltip(int index) =>
            tankTypeTooltip.SetText(tankManager.GetTypeInfo(tankManager.SupportedTypes[index]));

        private void setMaxVolume() => setVolume(1, true);

        private void setHalfVolume() => setVolume(0.5f, true);

        private void setVolume(float part, bool updateState = false)
        {
            var newVolume = currentUnits == VolumeUnits.CUBIC_METERS
                ? partsToVolume(part)
                : part * 100;
            volumeField.SetTextWithoutNotify(newVolume.ToString("G9"));
            if(!updateState)
                return;
            if(tankManager.AvailableVolume > 0)
                volumeOk(tankManager.OnVolumeChanged(tankType,
                    currentUnits == VolumeUnits.CUBIC_METERS
                        ? newVolume
                        : partsToVolume(part)));
            else
                volumeNotOk("No free space left");
        }


        private void volumeNotOk(string error)
        {
            volumeFieldColorizer.SetColor(Colors.Danger);
            volumeFieldTooltip.SetText(error);
            addButton.SetInteractable(false);
        }

        private void volumeOk(string tooltip = null)
        {
            volumeFieldColorizer.SetColor(Colors.Neutral);
            volumeFieldTooltip.SetText(tooltip ?? "Volume of the new tank");
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
            string info = null;
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch(currentUnits)
            {
                case VolumeUnits.CUBIC_METERS:
                    if(newValue > tankManager.AvailableVolume)
                    {
                        volumeNotOk("Entered volume is greater than the available volume");
                        return;
                    }
                    info = tankManager.OnVolumeChanged(tankType, newValue);
                    break;
                case VolumeUnits.PARTS:
                    if(newValue > 100)
                    {
                        volumeNotOk("Entered volume is greater than the available volume");
                        return;
                    }
                    info = tankManager.OnVolumeChanged(tankType, partsToVolume(newValue / 100));
                    break;
            }
            volumeOk(info);
        }

        private void addTank()
        {
            if(!float.TryParse(volumeField.text, out var tankVolume))
                return;
            if(currentUnits == VolumeUnits.PARTS)
                tankVolume = Mathf.Clamp(partsToVolume(tankVolume / 100),
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
