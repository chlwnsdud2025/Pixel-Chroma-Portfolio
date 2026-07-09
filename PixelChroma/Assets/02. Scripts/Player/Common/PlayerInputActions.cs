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
        private const string ActionExposeSkill = "ExposeSkill";
        private const string ActionCatch = "Catch";
        private const string ActionToggleFreeCam = "ToggleFreeCam";
        private const string ActionSpectateRunner = "SpectateRunner";
        private const string ActionSpectateSeeker = "SpectateSeeker";
        private const string ActionCyclePrev = "CyclePrev";
        private const string ActionCycleNext = "CycleNext";

        private readonly InputActionMap _map;

        public InputAction Move { get; }
        public InputAction TogglePaint { get; }
        public InputAction SelectBrush { get; }
        public InputAction SelectEraser { get; }
        public InputAction SelectEyedropper { get; }
        public InputAction ZoomModifier { get; }
        public InputAction ExposeSkill { get; }
        public InputAction Catch { get; }
        public InputAction ToggleFreeCam { get; }
        public InputAction SpectateRunner { get; }
        public InputAction SpectateSeeker { get; }
        public InputAction CyclePrev { get; }
        public InputAction CycleNext { get; }

        public PlayerInputActions(InputActionAsset asset)
        {
            _map = asset.FindActionMap(ActionMapName);
            Move = _map.FindAction(ActionMove);
            TogglePaint = _map.FindAction(ActionTogglePaint);
            SelectBrush = _map.FindAction(ActionSelectBrush);
            SelectEraser = _map.FindAction(ActionSelectEraser);
            SelectEyedropper = _map.FindAction(ActionSelectEyedropper);
            ZoomModifier = _map.FindAction(ActionZoomModifier);
            ExposeSkill = _map.FindAction(ActionExposeSkill);
            Catch = _map.FindAction(ActionCatch);
            ToggleFreeCam = _map.FindAction(ActionToggleFreeCam);
            SpectateRunner = _map.FindAction(ActionSpectateRunner);
            SpectateSeeker = _map.FindAction(ActionSpectateSeeker);
            CyclePrev = _map.FindAction(ActionCyclePrev);
            CycleNext = _map.FindAction(ActionCycleNext);
        }

        public void EnableAll() => _map.Enable();
        public void DisableAll() => _map.Disable();
    }
}
