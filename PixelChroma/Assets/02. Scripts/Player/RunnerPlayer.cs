using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

namespace PixelChroma
{
    public class RunnerPlayer : PlayerBase
    {
        [Header("Runner References")]
        public SpriteRenderer spriteRenderer;
        public PlayerPaintCanvas paintCanvas;

        private UIInGameHUD _hud;
        private bool _isPaintMode;

        protected override void OnLocalPlayerStarted()
        {
            moveSpeed = 5f;
            ShowHUDAsync().Forget();
        }

        protected override void OnLocalPlayerStopped()
        {
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

        protected override void Update()
        {
            if (!isLocalPlayer) return;

            if (Keyboard.current != null && Keyboard.current[Key.F].wasPressedThisFrame)
                TogglePaintMode();

            HandleScrollWheel();

            if (_isPaintMode)
            {
                HandlePainting();
                return;
            }

            HandleMovement();
        }

        private void HandleScrollWheel()
        {
            float scroll = GetScrollValue();
            if (Mathf.Approximately(scroll, 0f)) return;

            if (IsAltHeld())
            {
                HandleZoom();
            }
            else if (_isPaintMode && paintCanvas != null)
            {
                int current = paintCanvas.BrushSize;
                int next = scroll > 0 ? current + 1 : current - 1;
                next = Mathf.Clamp(next, 1, GetMaxBrushSize());
                paintCanvas.BrushSize = next;
                SyncBrushSizeToUI(next);
            }
        }

        private int GetMaxBrushSize()
        {
            if (_hud != null && _hud.ColorPicker != null && _hud.ColorPicker.MaxBrushSize > 0)
                return _hud.ColorPicker.MaxBrushSize;
            return 20;
        }

        private void SyncBrushSizeToUI(int size)
        {
            if (_hud != null && _hud.ColorPicker != null)
                _hud.ColorPicker.SetBrushSize(size);
        }

        private void TogglePaintMode()
        {
            _isPaintMode = !_isPaintMode;
            _hud?.SetPaintMode(_isPaintMode);

            if (_isPaintMode && animator != null)
                animator.SetBool(IsMoving, false);
        }

        private void HandlePainting()
        {
            if (paintCanvas == null || Mouse.current == null) return;
            if (!Mouse.current.leftButton.isPressed) return;
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

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
