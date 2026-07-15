using Mirror;
using PixelChroma.UI;
using System;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

namespace PixelChroma
{
    public abstract class PlayerBase : NetworkBehaviour
    {
        protected static readonly int MoveX = Animator.StringToHash("MoveX");
        protected static readonly int MoveY = Animator.StringToHash("MoveY");
        protected static readonly int IsMoving = Animator.StringToHash("IsMoving");

        private const int OriginalPPU = 16;

        [Header("Input")]
        public InputActionAsset inputActions;

        [Header("References")]
        public Animator animator;
        public PlayerPaintCanvas paintCanvas;

        [Header("Settings")]
        public float moveSpeed = 5f;

        [Header("Zoom (PPU)")]
        [SerializeField] private int maxPPU = 64;
        [SerializeField] private int zoomStepPPU = 5;
        [SerializeField] private float manualZoomTweenDuration = 0.15f;
        [Tooltip("페인트 모드에서 빠져나올 때 최대 줌아웃 상태로 돌아가는 데 걸리는 시간(초)")]
        [SerializeField] private float forcedZoomOutDuration = 0.4f;

        [Header("Free Cam Settings")]
        [Tooltip("자유 시점(고정 모드)일 때 카메라의 이동 속도")]
        [SerializeField] private float freeCamSpeed = 15f;

        [Header("Nickname UI")]
        [SerializeField] private GameObject nicknameRoot;
        [SerializeField] private TMP_Text nicknameText;

        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        public string playerName = "Player";

        [SyncVar(hook = nameof(OnRoleChanged))]
        public PlayerRole role = PlayerRole.None;

        protected CinemachineCamera cinemachineCamera;

        private PlayerInputActions _input;
        private PlayerPaintController _paintController;
        private Camera _mainCamera;
        private PixelPerfectCamera _pixelPerfectCamera;
        private PlayerNicknameDisplay _nicknameDisplay;
        private Coroutine _zoomResetCoroutine;
        private Tween _zoomTween;
        private SpectatorController _spectator;
        private bool _isChatActive = false;

        protected static event Action NicknameVisibilityChanged;
        public static event Action OnAnyPlayerNameChanged;

        protected PlayerInputActions Input => _input;
        protected PlayerPaintController PaintController => _paintController;
        protected Rigidbody2D Rb { get; private set; }
        protected bool IsChatActive => _isChatActive;
        protected SpectatorController Spectator => _spectator;
        protected virtual UIInGameHUD InGameHUD => null;
        protected virtual bool CanTogglePaintMode => true;//PaintMode 진입가능여부

        // 분리된 컴포넌트(SeekerWaterGun 등)가 페인트 모드 여부만 좁게 조회할 수 있도록 공개합니다.
        public bool IsPaintMode => _paintController != null && _paintController.IsPaintMode;

        protected virtual void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
        }

        protected virtual void Update()
        {
            if (!isLocalPlayer) return;
            if (_isChatActive) return;

            if (CanTogglePaintMode && _input.TogglePaint.WasPressedThisFrame())
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

            // 스폰 시 인게임 명부에 통지합니다. 어떤 타입을 명부에 올릴지는 InGameManager가 결정합니다.
            if (InGameManager.Instance != null)
                InGameManager.Instance.RegisterPlayer(this);

            _nicknameDisplay ??= new PlayerNicknameDisplay(this, nicknameText, nicknameRoot);
            _nicknameDisplay.Refresh();
            _nicknameDisplay.UpdateVisibility();
            NicknameVisibilityChanged += _nicknameDisplay.UpdateVisibility;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (InGameManager.Instance != null)
                InGameManager.Instance.UnregisterPlayer(this);

            if (_nicknameDisplay != null)
                NicknameVisibilityChanged -= _nicknameDisplay.UpdateVisibility;
        }

        public override void OnStartLocalPlayer()
        {
            _input = new PlayerInputActions(Instantiate(inputActions));
            _input.EnableAll();

            _paintController = new PlayerPaintController(this, paintCanvas, _input);
            _paintController.OnPaintModeChanged += HandlePaintModeChangedForZoom;
            _paintController.OnPaintModeChanged += _nicknameDisplay.HandlePaintModeChanged;

            UIChat.OnChatToggled += HandleChatToggled;

            SetupCinemachineFollow();
            OnLocalPlayerStarted();

            _paintController.RefreshCursor();
            RaiseNicknameVisibilityChanged();

            string mySavedName = PlayerPrefs.GetString(UIMainMenuNickname.NicknamePrefsKey, GetDefaultSteamName());
            CmdRegisterMyNameWithManager(mySavedName);

            // 이미 칠해둔 다른 플레이어들의 현재 페인트 상태를 요청(late-join 동기화).
            CmdRequestPaintSnapshots();
        }

