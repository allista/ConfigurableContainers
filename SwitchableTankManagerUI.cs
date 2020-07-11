using System;
using AT_Utils.UI;
using CC.UI;
using UnityEngine;

namespace AT_Utils
{
    public static class CC_UI
    {
        public static readonly UIBundle AssetBundle = new UIBundle("ConfigurableContainers/cc_ui.bundle");
    }

    public class SwitchableTankManagerUI : UIWindowBase<TankManagerUI>
    {
        private readonly SwitchableTankManager manager;

        public SwitchableTankManagerUI(SwitchableTankManager manager) : base(CC_UI.AssetBundle)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        protected override void init_controller()
        {
            base.init_controller();
            Controller.closeButton.onClick.AddListener(Close);
            Controller.colorSettingsButton.onClick.AddListener(toggleColors);
            Controller.SetTankManager(manager);
        }

        private void toggleColors() => Controller.ToggleStylesUI();

        public void Toggle(MonoBehaviour monoBehaviour) => Toggle(monoBehaviour, !manager.EnablePartControls);

        public void OnLateUpdate()
        {
            if(!IsShown)
                return;
            if(manager.EnablePartControls)
            {
                Close();
                return;
            }
            Controller.UpdateDisplay();
        }
    }
}
