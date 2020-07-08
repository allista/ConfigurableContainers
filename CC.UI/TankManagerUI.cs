using System.Collections.Generic;
using AT_Utils.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CC.UI
{
    public class TankManagerUI : ScreenBoundRect
    {
        private ITankManager tankManager;

        public AddTankControl addTankControl;
        public Text partTitleLabel, volumeLabel;
        public Button closeButton;
        public RectTransform tanksScroll;
        public GameObject tankControlPrefab;
        public List<TankControlsUI> tankControls = new List<TankControlsUI>();

        public void SetTankManager(ITankManager newTankManager)
        {
            tankControls.ForEach(t => Destroy(t.gameObject));
            tankControls.Clear();
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
                $"{FormatUtils.formatVolume(tankManager.AvailableVolume)} / {FormatUtils.formatVolume(tankManager.Volume)}";
            updateTankControls();
        }

        private void updateTankControls()
        {
            var existingTanks = new Dictionary<ITankInfo, TankControlsUI>(tankControls.Count);
            for(var i = tankControls.Count - 1; i >= 0; i--)
            {
                var tankControl = tankControls[i];
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
                tankControls.Add(newTankControl);
            }
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach(var tank in existingTanks)
            {
                if(newTanks.Contains(tank.Key))
                    continue;
                tankControls.Remove(tank.Value);
                Destroy(tank.Value.gameObject);
            }
            tankControls.ForEach(t => t.UpdateDisplay());
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
