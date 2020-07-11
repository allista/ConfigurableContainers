using System.Collections.Generic;
using System.Linq;
using AT_Utils.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CC.UI
{
    public class TankManagerUI : ScreenBoundRect
    {
        public Button
            closeButton,
            colorSettingsButton;

        private ITankManager tankManager;

        public AddTankControl addTankControl;
        public TankConfigsControl tankConfigsControl;
        public Text partTitleLabel, volumeLabel;
        public RectTransform tanksScroll;
        public GameObject tankControlPrefab;
        public Dictionary<ITankInfo, TankControlsUI> tankControls = new Dictionary<ITankInfo, TankControlsUI>();

        public void SetTankManager(ITankManager newTankManager)
        {
            if(newTankManager == tankManager)
                return;
            foreach(var t in tankControls.Values)
                Destroy(t.gameObject);
            tankControls.Clear();
            tankManager = newTankManager;
            addTankControl.SetTankManager(tankManager);
            tankConfigsControl.SetTankManager(tankManager);
            if(tankManager == null)
                return;
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            partTitleLabel.text = tankManager.Title;
            volumeLabel.text =
                $"{FormatUtils.formatVolume(tankManager.AvailableVolume)} / {FormatUtils.formatVolume(tankManager.Volume)}";
            updateTankControls();
        }

        private void updateTankControls()
        {
            var newTanks = new HashSet<ITankInfo>();
            foreach(var tank in tankManager.Tanks)
            {
                newTanks.Add(tank);
                if(tankControls.ContainsKey(tank))
                    continue;
                var newTankControlObj = Instantiate(tankControlPrefab, tanksScroll);
                if(newTankControlObj == null)
                {
                    Debug.LogError($"Unable to instantiate prefab: {tankControlPrefab}");
                    continue;
                }
                newTankControlObj.transform.SetAsLastSibling();
                var newTankControl = newTankControlObj.GetComponent<TankControlsUI>();
                if(newTankControl == null)
                {
                    Debug.LogError($"No {nameof(TankControlsUI)} in prefab {tankControlPrefab}");
                    continue;
                }
                newTankControl.managerUI = this;
                newTankControl.SetTank(tank);
                tankControls.Add(tank, newTankControl);
            }
            foreach(var tank in tankControls.ToList())
            {
                if(newTanks.Contains(tank.Key))
                {
                    tank.Value.UpdateDisplay();
                    continue;
                }
                Destroy(tank.Value.gameObject);
                tankControls.Remove(tank.Key);
            }
            tankConfigsControl.EnableControls(tankManager.Tanks.Count > 0);
        }

#if DEBUG
        protected override void Start()
        {
            base.Start();
            if(Application.isEditor)
                SetTankManager(new TestTankManager());
        }
#endif
    }

    public class TankManagerUIPart : MonoBehaviour
    {
        public TankManagerUI managerUI;
    }
}
