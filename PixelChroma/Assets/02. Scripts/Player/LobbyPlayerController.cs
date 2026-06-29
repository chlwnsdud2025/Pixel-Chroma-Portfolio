using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

namespace PixelChroma
{
    public class LobbyPlayerController : NetworkBehaviour
    {
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");

        [Header("Input")]
        public InputActionAsset inputActions;

        [Header("References")]
        public Animator animator;
        public PlayerPaintCanvas paintCanvas;

        [Header("Settings")]
        public float moveSpeed = 3f;

        private InputAction moveAction;
        private UIInGameHUD _hud;
        private bool _isPaintMode;

        public override void OnStartLocalPlayer()
        {
            var map = inputActions.FindActionMap("PlayerControll");
            moveAction = map.FindAction("Move");
            moveAction.Enable();

            ShowHUDAsync().Forget();
        }

        public override void OnStopLocalPlayer()
        {
            moveAction?.Disable();

            if (_hud != null && _hud.ColorPicker != null)
                _hud.ColorPicker.OnBrushSizeChanged -= OnBrushSizeChanged;
        }

        private async UniTaskVoid ShowHUDAsync()
        {
            _hud = await UIManager.Instance.ShowScene<UIInGameHUD>();

            if (_hud != null && _hud.ColorPicker != null)
            {
                _hud.ColorPicker.OnBrushSizeChanged += OnBrushSizeChanged;
                if (paintCanvas != null)
                    paintCanvas.BrushSize = _hud.ColorPicker.BrushSize;
            }
        }

        private void OnBrushSizeChanged(int size)
        {
            if (paintCanvas != null)
                paintCanvas.BrushSize = size;
        }

        void Update()
        {
            if (!isLocalPlayer) return;

            if (Keyboard.current != null && Keyboard.current[Key.F].wasPressedThisFrame)
                TogglePaintMode();

            if (_isPaintMode)
            {
                HandlePainting();
                return;
            }

            HandleMovement();
        }

        private void HandleMovement()
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            bool moving = input.sqrMagnitude > 0.01f;

            if (moving)
            {
                Vector3 move = new Vector3(input.x, input.y, 0).normalized;
                transform.position += move * moveSpeed * Time.deltaTime;
            }

            animator.SetBool(IsMoving, moving);
            if (moving)
            {
                animator.SetFloat(MoveX, input.x);
                animator.SetFloat(MoveY, input.y);
            }
        }

        private void TogglePaintMode()
        {
            _isPaintMode = !_isPaintMode;
            _hud?.SetPaintMode(_isPaintMode);
        }

        private void HandlePainting()
        {
            if (paintCanvas == null || Mouse.current == null) return;
            if (!Mouse.current.leftButton.isPressed) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            worldPos.z = 0;

            Color color = _hud != null && _hud.ColorPicker != null
                ? _hud.ColorPicker.SelectedColor
                : Color.white;

            Vector2Int pixel = paintCanvas.WorldToPixel(worldPos);
            if (paintCanvas.IsInBounds(pixel.x, pixel.y))
                paintCanvas.PaintAt(pixel.x, pixel.y, color);
        }
    }
}
