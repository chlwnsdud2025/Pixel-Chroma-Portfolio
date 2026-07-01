using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using Mirror;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using PixelChroma.UI;

namespace PixelChroma
{
    public abstract class PlayerBase : NetworkBehaviour
    {
        protected const string ActionMapName = "PlayerControll";
        protected const string MoveActionName = "Move";
        protected const string TogglePaintActionName = "TogglePaint";
        protected const string EyedropperActionName = "Eyedropper";
        protected const string ZoomModifierActionName = "ZoomModifier";

        protected static readonly int MoveX = Animator.StringToHash("MoveX");
        protected static readonly int MoveY = Animator.StringToHash("MoveY");
        protected static readonly int IsMoving = Animator.StringToHash("IsMoving");

        [Header("Input")]
        public InputActionAsset inputActions;

        [Header("References")]
        public Animator animator;
        public PlayerPaintCanvas paintCanvas;

        [Header("Settings")]
        public float moveSpeed = 5f;

        [Header("Zoom (PPU)")]
        [SerializeField] private int maxPPU = 64;
        [SerializeField] private int zoomStepPPU = 2;

        [SyncVar]
        public PlayerRole role = PlayerRole.None;

        public event Action<bool> OnPaintModeChanged;
        public event Action<int> OnBrushSizeScrolled;
        public event Action<bool> OnEyedropperStateChanged;
        public event Action<Color> OnEyedropperPreview;
        public event Action<Color> OnEyedropperPicked;
        public Func<Color> PaintColorProvider;

        protected InputAction moveAction;
        protected InputAction togglePaintAction;
        protected InputAction eyedropperAction;
        protected InputAction zoomModifierAction;
        protected CinemachineCamera cinemachineCamera;
        protected bool isPaintMode;

        private Rigidbody2D _rb;
        private PixelPerfectCamera _pixelPerfectCamera;
        private int _originalPPU;
        private bool _isEyedropperActive;
        private bool _eyedropperLocked;
        private Color _eyedropperColor;
        private Texture2D _eyedropperCapture;
        private Coroutine _eyedropperCoroutine;

        public override void OnStartLocalPlayer()
        {
            _rb = GetComponent<Rigidbody2D>();

            var map = inputActions.FindActionMap(ActionMapName);
            moveAction = map.FindAction(MoveActionName);
            togglePaintAction = map.FindAction(TogglePaintActionName);
            eyedropperAction = map.FindAction(EyedropperActionName);
            zoomModifierAction = map.FindAction(ZoomModifierActionName);
            moveAction.Enable();
            togglePaintAction.Enable();
            eyedropperAction.Enable();
            zoomModifierAction.Enable();

            SetupCinemachineFollow();
            OnLocalPlayerStarted();
        }

        protected virtual void OnLocalPlayerStarted() { }

        public override void OnStopLocalPlayer()
        {
            moveAction?.Disable();
            togglePaintAction?.Disable();
            eyedropperAction?.Disable();
            zoomModifierAction?.Disable();

            StopEyedropperCapture();
            if (_eyedropperCapture != null)
            {
                Destroy(_eyedropperCapture);
                _eyedropperCapture = null;
            }

            OnLocalPlayerStopped();
        }

        protected virtual void OnLocalPlayerStopped() { }

        private void SetupCinemachineFollow()
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
            if (cinemachineCamera != null)
                cinemachineCamera.Follow = transform;

            var mainCam = Camera.main;
            if (mainCam != null)
            {
                _pixelPerfectCamera = mainCam.GetComponent<PixelPerfectCamera>();
                if (_pixelPerfectCamera != null)
                    _originalPPU = _pixelPerfectCamera.assetsPPU;
            }
        }

        protected virtual void Update()
        {
            if (!isLocalPlayer) return;

            if (togglePaintAction.WasPressedThisFrame())
                TogglePaintMode();

            HandleScrollWheel();

            if (isPaintMode)
            {
                HandleEyedropper();
                HandlePainting();
            }

            UpdateAnimation();
        }

        protected virtual void FixedUpdate()
        {
            if (!isLocalPlayer) return;
            HandleMovement();
        }

        protected virtual void HandleMovement()
        {
            if (_rb == null) return;

            Vector2 input = moveAction.ReadValue<Vector2>();
            _rb.linearVelocity = input.sqrMagnitude > 0.01f
                ? input.normalized * moveSpeed
                : Vector2.zero;
        }

        private void UpdateAnimation()
        {
            if (animator == null) return;

            Vector2 input = moveAction.ReadValue<Vector2>();
            bool moving = input.sqrMagnitude > 0.01f;

            animator.SetBool(IsMoving, moving);
            if (moving)
            {
                animator.SetFloat(MoveX, input.x);
                animator.SetFloat(MoveY, input.y);
            }
        }

