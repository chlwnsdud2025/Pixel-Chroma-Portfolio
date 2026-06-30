using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using Mirror;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace PixelChroma
{
    public abstract class PlayerBase : NetworkBehaviour
    {
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
        public Func<Color> PaintColorProvider;
        public Func<int> MaxBrushSizeProvider;

        protected InputAction moveAction;
        protected CinemachineCamera cinemachineCamera;
        protected bool isPaintMode;
        private PixelPerfectCamera _pixelPerfectCamera;
        private int _originalPPU;

        public override void OnStartLocalPlayer()
        {
            var map = inputActions.FindActionMap("PlayerControll");
            moveAction = map.FindAction("Move");
            moveAction.Enable();

            SetupCinemachineFollow();
            OnLocalPlayerStarted();
        }

        protected virtual void OnLocalPlayerStarted() { }

        public override void OnStopLocalPlayer()
        {
            moveAction?.Disable();
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

            if (Keyboard.current != null && Keyboard.current[Key.F].wasPressedThisFrame)
                TogglePaintMode();

            HandleScrollWheel();

            if (isPaintMode)
                HandlePainting();

            HandleMovement();
        }

        protected virtual void HandleMovement()
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            bool moving = input.sqrMagnitude > 0.01f;

            if (moving)
            {
                Vector3 move = new Vector3(input.x, input.y, 0).normalized;
                transform.position += move * moveSpeed * Time.deltaTime;
            }

            if (animator != null)
            {
                animator.SetBool(IsMoving, moving);
                if (moving)
                {
                    animator.SetFloat(MoveX, input.x);
                    animator.SetFloat(MoveY, input.y);
                }
            }
        }

        protected void HandleZoom()
        {
            if (_pixelPerfectCamera == null || Mouse.current == null) return;

            float scroll = Mouse.current.scroll.y.ReadValue();
            if (Mathf.Approximately(scroll, 0f)) return;

            bool altHeld = Keyboard.current != null &&
                           (Keyboard.current[Key.LeftAlt].isPressed || Keyboard.current[Key.RightAlt].isPressed);

            if (altHeld)
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
            return Keyboard.current != null &&
                   (Keyboard.current[Key.LeftAlt].isPressed || Keyboard.current[Key.RightAlt].isPressed);
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

            if (isPaintMode && paintCanvas != null)
            {
                int max = MaxBrushSizeProvider?.Invoke() ?? 20;
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

        private void HandlePainting()
        {
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

        protected void ClearPaintEvents()
        {
            OnPaintModeChanged = null;
            OnBrushSizeScrolled = null;
            PaintColorProvider = null;
            MaxBrushSizeProvider = null;
        }
    }
}