        public override void OnStopLocalPlayer()
        {
            _input?.DisableAll();
            UIChat.OnChatToggled -= HandleChatToggled;

            if (_paintController != null)
            {
                _paintController.OnPaintModeChanged -= HandlePaintModeChangedForZoom;
                _paintController.OnPaintModeChanged -= _nicknameDisplay.HandlePaintModeChanged;
            }
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

        // 이동/애니메이션이 참조할 입력값. 자식이 오버라이드해서 입력을 가공할 수 있습니다(예: 프리즈 시 0).
        protected virtual Vector2 GetMoveInput()
        {
            if (_spectator != null && _spectator.IsSpectating) return Vector2.zero;
            return _input.Move.ReadValue<Vector2>();
        }

        protected virtual void HandleMovement()
        {
            if (Rb == null) return;

            if (_spectator != null && _spectator.IsSpectating)
            {
                Rb.linearVelocity = Vector2.zero;
                if (_spectator.Mode == SpectateMode.Free)
                    _spectator.HandleFreeCamMovement(_input.Move.ReadValue<Vector2>());
                return;
            }

            Vector2 input = GetMoveInput();
            Rb.linearVelocity = input.sqrMagnitude > 0.01f
                ? input.normalized * moveSpeed
                : Vector2.zero;
        }

        protected virtual void UpdateAnimation()
        {
            if (animator == null) return;

            Vector2 input = GetMoveInput();
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

        // ── 관전 공용 ──────────────────────────────────────────────────
        protected virtual void HandleSpectateInput()
        {
            if (_spectator == null || !_spectator.IsSpectating) return;

            if (_input.SpectateRunner.WasPressedThisFrame() && _spectator.Mode != SpectateMode.Runner)
            {
                _spectator.EnterRunnerMode();
                InGameHUD?.SetActiveSpectateMode(SpectateMode.Runner);
                return;
            }

            if (_input.SpectateSeeker.WasPressedThisFrame() && _spectator.Mode != SpectateMode.Seeker)
            {
                _spectator.EnterSeekerMode();
                InGameHUD?.SetActiveSpectateMode(SpectateMode.Seeker);
                return;
            }

            if (_input.SpectateFree.WasPressedThisFrame() && _spectator.Mode != SpectateMode.Free)
            {
                _spectator.EnterFreeMode();
                InGameHUD?.SetActiveSpectateMode(SpectateMode.Free);
                return;
            }

            if (_spectator.Mode == SpectateMode.Runner || _spectator.Mode == SpectateMode.Seeker)
            {
                if (_input.CyclePrev.WasPressedThisFrame()) _spectator.CycleTarget(-1);
                if (_input.CycleNext.WasPressedThisFrame()) _spectator.CycleTarget(1);
            }
        }

        protected void InitializeSpectator()
        {
            _spectator = new SpectatorController(this, cinemachineCamera, freeCamSpeed);
            _spectator.OnTargetNameChanged += HandleSpectateTargetChanged;
        }

        protected void CleanupSpectator()
        {
            if (_spectator != null)
                _spectator.OnTargetNameChanged -= HandleSpectateTargetChanged;
            _spectator?.Dispose();
            _spectator = null;
        }

        private void HandleSpectateTargetChanged(string targetName, bool isSelf)
        {
            InGameHUD?.SetSpectateTargetName(targetName, isSelf);
        }

        // 스폰된 다른 플레이어 캔버스들이 각자의 현재 페인트를 요청자에게 보내도록 한다.
        [Command]
        private void CmdRequestPaintSnapshots()
        {
            var conn = connectionToClient;
            if (conn == null) return;

            foreach (var identity in NetworkServer.spawned.Values)
            {
                if (identity == null || identity == netIdentity) continue;   // 내 캔버스는 빈 상태
                var canvas = identity.GetComponentInChildren<PlayerPaintCanvas>();
                if (canvas != null)
                    canvas.SendPaintSnapshotTo(conn);
            }
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
                    _pixelPerfectCamera.assetsPPU = OriginalPPU;
            }
        }

        private void HandleScrollWheel()
        {
            float scroll = GetScrollValue();
            if (Mathf.Approximately(scroll, 0f)) return;

            if (IsAltHeld())
            {
                if (_paintController != null && _paintController.IsPaintMode)
                    _paintController.HandleBrushScroll(scroll);
                return;
            }

            if (_paintController != null && _paintController.IsPaintMode)
                HandleZoom(scroll);
        }

        private void HandleZoom(float scroll)
        {
            if (_pixelPerfectCamera == null) return;

            _zoomTween?.Kill();

            int current = _pixelPerfectCamera.assetsPPU;
            int delta = scroll > 0 ? zoomStepPPU : -zoomStepPPU;
            int target = Mathf.Clamp(current + delta, OriginalPPU, maxPPU);

            _zoomTween = DOTween.To(
                    () => _pixelPerfectCamera.assetsPPU,
                    v => _pixelPerfectCamera.assetsPPU = v,
                    target,
                    manualZoomTweenDuration)
                .SetEase(Ease.OutQuad);
        }

        private void HandlePaintModeChangedForZoom(bool isPaintMode)
        {
            if (isPaintMode) return;
            StartForcedZoomOut();
        }

        private void StartForcedZoomOut()
        {
            if (_pixelPerfectCamera == null) return;

            _zoomTween?.Kill();   // 이전에 남아있던 트윈(수동 줌이든 이전 줌아웃이든) 취소
            _zoomTween = DOTween.To(
                    () => _pixelPerfectCamera.assetsPPU,
                    v => _pixelPerfectCamera.assetsPPU = v,
                    OriginalPPU,
                    forcedZoomOutDuration)
                .SetEase(Ease.OutQuad);
        }

        private float GetScrollValue()
        {
            if (Mouse.current == null) return 0f;
            return Mouse.current.scroll.y.ReadValue();
        }

        private bool IsAltHeld()
        {
            return _input.BrushSizeModifier.IsPressed();
        }

        private void OnPlayerNameChanged(string oldName, string newName)
        {
            _nicknameDisplay?.Refresh();
            OnAnyPlayerNameChanged?.Invoke();
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
                if (Rb != null) Rb.linearVelocity = Vector2.zero;
                if (animator != null) animator.SetBool(IsMoving, false);
            }
        }
    }
}
