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
        private static readonly Color PreviewFill = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        private static readonly Color PreviewOutline = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color EraserFill = new Color(1f, 0.2f, 0.2f, 0.3f);

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

        private Camera _cachedCamera;
        private GameObject _brushPreview;
        private SpriteRenderer _brushPreviewRenderer;
        private int _previewLastSize = -1;

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
            _cachedCamera = Camera.main;
        }

        public void Tick()
        {
            HandleModeInput();
            HandleHistoryInput();

            if (_brushMode == BrushMode.Eyedropper)
            {
                HideBrushPreview();
                TickEyedropper();
                return;
            }

            UpdateBrushPreview();
            HandlePainting();
        }

        public void Dispose()
        {
            StopEyedropperCapture();

            if (_owner != null && _owner.isLocalPlayer && CursorManager.Instance != null)
                CursorManager.Instance.SetCursorType(CursorType.Default);

            if (_eyedropperCapture != null)
            {
                UnityEngine.Object.Destroy(_eyedropperCapture);
                _eyedropperCapture = null;
            }

            if (_brushPreview != null)
            {
                if (_brushPreviewRenderer != null && _brushPreviewRenderer.sprite != null)
                    UnityEngine.Object.Destroy(_brushPreviewRenderer.sprite.texture);
                UnityEngine.Object.Destroy(_brushPreview);
                _brushPreview = null;
                _brushPreviewRenderer = null;
                _previewLastSize = -1;
            }
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

            _paintCanvas?.EndStroke();   // 도구 전환 시 진행 중 스트로크 확정

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

            if (!_isPaintMode)
            {
                _paintCanvas?.EndStroke();   // 페인트 모드 종료 시 진행 중 스트로크 확정
                HideBrushPreview();
            }

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

        private void HandleHistoryInput()
        {
            if (_paintCanvas == null) return;
            // 드래그(색칠) 중에는 되돌리기를 막는다 — 진행 중 스트로크와 꼬이지 않도록
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return;

            // Redo를 먼저 검사: Ctrl+Shift+Z는 Undo(Ctrl+Z)의 OneModifier 조건도
            // 만족하므로, Redo가 우선하도록 해 같은 프레임 중복 발동을 막는다.
            if (_input.Redo != null && _input.Redo.WasPressedThisFrame())
            {
                _paintCanvas.EndStroke();   // 혹시 남은 스트로크가 있으면 먼저 확정
                _paintCanvas.Redo();
            }
            else if (_input.Undo != null && _input.Undo.WasPressedThisFrame())
            {
                _paintCanvas.EndStroke();
                _paintCanvas.Undo();
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

        private void UpdateBrushPreview()
        {
            if (_owner == null || !_owner.isLocalPlayer || !_isPaintMode)
            {
                HideBrushPreview();
                return;
            }

            // UI 위에서는 색칠이 막히므로 프리뷰도 숨김
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HideBrushPreview();
                return;
            }

            if (!TryGetMousePixel(out var pixel))
            {
                HideBrushPreview();
                return;
            }

            int size = _paintCanvas.BrushSize;
            EnsureBrushPreview(size);

            Vector3 center = _paintCanvas.GetBrushWorldCenter(pixel.x, pixel.y, size);
            center.z = 0f;
            float worldSize = _paintCanvas.GetBrushWorldSize(size);

            _brushPreview.transform.position = center;
            _brushPreview.transform.localScale = new Vector3(worldSize, worldSize, 1f);

            _brushPreviewRenderer.color = _brushMode == BrushMode.Eraser
                ? EraserFill   // 지우개: 빨간 반투명 (외곽선은 텍스처에 내장)
                : Color.white; // 브러시: 텍스처 원색 그대로 (회색 내부 + 검정 외곽선)

            if (!_brushPreview.activeSelf)
                _brushPreview.SetActive(true);
        }

        private void EnsureBrushPreview(int size)
        {
            if (_brushPreview == null)
            {
                _brushPreview = new GameObject("BrushAreaPreview");
                _brushPreviewRenderer = _brushPreview.AddComponent<SpriteRenderer>();
                _brushPreviewRenderer.sortingLayerID = _paintCanvas.SortingLayerID;
                _brushPreviewRenderer.sortingOrder = _paintCanvas.SortingOrder + 1000;
                _brushPreview.SetActive(false);
                _previewLastSize = -1;
            }

            if (_previewLastSize == size) return;
            _previewLastSize = size;

            const int res = 6;
            int brushPx = Mathf.Max(size, 1);
            int texSize = brushPx * res;
            var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == texSize - 1 || y == texSize - 1;
                    tex.SetPixel(x, y, isBorder ? PreviewOutline : PreviewFill);
                }
            }
            tex.Apply();

            if (_brushPreviewRenderer.sprite != null)
                UnityEngine.Object.Destroy(_brushPreviewRenderer.sprite.texture);

            _brushPreviewRenderer.sprite = Sprite.Create(
                tex, new Rect(0f, 0f, texSize, texSize),
                new Vector2(0.5f, 0.5f), texSize);
        }

        private void HideBrushPreview()
        {
            if (_brushPreview != null && _brushPreview.activeSelf)
                _brushPreview.SetActive(false);
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
            if (Mouse.current == null) return;

            // 마우스를 떼면 이번 드래그를 1 스트로크로 확정(히스토리 1건)
            if (Mouse.current.leftButton.wasReleasedThisFrame)
                _paintCanvas.EndStroke();

            if (_waitForMouseRelease)
            {
                if (!Mouse.current.leftButton.isPressed)
                    _waitForMouseRelease = false;
                return;
            }

            if (!Mouse.current.leftButton.isPressed) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (!TryGetMousePixel(out var pixel)) return;

            if (_paintCanvas.IsInBounds(pixel.x, pixel.y))
            {
                _paintCanvas.BeginStroke();   // 드래그 첫 프레임에만 실제로 시작(그 외 no-op)
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

                if (x < 0 || x >= Screen.width || y < 0 || y >= Screen.height) continue;

                _eyedropperCapture.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
                _eyedropperCapture.Apply();
                _eyedropperColor = _eyedropperCapture.GetPixel(0, 0);

                _boundPicker?.SetEyedropperPreview(_eyedropperColor);

                // 커서 옆 스포이드 미리보기 내용만 갱신
                if (CursorManager.Instance != null)
                    CursorManager.Instance.UpdateEyedropperPreview(_eyedropperColor);
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

        private bool TryGetMousePixel(out Vector2Int pixel)
        {
            pixel = default;

            if (_cachedCamera == null || Mouse.current == null || _paintCanvas == null)
                return false;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = _cachedCamera.ScreenToWorldPoint(screenPos);
            worldPos.z = 0f;
            pixel = _paintCanvas.WorldToPixel(worldPos);
            return true;
        }

        private void OnUIBrushSizeChanged(int size)
        {
            if (_paintCanvas != null)
                _paintCanvas.BrushSize = size;

            RefreshCursor();
        }
    }
}