        protected void HandleZoom()
        {
            if (_pixelPerfectCamera == null || Mouse.current == null) return;

            float scroll = Mouse.current.scroll.y.ReadValue();
            if (Mathf.Approximately(scroll, 0f)) return;

            if (zoomModifierAction.IsPressed())
            {
                int current = _pixelPerfectCamera.assetsPPU;
                int delta = scroll > 0 ? zoomStepPPU : -zoomStepPPU;
                _pixelPerfectCamera.assetsPPU = Mathf.Clamp(current + delta, _originalPPU, maxPPU);
            }
        }

        protected float GetScrollValue()
        {
            if (Mouse.current == null) return 0f;
            return Mouse.current.scroll.y.ReadValue();
        }

        protected bool IsAltHeld()
        {
            return zoomModifierAction.IsPressed();
        }

        private void HandleScrollWheel()
        {
            float scroll = GetScrollValue();
            if (Mathf.Approximately(scroll, 0f)) return;

            if (IsAltHeld())
            {
                HandleZoom();
                return;
            }

            if (isPaintMode && paintCanvas != null && _boundPicker != null)
            {
                int max = _boundPicker.MaxBrushSize;
                int current = paintCanvas.BrushSize;
                int next = scroll > 0 ? current + 1 : current - 1;
                next = Mathf.Clamp(next, 1, max);
                paintCanvas.BrushSize = next;
                OnBrushSizeScrolled?.Invoke(next);
            }
        }

        private void TogglePaintMode()
        {
            isPaintMode = !isPaintMode;
            OnPaintModeChanged?.Invoke(isPaintMode);
        }

        private void HandleEyedropper()
        {
            bool spaceHeld = eyedropperAction.IsPressed();

            if (!spaceHeld)
                _eyedropperLocked = false;

            if (spaceHeld && !_isEyedropperActive && !_eyedropperLocked)
            {
                _isEyedropperActive = true;
                OnEyedropperStateChanged?.Invoke(true);
                _eyedropperCoroutine = StartCoroutine(EyedropperCaptureLoop());
            }
            else if (!spaceHeld && _isEyedropperActive)
            {
                StopEyedropperCapture();
                _isEyedropperActive = false;
                OnEyedropperPicked?.Invoke(_eyedropperColor);
                OnEyedropperStateChanged?.Invoke(false);
            }

            if (_isEyedropperActive && Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame)
            {
                StopEyedropperCapture();
                _isEyedropperActive = false;
                _eyedropperLocked = true;
                OnEyedropperPicked?.Invoke(_eyedropperColor);
                OnEyedropperStateChanged?.Invoke(false);
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
                    OnEyedropperPreview?.Invoke(_eyedropperColor);
                }
            }
        }

        private void StopEyedropperCapture()
        {
            if (_eyedropperCoroutine != null)
            {
                StopCoroutine(_eyedropperCoroutine);
                _eyedropperCoroutine = null;
            }
        }

        private void HandlePainting()
        {
            if (_isEyedropperActive) return;
            if (paintCanvas == null || Mouse.current == null) return;
            if (!Mouse.current.leftButton.isPressed) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            worldPos.z = 0;

            Color color = PaintColorProvider?.Invoke() ?? Color.white;

            Vector2Int pixel = paintCanvas.WorldToPixel(worldPos);
            if (paintCanvas.IsInBounds(pixel.x, pixel.y))
                paintCanvas.PaintAt(pixel.x, pixel.y, color);
        }

        private IPaintHUD _boundHUD;
        private UIColorPicker _boundPicker;

        protected void BindPaintHUD(IPaintHUD hud)
        {
            if (hud == null) return;

            _boundHUD = hud;
            _boundPicker = hud.ColorPicker;

            OnPaintModeChanged += hud.SetPaintMode;

            if (_boundPicker != null)
            {
                PaintColorProvider = () => _boundPicker.SelectedColor;
                OnBrushSizeScrolled += _boundPicker.SetBrushSize;
                OnEyedropperStateChanged += _boundPicker.SetEyedropperActive;
                OnEyedropperPreview += _boundPicker.SetEyedropperPreview;
                OnEyedropperPicked += _boundPicker.SetColor;
                _boundPicker.OnBrushSizeChanged += OnUIBrushSizeChanged;

                if (paintCanvas != null)
                    paintCanvas.BrushSize = _boundPicker.BrushSize;
            }
        }

        protected void UnbindPaintHUD()
        {
            if (_boundPicker != null)
                _boundPicker.OnBrushSizeChanged -= OnUIBrushSizeChanged;

            OnPaintModeChanged = null;
            OnBrushSizeScrolled = null;
            OnEyedropperStateChanged = null;
            OnEyedropperPreview = null;
            OnEyedropperPicked = null;
            PaintColorProvider = null;

            _boundHUD = null;
            _boundPicker = null;
        }

        private void OnUIBrushSizeChanged(int size)
        {
            if (paintCanvas != null)
                paintCanvas.BrushSize = size;
        }
    }
}
