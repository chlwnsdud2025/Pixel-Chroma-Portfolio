using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using OneText.UGUI;

namespace PixelChroma.UI
{
    public class UIPixelNicknameSetting : UIPopup
    {
        [Header("Canvas")]
        [SerializeField] private RawImage canvasImage;
        [SerializeField] private RawImage checkerboardBackground;
        [SerializeField] private RawImage hoverOverlay;
        [SerializeField] private int canvasWidth = 48;
        [SerializeField] private int canvasHeight = 12;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Color Picker")]
        [SerializeField] private UIColorPicker colorPicker;

        [Header("Text Nickname")]
        [SerializeField] private OneTextInputField nicknameInputField;
        [SerializeField] private int maxNicknameLength = 10;
        [SerializeField] private BannedWordsSO bannedWords;
        [SerializeField] private UIWarningText warningText;
        [SerializeField] private NicknameSettingTextDataSO textData;

        [Header("Tools")]
        [SerializeField] private Button clearButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button closeButton;

        [Header("Preview")]
        [SerializeField] private RawImage previewImage;

        [Header("Presets")]
        [SerializeField] private Button[] presetButtons;    // Inspector에서 3개 할당
        [SerializeField] private Color presetActiveColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color presetInactiveColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [Header("Default Preset")]
        [SerializeField] private Button defaultPresetButton;
        [SerializeField] private Texture2D[] defaultImages;  // Inspector에서 기본 이미지들 할당

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        private Texture2D _editTexture;
        private Texture2D _checkerTexture;
        private Color _currentColor = Color.white;
        private BrushMode _brushMode = BrushMode.Brush;
        private int _brushSize = 1;
        private bool _isPainting;

        // ── Input System ──
        private PlayerInputActions _input;

        // ── 호버 인디케이터 (인게임 EnsureBrushPreview와 동일 사양) ──
        private const int HoverRes = 6; // 브러시 셀 1개당 서브픽셀 수 (인게임과 동일)
        private Texture2D _hoverTexture;
        private int _lastHoverPixelX = -1;
        private int _lastHoverPixelY = -1;

        private static readonly Color PreviewOutline = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color PreviewFill   = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        // ── Undo / Redo ──
        private const int MaxHistory = 50;
        private readonly List<Color32[]> _undoStack = new();
        private readonly List<Color32[]> _redoStack = new();
        private Color32[] _strokeSnapshot;
        private bool _strokeActive;

        // ── Presets ──
        private int _activePresetIndex;
        private byte[][] _presetCache;
        private bool _isDefaultPreset;
        private int _defaultImageIndex;

        // ────────────────────── 라이프사이클 ──────────────────────

        public override void Initialize()
        {
            base.Initialize();

            // Input System
            if (inputActions != null)
                _input = new PlayerInputActions(inputActions);

            // ColorPicker
            if (colorPicker != null)
            {
                colorPicker.Initialize();
                colorPicker.OnColorChanged      += OnColorChanged;
                colorPicker.OnToolButtonClicked  += OnToolChanged;
                colorPicker.OnBrushSizeChanged   += OnBrushSizeChanged;
                colorPicker.OnUndoButtonClicked  += Undo;
                colorPicker.OnRedoButtonClicked  += Redo;
            }

            if (clearButton != null) clearButton.AddClickListener(OnClickClear);
            if (saveButton != null) saveButton.AddClickListener(OnClickSave);
            if (closeButton != null) closeButton.AddClickListener(OnClickBack);

            if (nicknameInputField != null)
                nicknameInputField.characterLimit = maxNicknameLength;

            if (presetButtons != null)
            {
                for (int i = 0; i < presetButtons.Length; i++)
                {
                    int index = i;  // 클로저 캡처용 로컬 복사
                    if (presetButtons[i] != null)
                        presetButtons[i].AddClickListener(() => OnPresetSelected(index));
                }
            }
            if (defaultPresetButton != null)
                defaultPresetButton.AddClickListener(OnDefaultPresetSelected);

            SetupCanvasDrag();
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();
            _input?.EnableAll();

            PixelNicknameService.MigrateToPresetIfNeeded();
            LoadPresetsAndSelect();

            if (nicknameInputField != null)
                nicknameInputField.text = TextNicknameService.LoadOrDefault();

            SetWarningMessage(string.Empty);

            _brushMode = BrushMode.Brush;
            _brushSize = 1;

            if (colorPicker != null)
            {
                colorPicker.SetColor(Color.white);
                colorPicker.SetActiveTool(BrushMode.Brush);
                colorPicker.SetBrushSize(1);
            }

            if (panel != null) panel.localScale = Vector3.zero;
        }

