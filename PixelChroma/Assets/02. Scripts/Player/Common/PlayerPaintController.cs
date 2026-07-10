using System;
using System.Collections;
using PixelChroma.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PixelChroma
{
    public enum BrushMode { Brush, Eraser, Eyedropper, Fill }

    public class PlayerPaintController
    {
        private readonly PlayerBase _owner;
        private readonly PlayerPaintCanvas _paintCanvas;
        private readonly PlayerInputActions _input;

        private UIColorPicker _boundPicker;
        private BrushMode _brushMode = BrushMode.Brush;
        private bool _isPaintMode;

        private Color _eyedropperColor;
        private Texture2D _eyedropperCapture;
        private Coroutine _eyedropperCoroutine;
        private bool _waitForMouseRelease;

        public event Action<bool> OnPaintModeChanged;
        public event Action<int> OnBrushSizeScrolled;

        public bool IsPaintMode => _isPaintMode;
        public BrushMode BrushMode => _brushMode;

        public PlayerPaintController(
            PlayerBase owner,
            PlayerPaintCanvas paintCanvas,
            PlayerInputActions input)
        {
            _owner = owner;
            _paintCanvas = paintCanvas;
            _input = input;
        }

        public void Tick()
        {
            HandleModeInput();

            if (_brushMode == BrushMode.Eyedropper)
            {
                TickEyedropper();
                return;
            }

            HandlePainting();
        }

        public void BindHUD(IPaintHUD hud)
        {
            if (hud == null) return;

            _boundPicker = hud.ColorPicker;
            OnPaintModeChanged += hud.SetPaintMode;

            if (_boundPicker != null)
            {
                OnBrushSizeScrolled += _boundPicker.SetBrushSize;
                _boundPicker.OnBrushSizeChanged += OnUIBrushSizeChanged;
                if (_paintCanvas != null)
                    _paintCanvas.BrushSize = _boundPicker.BrushSize;
            }

            RefreshCursor();
        }

        public void UnbindHUD()
        {
            if (_boundPicker != null)
            {
                OnBrushSizeScrolled -= _boundPicker.SetBrushSize;
                _boundPicker.OnBrushSizeChanged -= OnUIBrushSizeChanged;
            }

            OnPaintModeChanged = null;
            OnBrushSizeScrolled = null;
            _boundPicker = null;
        }

        public void SetBrushMode(BrushMode mode)
        {
            if (_brushMode == mode) return;

            if (_brushMode == BrushMode.Eyedropper)
                StopEyedropperCapture();

            _brushMode = mode;

            if (mode == BrushMode.Eyedropper)
                _eyedropperCoroutine = _owner.StartCoroutine(EyedropperCaptureLoop());

            _boundPicker?.SetActiveTool(mode);
            RefreshCursor();
        }

        public void TogglePaintMode()
        {
            _isPaintMode = !_isPaintMode;
            OnPaintModeChanged?.Invoke(_isPaintMode);
            RefreshCursor();
        }

        public void HandleBrushScroll(float scroll)
        {
            if (_boundPicker == null || _paintCanvas == null) return;

            int max = _boundPicker.MaxBrushSize;
            int current = _paintCanvas.BrushSize;
            int next = scroll > 0 ? current + 1 : current - 1;
            next = Mathf.Clamp(next, 1, max);
            _paintCanvas.BrushSize = next;
            OnBrushSizeScrolled?.Invoke(next);
        }

        public void RefreshCursor()
        {
            if (!_owner.isLocalPlayer || CursorManager.Instance == null) return;

            if (!_isPaintMode)
            {
                CursorManager.Instance.SetCursorType(_owner.GetDefaultCursorType());
                return;
            }

            int brushSize = _boundPicker != null ? _boundPicker.BrushSize : 1;

            switch (_brushMode)
            {
                case BrushMode.Eyedropper:
                    CursorManager.Instance.SetCursorType(CursorType.Eyedropper);
                    break;
                case BrushMode.Eraser:
                    CursorManager.Instance.SetCursorType(CursorType.Eraser, 1f + (brushSize * 0.1f));
                    break;
                case BrushMode.Brush:
                default:
                    CursorManager.Instance.SetCursorType(CursorType.Brush, 1f + (brushSize * 0.1f));
                    break;
            }
        }

        public void Dispose()
        {
            StopEyedropperCapture();
            if (_eyedropperCapture != null)
            {
                UnityEngine.Object.Destroy(_eyedropperCapture);
                _eyedropperCapture = null;
            }
        }

        private void HandleModeInput()
        {
            if (_input.SelectBrush != null && _input.SelectBrush.WasPressedThisFrame())
                SetBrushMode(BrushMode.Brush);
            else if (_input.SelectEraser != null && _input.SelectEraser.WasPressedThisFrame())
                SetBrushMode(BrushMode.Eraser);
            else if (_input.SelectEyedropper != null && _input.SelectEyedropper.WasPressedThisFrame())
                SetBrushMode(BrushMode.Eyedropper);
        }

        private void TickEyedropper()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            _boundPicker?.SetColor(_eyedropperColor);
            _waitForMouseRelease = true;
            SetBrushMode(BrushMode.Brush);
        }

        private void HandlePainting()
        {
            if (_brushMode == BrushMode.Eyedropper) return;
            if (_paintCanvas == null || Mouse.current == null) return;

            if (_waitForMouseRelease)
            {
                if (!Mouse.current.leftButton.isPressed)
                    _waitForMouseRelease = false;
                return;
            }

            if (!Mouse.current.leftButton.isPressed) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = cam.ScreenToWorldPoint(screenPos);
            worldPos.z = 0;

            Vector2Int pixel = _paintCanvas.WorldToPixel(worldPos);
            if (_paintCanvas.IsInBounds(pixel.x, pixel.y))
            {
                switch (_brushMode)
                {
                    case BrushMode.Eraser:
                        _paintCanvas.EraseAt(pixel.x, pixel.y);
                        break;
                    case BrushMode.Brush:
                    default:
                        Color color = _boundPicker != null ? _boundPicker.SelectedColor : Color.white;
                        _paintCanvas.PaintAt(pixel.x, pixel.y, color);
                        break;
                }
            }
        }

        private IEnumerator EyedropperCaptureLoop()
        {
            if (_eyedropperCapture == null)
                _eyedropperCapture = new Texture2D(1, 1, TextureFormat.RGB24, false);

            while (true)
            {
                yield return new WaitForEndOfFrame();

                if (Mouse.current == null) continue;

                Vector2 screenPos = Mouse.current.position.ReadValue();
                int x = (int)screenPos.x;
                int y = (int)screenPos.y;

                if (x >= 0 && x < Screen.width && y >= 0 && y < Screen.height)
                {
                    _eyedropperCapture.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
                    _eyedropperCapture.Apply();
                    _eyedropperColor = _eyedropperCapture.GetPixel(0, 0);
                    _boundPicker?.SetEyedropperPreview(_eyedropperColor);
                }
            }
        }

        private void StopEyedropperCapture()
        {
            if (_eyedropperCoroutine != null)
            {
                _owner.StopCoroutine(_eyedropperCoroutine);
                _eyedropperCoroutine = null;
            }
        }

        private void OnUIBrushSizeChanged(int size)
        {
            if (_paintCanvas != null)
                _paintCanvas.BrushSize = size;

            RefreshCursor();
        }
    }
}
