using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIColorPicker : MonoBehaviour
    {
        private const int DefaultMaxBrushSize = 20;

        [Header("Tools (New)")]
        [SerializeField] private Button eraserButton;     // 인스펙터에서 지우개 버튼 연결
        [SerializeField] private Button eyedropperButton; // 인스펙터에서 스포이드 버튼 연결
        [SerializeField] private Color activeToolColor = new Color(0.8f, 0.8f, 0.8f); // 켜졌을 때 버튼 색상
        [SerializeField] private Color inactiveToolColor = Color.white;               // 꺼졌을 때 버튼 색상

        [Header("SV Rectangle")]
        [SerializeField] private RectTransform svArea;
        [SerializeField] private RawImage svImage;
        [SerializeField] private RectTransform svSelector;

        [Header("Hue Bar")]
        [SerializeField] private RectTransform hueBarArea;
        [SerializeField] private RectTransform hueSelector;

        [Header("Preview")]
        [SerializeField] private Image colorPreview;
        [SerializeField] private TMP_Text hexLabel;

        [Header("Eyedropper")]
        [SerializeField] private Outline eyedropperOutline;
        [SerializeField] private Image eyedropperPreviewColor;

        [Header("Brush Size")]
        [SerializeField] private Slider brushSizeSlider;
        [SerializeField] private TMP_Text brushSizeLabel;
        [SerializeField] private int maxBrushSize = 5;

        private Texture2D _svTexture;
        private float _hue;
        private float _saturation = 1f;
        private float _value = 1f;

        // 💡 툴 상태 속성
        public bool IsEraserMode { get; private set; }
        public bool IsEyedropperMode { get; private set; }

        public Color SelectedColor => Color.HSVToRGB(_hue, _saturation, _value);
        public int BrushSize => brushSizeSlider != null ? (int)brushSizeSlider.value : 1;
        public int MaxBrushSize => maxBrushSize > 0 ? maxBrushSize : DefaultMaxBrushSize;

        public event Action<Color> OnColorChanged;
        public event Action<int> OnBrushSizeChanged;
        public event Action<bool> OnEyedropperModeChanged; // 스포이드 상태 변경 이벤트 추가

        private void Awake()
        {
            _svTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            SetupSVDrag();
            SetupHueDrag();
            SetupBrushSizeSliders();
            SetColor(Color.white);

            // 버튼 리스너 등록
            if (eraserButton != null) eraserButton.onClick.AddListener(ToggleEraser);
            if (eyedropperButton != null) eyedropperButton.onClick.AddListener(ToggleEyedropper);
        }

        // ── 툴 토글 로직 ──────────────────────────────────────

        private void ToggleEraser()
        {
            IsEraserMode = !IsEraserMode;
            if (IsEraserMode) SetEyedropper(false); // 지우개를 켜면 스포이드는 끔
            UpdateToolButtons();
        }

        private void ToggleEyedropper()
        {
            SetEyedropper(!IsEyedropperMode);
        }

        public void SetEyedropper(bool active)
        {
            IsEyedropperMode = active;
            if (IsEyedropperMode) IsEraserMode = false; // 스포이드를 켜면 지우개는 끔

            if (eyedropperOutline != null) eyedropperOutline.enabled = active;
            if (!active && eyedropperPreviewColor != null) eyedropperPreviewColor.color = SelectedColor;

            UpdateToolButtons();
            OnEyedropperModeChanged?.Invoke(active); // PlayerBase에 알림
        }

        private void UpdateToolButtons()
        {
            if (eraserButton != null)
                eraserButton.image.color = IsEraserMode ? activeToolColor : inactiveToolColor;

            if (eyedropperButton != null)
                eyedropperButton.image.color = IsEyedropperMode ? activeToolColor : inactiveToolColor;
        }

        // ── 기존 마우스 드래그 및 색상 세팅 로직 ───────────────────────

        private void CancelToolsIfColorPicked()
        {
            // 유저가 직접 팔레트에서 새로운 색상을 클릭/드래그하면 자동으로 브러시(그리기) 모드로 돌아감
            if (IsEraserMode || IsEyedropperMode)
            {
                IsEraserMode = false;
                SetEyedropper(false);
            }
        }

        private void SetupSVDrag()
        {
            var trigger = svArea.gameObject.AddComponent<EventTrigger>();
            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener(data => OnSVInput((PointerEventData)data));
            trigger.triggers.Add(pointerDown);

            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(data => OnSVInput((PointerEventData)data));
            trigger.triggers.Add(drag);
        }

        private void SetupHueDrag()
        {
            var trigger = hueBarArea.gameObject.AddComponent<EventTrigger>();
            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener(data => OnHueInput((PointerEventData)data));
            trigger.triggers.Add(pointerDown);

            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(data => OnHueInput((PointerEventData)data));
            trigger.triggers.Add(drag);
        }

        private void OnSVInput(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    svArea, eventData.position, eventData.pressEventCamera, out var localPoint))
                return;

            CancelToolsIfColorPicked(); // 💡 색을 바꾸면 툴 취소

            var rect = svArea.rect;
            _saturation = Mathf.Clamp01((localPoint.x - rect.x) / rect.width);
            _value = Mathf.Clamp01((localPoint.y - rect.y) / rect.height);

            UpdateSVSelectorPosition();
            UpdateDisplay();
        }

        private void OnHueInput(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    hueBarArea, eventData.position, eventData.pressEventCamera, out var localPoint))
                return;

            CancelToolsIfColorPicked(); // 💡 색을 바꾸면 툴 취소

            var rect = hueBarArea.rect;
            _hue = Mathf.Clamp01((localPoint.x - rect.x) / rect.width);

            UpdateHueSelectorPosition();
            RegenerateSVTexture();
            UpdateDisplay();
        }

        private void UpdateSVSelectorPosition()
        {
            var rect = svArea.rect;
            svSelector.anchorMin = Vector2.zero;
            svSelector.anchorMax = Vector2.zero;
            svSelector.anchoredPosition = new Vector2(rect.width * _saturation, rect.height * _value);
        }

        private void UpdateHueSelectorPosition()
        {
            var rect = hueBarArea.rect;
            hueSelector.anchorMin = new Vector2(0, 0);
            hueSelector.anchorMax = new Vector2(0, 1);
            hueSelector.anchoredPosition = new Vector2(rect.width * _hue, 0);
        }

        public void SetColor(Color color)
        {
            Color.RGBToHSV(color, out _hue, out _saturation, out _value);
            RegenerateSVTexture();
            UpdateSVSelectorPosition();
            UpdateHueSelectorPosition();
            UpdateDisplay();

            // 색상이 외부(스포이드 등)에서 세팅될 때 UI 색상 반영
            if (!IsEyedropperMode && eyedropperPreviewColor != null)
                eyedropperPreviewColor.color = color;
        }

        private void RegenerateSVTexture()
        {
            int w = _svTexture.width;
            int h = _svTexture.height;
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float s = (float)x / (w - 1);
                    _svTexture.SetPixel(x, y, Color.HSVToRGB(_hue, s, v));
                }
            }
            _svTexture.Apply();
            svImage.texture = _svTexture;
        }

        private void UpdateDisplay()
        {
            var color = SelectedColor;
            colorPreview.color = color;
            if (hexLabel != null) hexLabel.text = $"#{ColorUtility.ToHtmlStringRGB(color)}";
            OnColorChanged?.Invoke(color);
        }

        private void SetupBrushSizeSliders()
        {
            if (brushSizeSlider == null) return;
            brushSizeSlider.wholeNumbers = true;
            brushSizeSlider.minValue = 1;
            brushSizeSlider.maxValue = maxBrushSize;
            brushSizeSlider.value = 3;
            brushSizeSlider.onValueChanged.AddListener(OnBrushSizeSliderChanged);
            UpdateBrushSizeLabel();
        }

        private void OnBrushSizeSliderChanged(float value)
        {
            UpdateBrushSizeLabel();
            OnBrushSizeChanged?.Invoke((int)value);
        }

        public void SetBrushSize(int size)
        {
            if (brushSizeSlider == null) return;
            brushSizeSlider.value = Mathf.Clamp(size, 1, maxBrushSize);
        }

        private void UpdateBrushSizeLabel()
        {
            if (brushSizeLabel != null)
                brushSizeLabel.text = $"{(int)brushSizeSlider.value}px";
        }

        public void SetEyedropperActive(bool active) { SetEyedropper(active); } // 호환성 유지

        public void SetEyedropperPreview(Color color)
        {
            if (eyedropperPreviewColor != null)
                eyedropperPreviewColor.color = color;
        }

        private void OnDestroy()
        {
            if (_svTexture != null) Destroy(_svTexture);
        }
    }
}