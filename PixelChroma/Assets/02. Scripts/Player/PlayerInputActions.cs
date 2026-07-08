using UnityEngine.InputSystem;

namespace PixelChroma
{
    public class PlayerInputActions
    {
        private const string ActionMapName = "PlayerControll";
        private const string ActionMove = "Move";
        private const string ActionTogglePaint = "TogglePaint";
        private const string ActionSelectBrush = "SelectBrush";
        private const string ActionSelectEraser = "SelectEraser";
        private const string ActionSelectEyedropper = "SelectEyedropper";
        private const string ActionZoomModifier = "ZoomModifier";

        private readonly InputActionMap _map;

        public InputAction Move { get; }
        public InputAction TogglePaint { get; }
        public InputAction SelectBrush { get; }
        public InputAction SelectEraser { get; }
        public InputAction SelectEyedropper { get; }
        public InputAction ZoomModifier { get; }

        public PlayerInputActions(InputActionAsset asset)
        {
            _map = asset.FindActionMap(ActionMapName);
            Move = _map.FindAction(ActionMove);
            TogglePaint = _map.FindAction(ActionTogglePaint);
            SelectBrush = _map.FindAction(ActionSelectBrush);
            SelectEraser = _map.FindAction(ActionSelectEraser);
            SelectEyedropper = _map.FindAction(ActionSelectEyedropper);
            ZoomModifier = _map.FindAction(ActionZoomModifier);
        }

        public void EnableAll() => _map.Enable();
        public void DisableAll() => _map.Disable();
    }
}
