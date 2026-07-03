using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace PixelChroma.UI
{
    public enum CursorType
    {
        Default,
        Brush,
        Eraser,
        Eyedropper,
        Crosshair
    }

    public class CursorManager : MonoSingleton<CursorManager>
    {
        [Header("UI References")]
        public RectTransform cursorRect;
        public Image cursorImage;

        [Header("Cursor Settings")]
        public Sprite defaultCursor;
        public Vector2 defaultPivot = new Vector2(0f, 1f);

        public Sprite brushCursor;
        public Vector2 brushPivot = new Vector2(0f, 1f);

        public Sprite eraserCursor;
        public Vector2 eraserPivot = new Vector2(0f, 1f);

        public Sprite eyedropperCursor;
        public Vector2 eyedropperPivot = new Vector2(0f, 1f);

        public Sprite crosshairCursor;
        public Vector2 crosshairPivot = new Vector2(0.5f, 0.5f);

        private Canvas _parentCanvas;

        [HideInInspector] public Vector2? overrideScreenPosition = null;

        private void InitCanvas()
        {
            if (cursorRect != null && _parentCanvas == null)
            {
                var canvas = cursorRect.GetComponentInParent<Canvas>();
                // Cursor 자신에게 Nested Canvas가 붙어 있으므로, 좌표 기준은 항상 루트 Canvas를 사용
                if (canvas != null) _parentCanvas = canvas.rootCanvas;
            }
        }

        private void Start()
        {
            Cursor.visible = false;
            SetCursorType(CursorType.Default);
        }

        private void Update()
        {
            if (cursorRect == null || cursorImage == null) return;
            if (_parentCanvas == null) InitCanvas();
            if (_parentCanvas == null || Mouse.current == null) return;

            Vector2 mousePos = overrideScreenPosition ?? Mouse.current.position.ReadValue();
            // 캔버스의 크기, 해상도, 렌더 모드(Overlay/Camera)에 상관없이 
            // 스크린 마우스 좌표를 캔버스 내부의 정확한 로컬 좌표로 100% 완벽하게 변환합니다.
            RectTransform canvasRect = _parentCanvas.GetComponent<RectTransform>();
            Camera cam = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mousePos, cam, out Vector2 localPos))
            {
                cursorRect.localPosition = localPos;
            }
        }

        public void SetCursorType(CursorType type, float brushScale = 1f)
        {
            if (cursorImage == null || cursorRect == null) return;

            cursorRect.localScale = Vector3.one;

            switch (type)
            {
                case CursorType.Default:
                    cursorImage.sprite = defaultCursor;
                    cursorRect.pivot = defaultPivot;
                    break;
                case CursorType.Brush:
                    cursorImage.sprite = brushCursor;
                    cursorRect.pivot = brushPivot;
                    cursorRect.localScale = new Vector3(brushScale, brushScale, 1f);
                    break;
                case CursorType.Eraser:
                    cursorImage.sprite = eraserCursor;
                    cursorRect.pivot = eraserPivot;
                    cursorRect.localScale = new Vector3(brushScale, brushScale, 1f);
                    break;
                case CursorType.Eyedropper:
                    cursorImage.sprite = eyedropperCursor;
                    cursorRect.pivot = eyedropperPivot;
                    break;
                case CursorType.Crosshair:
                    cursorImage.sprite = crosshairCursor;
                    cursorRect.pivot = crosshairPivot;
                    break;
            }
        }

        
    }
}