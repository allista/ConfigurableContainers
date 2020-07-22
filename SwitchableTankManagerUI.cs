using System;
using AT_Utils.UI;
using CC.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AT_Utils
{
    public static class CC_UI
    {
        public static readonly UIBundle AssetBundle = UIBundle.Create("ConfigurableContainers/cc_ui.bundle");
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
            Controller.onPointerEnterEvent.AddListener(highlightPart);
            Controller.onPointerExitEvent.AddListener(highlightPartDefault);
            Controller.SetTankManager(manager);
            if(manager != null)
                pos = manager.uiPos;
        }

        private void toggleColors() => Controller.ToggleStylesUI();

        private void highlightPart(PointerEventData _)
        {
            var part = manager?.part;
            if(part != null)
                part.HighlightAlways(Colors.Active.color);
        }

        private void highlightPartDefault(PointerEventData _)
        {
            var part = manager?.part;
            if(part != null)
                part.SetHighlightDefault();
        }

        public override void SyncState()
        {
            base.SyncState();
            if(manager != null)
                manager.uiPos = pos;
        }

        protected override void onClose()
        {
            base.onClose();
            highlightPartDefault(null);
        }

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
            if(FlightGlobals.ready && manager.part != null)
                Controller.gameObject.SetActive(manager.part.vessel == FlightGlobals.ActiveVessel);
            Controller.UpdateDisplay();
        }
    }
}
