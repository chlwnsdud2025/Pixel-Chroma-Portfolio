using UnityEngine;

namespace PixelChroma.UI
{
    public class UIInGameHUD : UIScene, IPaintHUD
    {
        [SerializeField] private GameObject paintModeRoot;
        [SerializeField] private UIColorPicker colorPicker;

        public UIColorPicker ColorPicker => colorPicker;
        public bool IsPaintModeActive => paintModeRoot != null && paintModeRoot.activeSelf;

        public override void Initialize()
        {
            base.Initialize();
            if (paintModeRoot != null)
                paintModeRoot.SetActive(false);
        }

        public void TogglePaintMode()
        {
            if (paintModeRoot != null)
                paintModeRoot.SetActive(!paintModeRoot.activeSelf);
        }

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null)
                paintModeRoot.SetActive(active);
        }
    }
}
