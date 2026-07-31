using Mirror;
using PixelChroma.UI;
using System;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using DG.Tweening;


namespace PixelChroma
{
    public abstract class PlayerBase : NetworkBehaviour, IEscapeHandler
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
        [SerializeField] private TMP_Text likeCountText;
        [SerializeField] private LikeRiseEffect likeRiseEffect;

        [Header("Form Wheel")]
        [SerializeField] private RadialMenu formWheel;

        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        public string playerName = "Player";

        [SyncVar(hook = nameof(OnRoleChanged))]
        public PlayerRole role = PlayerRole.None;

        [SyncVar(hook = nameof(OnLikeCountChanged))]
        private int _likeCount;

        protected CinemachineCamera cinemachineCamera;

        private PlayerInputActions _input;
        private PlayerPaintController _paintController;
        private Camera _mainCamera;
        private PixelPerfectCamera _pixelPerfectCamera;
        private PlayerNicknameDisplay _nicknameDisplay;
        private Tween _zoomTween;
        private SpectatorController _spectator;
        private bool _isChatActive;
        private bool _likeEffectsReady;
        private bool _likedByLocal;

        protected static event Action NicknameVisibilityChanged;
        public static event Action OnAnyPlayerNameChanged;

        public int LikeCount => _likeCount;
        public bool CanReceiveLike => role == PlayerRole.Runner;
        public bool LikedByLocal => _likedByLocal;

        // 분리된 컴포넌트(SeekerWaterGun 등)가 페인트 모드 여부만 좁게 조회할 수 있도록 공개합니다.
        public bool IsPaintMode => _paintController != null && _paintController.IsPaintMode;

        protected PlayerInputActions Input => _input;
        protected PlayerPaintController PaintController => _paintController;
        protected SpectatorController Spectator => _spectator;
        protected Rigidbody2D Rb { get; private set; }
        protected bool IsChatActive => _isChatActive;
        protected bool IsCheckTimeSpectatorLocked { get; private set; }

        protected virtual UIInGameHUD InGameHUD => null;
        protected virtual bool CanTogglePaintMode => true;   // PaintMode 진입 가능 여부

        // CheckTime 동안에만 좋아요를 주고받을 수 있습니다. 클라이언트 입력 차단과 서버 검증에 함께 씁니다.
        private static bool IsLikeAllowedNow =>
            GameRoundManager.Instance != null
            && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime;

