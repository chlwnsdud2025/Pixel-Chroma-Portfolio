using Mirror;
using PixelChroma.UI;
using System;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

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

        [Header("Nickname UI")]
        [SerializeField] private TMP_Text nicknameText;

        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        public string playerName = "Player";

        [SyncVar(hook = nameof(OnRoleChanged))]
        public PlayerRole role = PlayerRole.None;

        protected CinemachineCamera cinemachineCamera;

        private PlayerInputActions _input;
        private PlayerPaintController _paintController;
        private Rigidbody2D _rb;
        private Camera _mainCamera;
        private PixelPerfectCamera _pixelPerfectCamera;
        private PlayerNicknameDisplay _nicknameDisplay;
        private int _originalPPU;
        private bool _isChatActive = false;

        private static event Action NicknameVisibilityChanged;

        public virtual bool IsSpectator => false;

        protected PlayerInputActions Input => _input;
        protected PlayerPaintController PaintController => _paintController;

        protected virtual void Update()
        {
            if (!isLocalPlayer) return;
            if (_isChatActive) return;

            if (_input.TogglePaint.WasPressedThisFrame())
                _paintController.TogglePaintMode();

            HandleScrollWheel();

            if (_paintController.IsPaintMode)
                _paintController.Tick();

            UpdateAnimation();
        }

        protected virtual void FixedUpdate()
        {
            if (!isLocalPlayer) return;
            if (_isChatActive) return;
            HandleMovement();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _nicknameDisplay ??= new PlayerNicknameDisplay(this, nicknameText);
            _nicknameDisplay.Refresh();
            _nicknameDisplay.UpdateVisibility();
            NicknameVisibilityChanged += _nicknameDisplay.UpdateVisibility;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_nicknameDisplay != null)
                NicknameVisibilityChanged -= _nicknameDisplay.UpdateVisibility;
        }

        public override void OnStartLocalPlayer()
        {
            _rb = GetComponent<Rigidbody2D>();

            _input = new PlayerInputActions(inputActions);
            _input.EnableAll();

            _paintController = new PlayerPaintController(this, paintCanvas, _input);

            UIChat.OnChatToggled += HandleChatToggled;

            SetupCinemachineFollow();
            OnLocalPlayerStarted();

            _paintController.RefreshCursor();
            RaiseNicknameVisibilityChanged();

            string mySavedName = PlayerPrefs.GetString(UIMainMenuNickname.NicknamePrefsKey, GetDefaultSteamName());
            CmdRegisterMyNameWithManager(mySavedName);
        }

        public override void OnStopLocalPlayer()
        {
            _input?.DisableAll();
            UIChat.OnChatToggled -= HandleChatToggled;
            _paintController?.Dispose();

            OnLocalPlayerStopped();
        }

        protected virtual void OnLocalPlayerStarted() { }
        protected virtual void OnLocalPlayerStopped() { }

        public static string GetDefaultSteamName()
        {
            try
            {
                if (Steamworks.SteamAPI.IsSteamRunning())
                {
                    return Steamworks.SteamFriends.GetPersonaName();
                }
            }
            catch (System.Exception) { }
            return "PixelPlayer_" + UnityEngine.Random.Range(1000, 9999);
        }

        protected static void RaiseNicknameVisibilityChanged() => NicknameVisibilityChanged?.Invoke();

        protected virtual void HandleMovement()
        {
            if (_rb == null) return;

            Vector2 input = _input.Move.ReadValue<Vector2>();
            _rb.linearVelocity = input.sqrMagnitude > 0.01f
                ? input.normalized * moveSpeed
                : Vector2.zero;
        }

        protected virtual void UpdateAnimation()
        {
            if (animator == null) return;

            Vector2 input = _input.Move.ReadValue<Vector2>();
            bool moving = input.sqrMagnitude > 0.01f;

            animator.SetBool(IsMoving, moving);
            if (moving)
            {
                animator.SetFloat(MoveX, input.x);
                animator.SetFloat(MoveY, input.y);
            }
        }

        public virtual CursorType GetDefaultCursorType()
        {
            return CursorType.Default;
        }

        [Command]
        private void CmdRegisterMyNameWithManager(string nameToRegister)
        {
            if (string.IsNullOrEmpty(nameToRegister)) return;

            if (NetworkManager.singleton is ChromaNetworkManager netManager)
            {
                string trimmed = nameToRegister.Trim();
                netManager.RegisterNickname(connectionToClient.connectionId, trimmed);
                playerName = trimmed;
            }
        }

        private void SetupCinemachineFollow()
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
            if (cinemachineCamera != null)
                cinemachineCamera.Follow = transform;

            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                _pixelPerfectCamera = _mainCamera.GetComponent<PixelPerfectCamera>();
                if (_pixelPerfectCamera != null)
                    _originalPPU = _pixelPerfectCamera.assetsPPU;
            }
        }

        private void HandleScrollWheel()
        {
            float scroll = GetScrollValue();
            if (Mathf.Approximately(scroll, 0f)) return;

            if (IsAltHeld())
            {
                HandleZoom(scroll);
                return;
            }

            if (_paintController != null && _paintController.IsPaintMode)
                _paintController.HandleBrushScroll(scroll);
        }

        private void HandleZoom(float scroll)
        {
            if (_pixelPerfectCamera == null) return;

            int current = _pixelPerfectCamera.assetsPPU;
            int delta = scroll > 0 ? zoomStepPPU : -zoomStepPPU;
            _pixelPerfectCamera.assetsPPU = Mathf.Clamp(current + delta, _originalPPU, maxPPU);
        }

        private float GetScrollValue()
        {
            if (Mouse.current == null) return 0f;
            return Mouse.current.scroll.y.ReadValue();
        }

        private bool IsAltHeld()
        {
            return _input.ZoomModifier.IsPressed();
        }

        private void OnPlayerNameChanged(string oldName, string newName)
        {
            _nicknameDisplay?.Refresh();
        }

        private void OnRoleChanged(PlayerRole oldRole, PlayerRole newRole)
        {
            RaiseNicknameVisibilityChanged();
        }

        private void HandleChatToggled(bool isActive)
        {
            _isChatActive = isActive;

            if (_isChatActive)
            {
                if (_rb != null) _rb.linearVelocity = Vector2.zero;
                if (animator != null) animator.SetBool(IsMoving, false);
            }
        }
    }
}
