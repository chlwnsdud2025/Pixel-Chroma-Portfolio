using UnityEngine;
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

        [Header("Settings")]
        public float moveSpeed = 5f;

        [Header("Zoom (PPU)")]
        [SerializeField] private int maxPPU = 64;
        [SerializeField] private int zoomStepPPU = 2;

        [SyncVar]
        public PlayerRole role = PlayerRole.None;

        protected InputAction moveAction;
        protected CinemachineCamera cinemachineCamera;
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

            HandleZoom();
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
    }
}
