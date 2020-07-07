using System.Collections.Generic;
using AT_Utils.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CC.UI
{
    public interface ITankManager
    {
        string Title { get; }
        IList<string> AllTankTypeNames { get; }
        float TotalVolume { get; }
        float AvailableVolume { get; }
        float AvailableVolumePercent { get; }
        IList<ITankInfo> Tanks { get; }

        ITankInfo AddTank(string tankType, float volume);
        bool RemoveTank(ITankInfo tank);
    }

    public class TankManagerUI : ScreenBoundRect
    {
        private ITankManager tankManager;

        public AddTankControl addTankControl;
        public Text partTitleLabel, volumeLabel;
        public Button closeButton;
        public RectTransform tanksScroll;
        public GameObject tankControlPrefab;

        public void SetTankManager(ITankManager newTankManager)
        {
            tankManager = newTankManager;
            addTankControl.SetTankManager(tankManager);
            if(tankManager == null)
                return;
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            partTitleLabel.text = tankManager.Title;
            volumeLabel.text =
                $"{FormatUtils.formatVolume(tankManager.AvailableVolume)} / {FormatUtils.formatVolume(tankManager.TotalVolume)}";
            updateTankControls();
        }

        private void updateTankControls()
        {
            var existingTanks = new Dictionary<ITankInfo, TankControlsUI>(tanksScroll.childCount);
            for(var i = tanksScroll.childCount - 1; i >= 0; i--)
            {
                var child = tanksScroll.GetChild(i);
                var tankControl = child.GetComponent<TankControlsUI>();
                if(tankControl == null
                   || tankControl.Tank == null)
                    continue;
                existingTanks[tankControl.Tank] = tankControl;
            }
            var newTanks = new HashSet<ITankInfo>();
            foreach(var tank in tankManager.Tanks)
            {
                newTanks.Add(tank);
                if(existingTanks.ContainsKey(tank))
                    continue;
                var newTankControlObj = Instantiate(tankControlPrefab, tanksScroll);
                if(newTankControlObj == null)
                    continue;
                newTankControlObj.transform.SetAsLastSibling();
                var newTankControl = newTankControlObj.GetComponent<TankControlsUI>();
                if(newTankControl == null)
                    continue;
                newTankControl.managerUI = this;
                newTankControl.SetTank(tank);
            }
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach(var tank in existingTanks)
            {
                if(newTanks.Contains(tank.Key))
                    continue;
                Destroy(tank.Value.gameObject);
            }
        }

#if DEBUG
        private void Start()
        {
            SetTankManager(new TestTankManager());
        }
#endif
    }

    public class TankManagerUIPart : MonoBehaviour
    {
        public TankManagerUI managerUI;
    }
}
