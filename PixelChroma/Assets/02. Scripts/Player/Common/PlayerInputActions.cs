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
        private const string ActionBrushSizeModifier = "BrushSizeModifier";
        private const string ActionExposeSkill = "ExposeSkill";
        private const string ActionCatch = "Catch";
        private const string ActionToggleSpectate = "ToggleSpectate";
        private const string ActionSpectateSeeker = "SpectateSeeker";
        private const string ActionSpectateRunner = "SpectateRunner";
        private const string ActionSpectateFree = "SpectateFree";
        private const string ActionCyclePrev = "CyclePrev";
        private const string ActionCycleNext = "CycleNext";
        private const string ActionUndo = "Undo";
        private const string ActionRedo = "Redo";
        private const string ActionChatToggle = "ChatToggle";
        private const string ActionCancel = "Cancel";
        private const string ActionLike = "Like";
        private const string ActionToggleLikeDisplay = "ToggleLikeDisplay";
        private const string ActionRadialMenu = "RadialMenu";
        private readonly InputActionMap _map;

        public InputAction Move { get; }
        public InputAction TogglePaint { get; }
        public InputAction SelectBrush { get; }
        public InputAction SelectEraser { get; }
        public InputAction SelectEyedropper { get; }
        public InputAction BrushSizeModifier { get; }
        public InputAction ExposeSkill { get; }
        public InputAction Catch { get; }
        public InputAction ToggleSpectate { get; }
        public InputAction SpectateSeeker { get; }
        public InputAction SpectateRunner { get; }
        public InputAction SpectateFree { get; }
        public InputAction CyclePrev { get; }
        public InputAction CycleNext { get; }
        public InputAction Undo { get; }
        public InputAction Redo { get; }
        public InputAction ChatToggle { get; }
        public InputAction Cancel { get; }
        public InputAction Like { get; }
        public InputAction ToggleLikeDisplay { get; }
        public InputAction RadialMenu { get; }

        public PlayerInputActions(InputActionAsset asset)
        {
            _map = asset.FindActionMap(ActionMapName);
            Move = _map.FindAction(ActionMove);
            TogglePaint = _map.FindAction(ActionTogglePaint);
            SelectBrush = _map.FindAction(ActionSelectBrush);
            SelectEraser = _map.FindAction(ActionSelectEraser);
            SelectEyedropper = _map.FindAction(ActionSelectEyedropper);
            BrushSizeModifier = _map.FindAction(ActionBrushSizeModifier);
            ExposeSkill = _map.FindAction(ActionExposeSkill);
            Catch = _map.FindAction(ActionCatch);
            ToggleSpectate = _map.FindAction(ActionToggleSpectate);
            SpectateSeeker = _map.FindAction(ActionSpectateSeeker);
            SpectateRunner = _map.FindAction(ActionSpectateRunner);
            SpectateFree = _map.FindAction(ActionSpectateFree);
            CyclePrev = _map.FindAction(ActionCyclePrev);
            CycleNext = _map.FindAction(ActionCycleNext);
            Undo = _map.FindAction(ActionUndo);
            Redo = _map.FindAction(ActionRedo);
            ChatToggle = _map.FindAction(ActionChatToggle);
            Cancel = _map.FindAction(ActionCancel);
            Like = _map.FindAction(ActionLike);
            ToggleLikeDisplay = _map.FindAction(ActionToggleLikeDisplay);
            RadialMenu = _map.FindAction(ActionRadialMenu);
        }

        public void EnableAll() => _map.Enable();
        public void DisableAll() => _map.Disable();
    }
}
