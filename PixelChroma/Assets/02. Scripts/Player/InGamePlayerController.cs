using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using Mirror;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using PixelChroma.UI;

namespace PixelChroma
{
    public class InGamePlayerController : NetworkBehaviour
    {
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");

        [Header("Input")]
        public InputActionAsset inputActions;

        [Header("References")]
        public Animator animator;
        public SpriteRenderer spriteRenderer;
        public PlayerPaintCanvas paintCanvas;

        [Header("Settings")]
        public float moveSpeed = 5f;

        [Header("Zoom (PPU)")]
        [SerializeField] private int maxPPU = 64;
        [SerializeField] private int zoomStepPPU = 2;

        private InputAction moveAction;
        private UIInGameHUD _hud;
        private bool _isPaintMode;
        private CinemachineCamera _cinemachineCamera;
        private PixelPerfectCamera _pixelPerfectCamera;
        private int _originalPPU;

        public override void OnStartLocalPlayer()
        {
            var map = inputActions.FindActionMap("PlayerControll");
            moveAction = map.FindAction("Move");
            moveAction.Enable();

            SetupCinemachineFollow();
            ShowHUDAsync().Forget();
        }

        private void SetupCinemachineFollow()
        {
            _cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
            if (_cinemachineCamera != null)
                _cinemachineCamera.Follow = transform;

            var mainCam = Camera.main;
            if (mainCam != null)
            {
                _pixelPerfectCamera = mainCam.GetComponent<PixelPerfectCamera>();
                if (_pixelPerfectCamera != null)
                    _originalPPU = _pixelPerfectCamera.assetsPPU;
            }
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

        private void Update()
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

        private void HandleMovement()
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

        private void HandleScrollWheel()
        {
            if (Mouse.current == null) return;
            float scroll = Mouse.current.scroll.y.ReadValue();
            if (Mathf.Approximately(scroll, 0f)) return;

            bool altHeld = Keyboard.current != null &&
                           (Keyboard.current[Key.LeftAlt].isPressed || Keyboard.current[Key.RightAlt].isPressed);

            if (altHeld)
            {
                if (_pixelPerfectCamera != null)
                {
                    int current = _pixelPerfectCamera.assetsPPU;
                    int delta = scroll > 0 ? zoomStepPPU : -zoomStepPPU;
                    _pixelPerfectCamera.assetsPPU = Mathf.Clamp(current + delta, _originalPPU, maxPPU);
                }
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