        public override void OnAfterHide()
        {
            base.OnAfterHide();
            _input?.DisableAll();
            ClearHoverOverlay();
        }

        protected override async UniTask OnShowAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.one, animationDuration)
                    .SetEase(Ease.OutBack).ToUniTask();
        }

        protected override async UniTask OnHideAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.zero, animationDuration)
                    .SetEase(Ease.InBack).ToUniTask();
        }

        private void OnDestroy()
        {
            if (_editTexture != null)   Destroy(_editTexture);
            if (_checkerTexture != null) Destroy(_checkerTexture);
            if (_hoverTexture != null)   Destroy(_hoverTexture);

            if (colorPicker != null)
            {
                colorPicker.OnColorChanged      -= OnColorChanged;
                colorPicker.OnToolButtonClicked  -= OnToolChanged;
                colorPicker.OnBrushSizeChanged   -= OnBrushSizeChanged;
                colorPicker.OnUndoButtonClicked  -= Undo;
                colorPicker.OnRedoButtonClicked  -= Redo;
            }
        }

        // ────────────────────── Update ──────────────────────

        private void Update()
        {
            UpdateHoverIndicator();
            HandleKeyboardShortcuts();
            HandleBrushSizeScroll();
        }

        //---------------------------Text 닉네임----------------------------------------------
        private void SetWarningMessage(string message)
        {
            if (warningText != null) warningText.Show(message);

            if (!string.IsNullOrEmpty(message) && nicknameInputField != null)
                nicknameInputField.ActivateInputField();
        }

        // ────────────────────── 캔버스 초기화 ──────────────────────

        private void GenerateCheckerboard()
        {
            if (checkerboardBackground == null) return;

            if (_checkerTexture != null)
                Destroy(_checkerTexture);

            _checkerTexture = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color light = new Color(0.9f, 0.9f, 0.9f, 1f);
            Color dark  = new Color(0.7f, 0.7f, 0.7f, 1f);

            for (int y = 0; y < canvasHeight; y++)
                for (int x = 0; x < canvasWidth; x++)
                    _checkerTexture.SetPixel(x, y, (x + y) % 2 == 0 ? light : dark);

            _checkerTexture.Apply();
            checkerboardBackground.texture = _checkerTexture;
        }

        private void InitializeHoverOverlay()
        {
            if (hoverOverlay == null) return;

            if (_hoverTexture != null)
                Destroy(_hoverTexture);

            // 인게임과 동일: 캔버스 픽셀당 HoverRes(6) 서브픽셀, Bilinear 필터
            int hoverW = canvasWidth * HoverRes;
            int hoverH = canvasHeight * HoverRes;
            _hoverTexture = new Texture2D(hoverW, hoverH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            _hoverTexture.SetPixels32(new Color32[hoverW * hoverH]);
            _hoverTexture.Apply();

            hoverOverlay.texture = _hoverTexture;
            hoverOverlay.raycastTarget = false;
            hoverOverlay.gameObject.SetActive(false);

            _lastHoverPixelX = -1;
            _lastHoverPixelY = -1;
        }

        // ────────────────────── 입력 처리 ──────────────────────

        private void SetupCanvasDrag()
        {
            if (canvasImage == null) return;

            var trigger = canvasImage.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = canvasImage.gameObject.AddComponent<EventTrigger>();

            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener(data =>
            {
                _isPainting = true;
                BeginStroke();
                HandlePaintInput((PointerEventData)data);
            });
            trigger.triggers.Add(pointerDown);

            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(data => HandlePaintInput((PointerEventData)data));
            trigger.triggers.Add(drag);

            var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            pointerUp.callback.AddListener(_ =>
            {
                _isPainting = false;
                EndStroke();
            });
            trigger.triggers.Add(pointerUp);
        }

        private void HandlePaintInput(PointerEventData eventData)
        {
            if (_editTexture == null || canvasImage == null) return;
            if (!_isPainting) return;

            RectTransform rt = canvasImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                return;

            Rect rect = rt.rect;
            float u = (localPoint.x - rect.x) / rect.width;
            float v = (localPoint.y - rect.y) / rect.height;

            if (u < 0f || u >= 1f || v < 0f || v >= 1f) return;

            int px = Mathf.FloorToInt(u * canvasWidth);
            int py = Mathf.FloorToInt(v * canvasHeight);

            switch (_brushMode)
            {
                case BrushMode.Brush:
                    PaintPixels(px, py, _currentColor);
                    break;
                case BrushMode.Eraser:
                    PaintPixels(px, py, Color.clear);
                    break;
                case BrushMode.Eyedropper:
                    _currentColor = _editTexture.GetPixel(px, py);
                    colorPicker?.SetColor(_currentColor);
                    break;
                case BrushMode.Fill:
                    FloodFill(px, py, _currentColor);
                    break;
            }
        }

        // ────────────────────── 키보드 단축키 (InputSystem) ──────────────────────

        private void HandleKeyboardShortcuts()
        {
            if (_input == null) return;

            // 텍스트 입력 중에는 단축키 비활성화 (1→붓, Z→undo 등 충돌 방지)
            if (nicknameInputField != null && nicknameInputField.isFocused) return;
            // 드래그 중에는 Undo/Redo/도구전환 막음
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return;

            // Redo를 먼저 검사: Ctrl+Shift+Z가 Undo(Ctrl+Z)의 OneModifier 조건도
            // 만족하므로, Redo가 우선해야 같은 프레임 중복 발동 안 됨
            if (_input.Redo != null && _input.Redo.WasPressedThisFrame())
                Redo();
            else if (_input.Undo != null && _input.Undo.WasPressedThisFrame())
                Undo();

            // 도구 전환: 1 → 붓, 2 → 지우개, 3 → 스포이드
            if (_input.SelectBrush != null && _input.SelectBrush.WasPressedThisFrame())
                OnToolChanged(BrushMode.Brush);
            else if (_input.SelectEraser != null && _input.SelectEraser.WasPressedThisFrame())
                OnToolChanged(BrushMode.Eraser);
            else if (_input.SelectEyedropper != null && _input.SelectEyedropper.WasPressedThisFrame())
                OnToolChanged(BrushMode.Fill);
        }

        private void HandleBrushSizeScroll()
        {
            if (nicknameInputField != null && nicknameInputField.isFocused) return;

            if (_input?.BrushSizeModifier == null) return;
            if (!_input.BrushSizeModifier.IsPressed()) return;
            if (Mouse.current == null) return;

            float scroll = Mouse.current.scroll.y.ReadValue();
            if (Mathf.Abs(scroll) < 0.01f) return;

            int max = colorPicker != null ? colorPicker.MaxBrushSize : 5;
            int next = scroll > 0 ? _brushSize + 1 : _brushSize - 1;
            next = Mathf.Clamp(next, 1, max);

            if (next != _brushSize)
            {
                _brushSize = next;
                colorPicker?.SetBrushSize(next);
                _lastHoverPixelX = -1; // 호버 크기 갱신
            }
        }

        // ────────────────────── 그리기 ──────────────────────

        private void PaintPixels(int cx, int cy, Color color)
        {
            int half = _brushSize / 2;

            for (int dy = -half; dy < _brushSize - half; dy++)
            {
                for (int dx = -half; dx < _brushSize - half; dx++)
                {
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px < 0 || px >= canvasWidth || py < 0 || py >= canvasHeight)
                        continue;
                    _editTexture.SetPixel(px, py, color);
                }
            }

            _editTexture.Apply();
        }

        private void FloodFill(int startX, int startY, Color fillColor)
        {
            Color targetColor = _editTexture.GetPixel(startX, startY);

            if (ColorsEqual(targetColor, fillColor)) return;

            var stack = new Stack<(int x, int y)>();
            stack.Push((startX, startY));

            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                if (x < 0 || x >= canvasWidth || y < 0 || y >= canvasHeight)
                    continue;

                Color current = _editTexture.GetPixel(x, y);
                if (!ColorsEqual(current, targetColor))
                    continue;

                _editTexture.SetPixel(x, y, fillColor);

                stack.Push((x + 1, y));
                stack.Push((x - 1, y));
                stack.Push((x, y + 1));
                stack.Push((x, y - 1));
            }

            _editTexture.Apply();
        }

        private static bool ColorsEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f
                && Mathf.Abs(a.g - b.g) < 0.01f
                && Mathf.Abs(a.b - b.b) < 0.01f
                && Mathf.Abs(a.a - b.a) < 0.01f;
        }

        // ────────────────────── Undo / Redo ──────────────────────

        private Color32[] CapturePixels()
        {
            return _editTexture.GetPixels32();
        }

        private void ApplyPixels(Color32[] pixels)
        {
            _editTexture.SetPixels32(pixels);
            _editTexture.Apply();
        }

        private void PushUndo(Color32[] snapshot)
        {
            _undoStack.Add(snapshot);
            if (_undoStack.Count > MaxHistory)
                _undoStack.RemoveAt(0);
        }

        private void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _strokeSnapshot = null;
            _strokeActive = false;
        }

        // 스트로크 경계 (PointerDown → PointerUp = 1 스트로크 = 1 Undo 단위)

        private void BeginStroke()
        {
            if (_strokeActive) return;
            _strokeSnapshot = CapturePixels();
            _strokeActive = true;
        }

        private void EndStroke()
        {
            if (!_strokeActive) return;
            _strokeActive = false;

            if (_strokeSnapshot == null) return;

            Color32[] current = CapturePixels();
            if (!PixelsEqual(_strokeSnapshot, current))
            {
                PushUndo(_strokeSnapshot);
                _redoStack.Clear();
            }
            _strokeSnapshot = null;
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            EndStroke();

            _redoStack.Add(CapturePixels());
            Color32[] prev = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            ApplyPixels(prev);
        }

        private void Redo()
        {
            if (_redoStack.Count == 0) return;
            EndStroke();

            PushUndo(CapturePixels());
            Color32[] next = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            ApplyPixels(next);
        }

        private static bool PixelsEqual(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].r != b[i].r || a[i].g != b[i].g ||
                    a[i].b != b[i].b || a[i].a != b[i].a)
                    return false;
            }
            return true;
        }

        // ────────────────────── 호버 인디케이터 ──────────────────────

        private void UpdateHoverIndicator()
        {
            if (_hoverTexture == null || canvasImage == null || hoverOverlay == null) return;

            // 스포이드 모드에서는 호버 표시 안 함
            if (_brushMode == BrushMode.Eyedropper)
            {
                ClearHoverOverlay();
                return;
            }

            if (Mouse.current == null)
            {
                ClearHoverOverlay();
                return;
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();
            RectTransform rt = canvasImage.rectTransform;
            Camera cam = canvasImage.canvas != null ? canvasImage.canvas.worldCamera : null;

            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, cam))
            {
                ClearHoverOverlay();
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, mousePos, cam, out Vector2 localPoint);

            Rect rect = rt.rect;
            float u = (localPoint.x - rect.x) / rect.width;
            float v = (localPoint.y - rect.y) / rect.height;

            int px = Mathf.Clamp(Mathf.FloorToInt(u * canvasWidth), 0, canvasWidth - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(v * canvasHeight), 0, canvasHeight - 1);

            // 같은 픽셀이면 다시 그리지 않음
            if (px == _lastHoverPixelX && py == _lastHoverPixelY) return;
            _lastHoverPixelX = px;
            _lastHoverPixelY = py;

            // 채우기 모드는 단일 픽셀 표시
            int displaySize = _brushMode == BrushMode.Fill ? 1 : _brushSize;
            DrawBrushOutline(px, py, displaySize);
        }

        private void DrawBrushOutline(int cx, int cy, int size)
        {
            int hoverW = canvasWidth * HoverRes;
            int hoverH = canvasHeight * HoverRes;
            _hoverTexture.SetPixels32(new Color32[hoverW * hoverH]);

            // 캔버스 좌표 → 서브픽셀 좌표 변환 (인게임 EnsureBrushPreview와 동일 구조)
            int half = size / 2;
            int minX = (cx - half) * HoverRes;
            int minY = (cy - half) * HoverRes;
            int maxX = minX + size * HoverRes - 1;
            int maxY = minY + size * HoverRes - 1;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x < 0 || x >= hoverW || y < 0 || y >= hoverH)
                        continue;

                    // 최외곽 1 서브픽셀만 외곽선, 나머지는 채움
                    bool isBorder = (x == minX || x == maxX || y == minY || y == maxY);
                    _hoverTexture.SetPixel(x, y, isBorder ? PreviewOutline : PreviewFill);
                }
            }

            _hoverTexture.Apply();

            if (!hoverOverlay.gameObject.activeSelf)
                hoverOverlay.gameObject.SetActive(true);
        }

        private void ClearHoverOverlay()
        {
            _lastHoverPixelX = -1;
            _lastHoverPixelY = -1;

            if (hoverOverlay != null && hoverOverlay.gameObject.activeSelf)
                hoverOverlay.gameObject.SetActive(false);
        }

        // ────────────────────── UIColorPicker 이벤트 ──────────────────────

        private void OnColorChanged(Color color)
        {
            _currentColor = color;
        }

        private void OnToolChanged(BrushMode mode)
        {
            _brushMode = mode;
            colorPicker?.SetActiveTool(mode);
            _lastHoverPixelX = -1; // 도구 변경 시 호버 갱신
        }

        private void OnBrushSizeChanged(int size)
        {
            _brushSize = size;
            _lastHoverPixelX = -1; // 크기 변경 시 호버 갱신
        }

        // ────────────────────── 버튼 ──────────────────────

        private void OnClickClear()
        {
            if (_editTexture == null) return;
            if (_isDefaultPreset) return;

            Color32[] before = CapturePixels();
            _editTexture.SetPixels32(new Color32[canvasWidth * canvasHeight]);
            _editTexture.Apply();

            Color32[] after = CapturePixels();
            if (!PixelsEqual(before, after))
            {
                PushUndo(before);
                _redoStack.Clear();
            }
        }

        private void OnClickSave()
        {
            // ── 텍스트 닉네임 검증 ──
            if (nicknameInputField != null)
            {
                string enteredName = nicknameInputField.text.Trim();

                switch (NameValidationService.Validate(enteredName, bannedWords, maxNicknameLength))
                {
                    case NameValidation.TooLong:
                        SetWarningMessage(textData.warningMaxLength.GetLocalizedString(maxNicknameLength));
                        return;
                    case NameValidation.Banned:
                        SetWarningMessage(textData.warningBanned.GetLocalizedString());
                        return;
                    case NameValidation.Unsupported:
                        if (!textData.warningUnsupported.IsEmpty)
                            SetWarningMessage(textData.warningUnsupported.GetLocalizedString());
                        return;
                }

                if (string.IsNullOrEmpty(enteredName))
                    enteredName = TextNicknameService.GetDefaultSteamName();

                TextNicknameService.Save(enteredName);
            }

            // ── 픽셀 닉네임 저장 ──
            if (_editTexture != null)
            {
                // 빈 캔버스 체크: 유저 프리셋인데 아무것도 안 그렸으면 기본으로 자동 전환
                if (!_isDefaultPreset && IsCanvasEmpty())
                {
                    _isDefaultPreset = true;
                    _defaultImageIndex = PixelNicknameService.LoadDefaultImageIndex();
                    if (defaultImages != null && defaultImages.Length > 0)
                        ApplyDefaultToCanvas(_defaultImageIndex);
                }

                byte[] png = _editTexture.EncodeToPNG();

                if (_isDefaultPreset)
                {
                    PixelNicknameService.SaveIsDefaultPreset(true);
                    PixelNicknameService.SaveDefaultImageIndex(_defaultImageIndex);
                }
                else
                {
                    PixelNicknameService.SaveIsDefaultPreset(false);
                    PixelNicknameService.SavePreset(_activePresetIndex, png);
                    PixelNicknameService.SaveActivePresetIndex(_activePresetIndex);
                }

                // 메인 키는 항상 업데이트 (네트워크 호환)
                PixelNicknameService.Save(png);
            }

            OnClickBack();
        }

        // ────────────────────── 프리셋 ──────────────────────

        private void LoadPresetsAndSelect()
        {
            _presetCache = new byte[PixelNicknameService.PresetCount][];
            for (int i = 0; i < PixelNicknameService.PresetCount; i++)
                _presetCache[i] = PixelNicknameService.LoadPreset(i);

            _isDefaultPreset = PixelNicknameService.LoadIsDefaultPreset();
            _defaultImageIndex = PixelNicknameService.LoadDefaultImageIndex();
            _activePresetIndex = PixelNicknameService.LoadActivePresetIndex();

            if (_isDefaultPreset)
                ApplyDefaultToCanvas(_defaultImageIndex);
            else
                ApplyPresetToCanvas(_activePresetIndex);

            // 체커보드·호버는 최초 1회만
            GenerateCheckerboard();
            InitializeHoverOverlay();

            UpdatePresetToggleVisual();
            UpdateEditingLock();
        }

        private void OnDefaultPresetSelected()
        {
            if (_isDefaultPreset)
            {
                // 이미 기본 모드 → 다음 이미지로 순환
                if (defaultImages != null && defaultImages.Length > 0)
                {
                    _defaultImageIndex = (_defaultImageIndex + 1) % defaultImages.Length;
                    ApplyDefaultToCanvas(_defaultImageIndex);
                }
                return;
            }

            // 유저 프리셋 → 기본 프리셋으로 전환
            // 현재 캔버스를 유저 프리셋 캐시에 보관
            if (_editTexture != null)
                _presetCache[_activePresetIndex] = _editTexture.EncodeToPNG();

            _isDefaultPreset = true;
            _defaultImageIndex = PixelNicknameService.LoadDefaultImageIndex();

            ApplyDefaultToCanvas(_defaultImageIndex);
            UpdatePresetToggleVisual();
            UpdateEditingLock();
        }

        private void OnPresetSelected(int index)
        {
            // 기본 프리셋에서 유저 프리셋으로 복귀
            if (_isDefaultPreset)
            {
                _isDefaultPreset = false;
                _activePresetIndex = index;
                ApplyPresetToCanvas(index);
                UpdatePresetToggleVisual();
                UpdateEditingLock();
                return;
            }

            if (index == _activePresetIndex) return;

            // 현재 캔버스 작업물을 기존 프리셋 캐시에 임시 보관
            if (_editTexture != null)
                _presetCache[_activePresetIndex] = _editTexture.EncodeToPNG();

            _activePresetIndex = index;
            ApplyPresetToCanvas(index);
            UpdatePresetToggleVisual();
            UpdateEditingLock();
        }

        private void ApplyDefaultToCanvas(int imageIndex)
        {
            if (defaultImages == null || defaultImages.Length == 0) return;

            imageIndex = Mathf.Clamp(imageIndex, 0, defaultImages.Length - 1);
            Texture2D source = defaultImages[imageIndex];
            if (source == null) return;

            InitializeCanvasTexture();

            int copyW = Mathf.Min(source.width, canvasWidth);
            int copyH = Mathf.Min(source.height, canvasHeight);
            for (int y = 0; y < copyH; y++)
                for (int x = 0; x < copyW; x++)
                    _editTexture.SetPixel(x, y, source.GetPixel(x, y));

            _editTexture.Apply();
            canvasImage.texture = _editTexture;
            if (previewImage != null)
                previewImage.texture = _editTexture;

            ClearHistory();
        }

        private void ApplyPresetToCanvas(int index)
        {
            InitializeCanvasTexture();

            byte[] data = _presetCache[index];
            if (data != null && data.Length > 0)
            {
                var temp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (temp.LoadImage(data))
                {
                    int copyW = Mathf.Min(temp.width, canvasWidth);
                    int copyH = Mathf.Min(temp.height, canvasHeight);
                    for (int y = 0; y < copyH; y++)
                        for (int x = 0; x < copyW; x++)
                            _editTexture.SetPixel(x, y, temp.GetPixel(x, y));
                }
                Destroy(temp);
            }

            _editTexture.Apply();
            canvasImage.texture = _editTexture;
            if (previewImage != null)
                previewImage.texture = _editTexture;

            ClearHistory();
        }

        private void UpdateEditingLock()
        {
            if (canvasImage != null)
                canvasImage.raycastTarget = !_isDefaultPreset;
        }

        private void UpdatePresetToggleVisual()
        {
            // 유저 프리셋 버튼
            if (presetButtons != null)
            {
                for (int i = 0; i < presetButtons.Length; i++)
                {
                    if (presetButtons[i] == null) continue;

                    var img = presetButtons[i].GetComponent<Image>();
                    if (img != null)
                    {
                        bool active = !_isDefaultPreset && i == _activePresetIndex;
                        img.color = active ? presetActiveColor : presetInactiveColor;
                    }
                }
            }

            // 기본 프리셋 버튼
            if (defaultPresetButton != null)
            {
                var img = defaultPresetButton.GetComponent<Image>();
                if (img != null)
                    img.color = _isDefaultPreset ? presetActiveColor : presetInactiveColor;
            }
        }

        private bool IsCanvasEmpty()
        {
            if (_editTexture == null) return true;

            Color32[] pixels = _editTexture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0)
                    return false;
            }
            return true;
        }

        private void InitializeCanvasTexture()
        {
            if (_editTexture != null)
                Destroy(_editTexture);

            _editTexture = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            _editTexture.SetPixels32(new Color32[canvasWidth * canvasHeight]);
            _editTexture.Apply();
        }
    }
}