        protected virtual void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
        }

        protected virtual void Update()
        {
            if (!isLocalPlayer) return;
            if (_isChatActive) return;

            if (role != PlayerRole.Seeker && formWheel != null
                && (_spectator == null || !_spectator.IsSpectating))
            {
                if (_input.FormWheel.WasPressedThisFrame())
                    formWheel.Open();

                if (_input.FormWheel.WasReleasedThisFrame())
                {
                    int selected = formWheel.Close();
                    if (selected >= 0)
                    {
                        if (_paintController.IsPaintMode)
                            _paintController.TogglePaintMode();

                        paintCanvas.SwitchForm((PlayerPaintCanvas.PaintFormType)selected);
                    }
                }
            }

            if (formWheel != null && formWheel.IsOpen) return;

            if (CanTogglePaintMode && !IsCheckTimeSpectatorLocked && _input.TogglePaint.WasPressedThisFrame())
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
            //if (formWheel != null && formWheel.IsOpen) return; // 변신중 움직임 금지
            HandleMovement();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (connectionToClient != null)
            {
                var netManager = NetworkManager.singleton as ChromaNetworkManager;
                if (netManager != null)
                    _likeCount = netManager.GetLikeCount(connectionToClient.connectionId);
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // 스폰 시 인게임 명부에 통지합니다. 어떤 타입을 명부에 올릴지는 InGameManager가 결정합니다.
            if (InGameManager.Instance != null)
                InGameManager.Instance.RegisterPlayer(this);

            _nicknameDisplay ??= new PlayerNicknameDisplay(this, nicknameText, nicknameRoot, likeCountText);
            _nicknameDisplay.Refresh();
            _nicknameDisplay.UpdateVisibility();
            NicknameVisibilityChanged += _nicknameDisplay.UpdateVisibility;

            _likeEffectsReady = true;
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

            if (UIManager.Instance != null)
                UIManager.Instance.GameplayEscapeHandler = this;

            _paintController = new PlayerPaintController(this, paintCanvas, _input);
            _paintController.OnPaintModeChanged += HandlePaintModeChangedForZoom;
            _paintController.OnPaintModeChanged += _nicknameDisplay.HandlePaintModeChanged;

            UIChat.OnChatToggled += HandleChatToggled;

            SetupCinemachineFollow();
            OnLocalPlayerStarted();

            _paintController.RefreshCursor();
            RaiseNicknameVisibilityChanged();

            string mySavedName = NicknameService.LoadOrDefault();
            CmdRegisterMyNameWithManager(mySavedName);

            // 이미 칠해둔 다른 플레이어들의 현재 페인트 상태를 요청(late-join 동기화).
            CmdRequestPaintSnapshots();

            GameRoundManager.OnTimeStateChanged += HandleCheckTimeSpectator;
        }

        public override void OnStopLocalPlayer()
        {
            _input?.DisableAll();
            UIChat.OnChatToggled -= HandleChatToggled;

            if (UIManager.Instance != null && ReferenceEquals(UIManager.Instance.GameplayEscapeHandler, this))
                UIManager.Instance.GameplayEscapeHandler = null;

            if (_paintController != null)
            {
                _paintController.OnPaintModeChanged -= HandlePaintModeChangedForZoom;
                _paintController.OnPaintModeChanged -= _nicknameDisplay.HandlePaintModeChanged;
            }

            _paintController?.Dispose();

            GameRoundManager.OnTimeStateChanged -= HandleCheckTimeSpectator;

            OnLocalPlayerStopped();
        }

        protected virtual void OnLocalPlayerStarted() { }
        protected virtual void OnLocalPlayerStopped() { }

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
            if (this is RunnerPlayer runner && (runner.IsDead || runner.IsKnockedBack)) return;
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

        // ── ESC 처리 ──────────────────────────────────────────────────
        protected virtual bool TryExitSpectateByEscape() => false;
        public bool HandleEscape()
        {
            // 채팅 입력 중엔 게임플레이 취소를 소비만 하고 무시(설정 팝업 오작동 방지)
            if (IsChatActive) return true;

            if (TryExitSpectateByEscape()) return true;   // 1순위: 관전 종료(롤별 잠금 반영)

            if (_paintController != null && _paintController.IsPaintMode)  // 2순위: 페인트 종료
            {
                _paintController.TogglePaintMode();
                return true;
            }

            return false;
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
                if (_input.Like.WasPressedThisFrame()) TryLikeCurrentTarget();
            }
        }

        private void TryLikeCurrentTarget()
        {
            if (_spectator == null) return;
            if (!IsLikeAllowedNow) return;   // 좋아요는 CheckTime에만 가능합니다.

            var target = _spectator.CurrentTarget;
            if (target == null || target == this) return;
            if (!target.CanReceiveLike) return;    // 시커 관전 중에는 좋아요를 줄 수 없습니다.
            if (target.LikedByLocal) return;       // 이미 준 대상이면 서버까지 갈 필요가 없습니다.

            target.CmdAddLike();
            target._likedByLocal = true;   // 기록은 대상 오브젝트와 수명을 같이 합니다.

            RefreshLikeGuide();
        }

        // 좋아요 안내는 "지금 보고 있는 대상 한 명"에 대한 것이라 로컬 플레이어 HUD에서만 판정합니다.
        private void RefreshLikeGuide()
        {
            if (!isLocalPlayer) return;

            var target = _spectator?.CurrentTarget;

            InGameHUD?.SetLikeGuide(
                IsLikeAllowedNow
                && target != null
                && target != this
                && target.CanReceiveLike
                && !target.LikedByLocal);
        }

        private void HandleCheckTimeSpectator(TimeState state, int remainingSeconds) // CheckTime 전용
        {
            if (_spectator == null) return;

            if (state == TimeState.CheckTime)
            {
                IsCheckTimeSpectatorLocked = true;

                if (_paintController != null && _paintController.IsPaintMode)
                    _paintController.TogglePaintMode();

                if (!_spectator.IsSpectating)
                {
                    _spectator.EnterRunnerMode();
                    InGameHUD?.SetSpectatorMode(true);
                    InGameHUD?.SetActiveSpectateMode(SpectateMode.Runner);
                }

                RefreshLikeGuide();
                OnCheckTimeSpectatorEnter();
            }
            else
            {
                RefreshLikeGuide();

                if (IsCheckTimeSpectatorLocked)
                {
                    IsCheckTimeSpectatorLocked = false;
                    OnCheckTimeSpectatorExit();
                }
            }
        }

        protected virtual void OnCheckTimeSpectatorEnter() { }
        protected virtual void OnCheckTimeSpectatorExit() { }

        protected void InitializeSpectator()
        {
            _spectator = new SpectatorController(this, cinemachineCamera, freeCamSpeed);
            _spectator.OnTargetChanged += HandleSpectateTargetChanged;
        }

        protected void CleanupSpectator()
        {
            if (_spectator != null)
                _spectator.OnTargetChanged -= HandleSpectateTargetChanged;

            _spectator?.Dispose();
            _spectator = null;
        }

        private void HandleSpectateTargetChanged(PlayerBase target)
        {
            InGameHUD?.SetSpectateTarget(target, target == this);
            RefreshLikeGuide();
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

        [Command(requiresAuthority = false)]
        public void CmdAddLike(NetworkConnectionToClient sender = null)
        {
            if (sender == null) return;
            if (!IsLikeAllowedNow) return;
            if (!CanReceiveLike) return;
            if (connectionToClient == null) return;
            var senderPlayer = sender.identity?.GetComponent<PlayerBase>();
            if (senderPlayer == this) return;

            var netManager = NetworkManager.singleton as ChromaNetworkManager;
            if (netManager == null) return;

            // 클라이언트가 자체 판정으로 중복을 걸러주지만, 그건 UI 편의일 뿐이라
            // 카운트 정확성은 서버 장부(_likeRecords)로만 보장합니다.
            if (!netManager.AddLike(sender.connectionId, connectionToClient.connectionId))
                return;

            _likeCount = netManager.GetLikeCount(connectionToClient.connectionId);
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

        private void OnLikeCountChanged(int oldVal, int newVal)
        {
            _nicknameDisplay?.RefreshLikeCount();

            if (!_likeEffectsReady) return;   // 스폰 시 초기 동기화는 값만 반영하고 연출은 생략
            if (newVal <= oldVal) return;

            likeRiseEffect?.Play(newVal - oldVal);
        }

        private void HandleChatToggled(bool isActive)
        {
            _isChatActive = isActive;

            if (_isChatActive)
            {
                if (Rb != null) Rb.linearVelocity = Vector2.zero;
                if (animator != null) animator.SetBool(IsMoving, false);

                if (Rb != null) Rb.linearVelocity = Vector2.zero;
                if (animator != null) animator.SetBool(IsMoving, false);
            }
        }
    }
}
