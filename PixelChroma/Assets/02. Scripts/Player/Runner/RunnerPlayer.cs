using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

namespace PixelChroma
{
    public class RunnerPlayer : PlayerBase
    {
        private const float PaintRatioSyncInterval = 0.5f;

        [Header("Free Cam Settings")]
        [Tooltip("자유 시점(고정 모드)일 때 카메라의 이동 속도")]
        [SerializeField] private float freeCamSpeed = 15f;

        //[SerializeField] private GameObject visual;
        [SerializeField] private Collider2D bodyCollider;

        [Header("Wet Debuff Settings")]
        [Tooltip("물에 맞았을 때 적용되는 이동 속도 배율 (예: 0.5 = 기본 속도의 50%)")]
        [SerializeField, Range(0f, 1f)] private float wetSpeedMultiplier = 0.5f;

        [Header("Paint Mode Settings")]
        [Tooltip("페인트 모드를 켰을 때 적용되는 속도 배율 (예: 0.2 = 평소 속도의 20%로 매우 느려짐)")]
        [SerializeField, Range(0f, 1f)] private float paintModeSpeedMultiplier = 0.2f;
        protected override bool CanTogglePaintMode => !_isDead;// 사망여부를 Paint진입가능여부로 설정


        [Header("Camouflage")]
        [Tooltip("위장으로 인정되는 최소 색칠 비율 (0.3 = 30%)")]
        [SerializeField, Range(0f, 1f)] private float camouflagedThreshold = 0.3f;

        [Header("Expose Skill")]
        [SerializeField] private float exposeDuration = 1f;
        [SerializeField] private float exposeCooldown = 1f;

        [Tooltip("서로 안보이게 할때 껏다켯다할 렌더러")]
        [SerializeField] private SpriteRenderer visualRenderer;

        [SyncVar] private bool _isDead;
        private bool _isVisuallyDead;
        [SyncVar] private float _paintRatio;

        [SyncVar(hook = nameof(OnWetStateChanged))]
        private bool _isWet;

        private float _baseMoveSpeed;
        private float _wetTimer = 0f;
        private float _lastExposeTime = float.NegativeInfinity;
        private float _lastExposeTimeServer = float.NegativeInfinity;
        private float _lastPaintRatioSyncTime;

        private bool _isDeathSpectatorLocked;
        private SpectatorController _spectator;
        private UIInGameHUD_Runner _runnerHUD;

        public bool IsDead => _isDead;
        public bool IsWet => _isWet;
        public bool IsCamouflaged => _paintRatio >= camouflagedThreshold;

        protected override void Awake()
        {
            base.Awake();
            _baseMoveSpeed = moveSpeed;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // 닉네임 가시성과 같은 신호를 재사용해서, 누군가의 역할/사망 상태가 바뀔 때마다 내 몸도 다시 판단합니다.
            NicknameVisibilityChanged += RefreshVisualVisibilityForLocalViewer;

            // Hide→Seek→Check 전환마다 은닉 여부 자체가 바뀌므로, 상태 전환도 직접 구독합니다.
            GameRoundManager.OnTimeStateChanged += HandleTimeStateChangedForVisibility;

            RefreshVisualVisibilityForLocalViewer();
        }

        public override void OnStopClient()
        {
            NicknameVisibilityChanged -= RefreshVisualVisibilityForLocalViewer;
            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChangedForVisibility;
            base.OnStopClient();
        }

        protected override void Update()
        {
            base.Update();

            if (isServer && _isWet)
            {
                _wetTimer -= Time.deltaTime;
                if (_wetTimer <= 0)
                {
                    _isWet = false;
                }
            }

            if (!isLocalPlayer) return;

            // TODO: 테스트용 폼 전환 — 나중에 제거
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) paintCanvas.SwitchForm(PlayerPaintCanvas.PaintFormType.Default);
                if (kb.f2Key.wasPressedThisFrame) paintCanvas.SwitchForm(PlayerPaintCanvas.PaintFormType.Test1);
            }

            SyncPaintRatioIfNeeded();

            if (IsChatActive) return;

            if (Input.ExposeSkill.WasPressedThisFrame())
                TryUseExposeSkill();

            HandleSpectateInput();
        }

        protected override void OnLocalPlayerStarted()
        {
            ShowHUDAsync().Forget();

            _spectator = new SpectatorController(this, cinemachineCamera, freeCamSpeed);
            _spectator.OnTargetNameChanged += HandleSpectateTargetChanged;
            PaintController.OnPaintModeChanged += HandlePaintModeChanged;
        }

        protected override void OnLocalPlayerStopped()
        {
            PaintController.UnbindHUD();
            _runnerHUD = null;

            _spectator?.Dispose();
            _spectator = null;

            if (_spectator != null)
                _spectator.OnTargetNameChanged -= HandleSpectateTargetChanged;
            PaintController.OnPaintModeChanged -= HandlePaintModeChanged;
        }

        protected override void HandleMovement()
        {
            if (Rb == null) return;

            if (_spectator != null && _spectator.IsSpectating)
            {
                Rb.linearVelocity = Vector2.zero;

                if (_spectator.Mode == SpectateMode.Free)
                {
                    Vector2 rawInput = Input.Move.ReadValue<Vector2>();
                    _spectator.HandleFreeCamMovement(rawInput);
                }

                return;
            }

            base.HandleMovement();
        }

        protected override Vector2 GetMoveInput()
        {
            return _spectator is { IsSpectating: true } ? Vector2.zero : base.GetMoveInput();
        }

        [Server]
        public void ApplyWetState(float duration)
        {
            _isWet = true;
            _wetTimer = duration;
        }

        [Server]
        public void ServerTakeEraseDamage(Vector2 hitWorldPos, float aoeRadius)
        {
            if (paintCanvas == null) return;

            Vector2Int centerPixel = paintCanvas.WorldToPixel(hitWorldPos);
            float lossyScale = transform.lossyScale.x;
            float localAoeRadius = aoeRadius / lossyScale;
            int pixelRadius = Mathf.RoundToInt(localAoeRadius * paintCanvas.PixelsPerUnit);

            RpcTakeEraseDamageByPixel(centerPixel, pixelRadius);
        }

        //--------------------------------------사망시 로직(모드따라 다름)------------------------------------------
        [Server]
        public void ServerNotifyCaught()
        {
            if (_isDead) return;
            Debug.Log($"[Runner] {netId}번 플레이어 아웃!");
            GameRoundManager.Instance?.CurrentRule?.OnRunnerCaught(this);
        }

        [Server]
        public void ServerBecomeSpectator() // Basic모드
        {
            _isDead = true;
            RpcBecomeSpectator();
            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.NotifyRunnerDead();
        }

        [Server]
        public void ServerConvertToSeeker() // Infection모드
        {
            var netManager = NetworkManager.singleton as ChromaNetworkManager;
            netManager?.ServerConvertRunnerToSeeker(this);

            GameRoundManager.Instance?.NotifyRunnerDead();
        }

        //----------------------------------------------------------------------------------------------------

        //--------------------------------------가시성 로직(모드따라 다름, 판단은 GameModeRule에 위임)------------------------------------------
        private void RefreshVisualVisibilityForLocalViewer()
        {
            if (visualRenderer == null) return;

            // 1순위: 죽었으면(사망 처리든 감염 전환이든) 무조건 숨김. 모드 규칙보다 항상 우선.
            if (_isVisuallyDead)
            {
                visualRenderer.enabled = false;
                return;
            }

            // 로컬 플레이어를 아직 못 찾으면 일단 보이게 (안전한 기본값)
            if (NetworkClient.localPlayer == null) { visualRenderer.enabled = true; return; }

            // 2순위: "보는 사람"이 러너가 아니거나(=시커),
            // 나 자신을 보는 경우면 은닉 규칙 대상이 아님 → 항상 보임
            var localPlayer = NetworkClient.localPlayer.GetComponent<PlayerBase>();
            if (localPlayer == null || localPlayer == (PlayerBase)this || localPlayer.role != PlayerRole.Runner)
            {
                visualRenderer.enabled = true;
                return;
            }

            var round = GameRoundManager.Instance;

            // 3순위: 정산 화면(CheckTime)에는 모드 규칙보다 우선해서 무조건 보임
            if (round != null && round.CurrentTimeState == TimeState.CheckTime)
            {
                visualRenderer.enabled = true;
                return;
            }

            // 4순위(남은 경우 = 러너가 다른 러너의 몸을 보는 상황): 모드 규칙에 위임
            bool canSee = round?.CurrentRule == null || round.CurrentRule.CanSeeRunnerBody(round.CurrentTimeState);
            visualRenderer.enabled = canSee;
        }

        //GameRoundManager.OnTimeStateChanged 구독용 콜백.시간대가 바뀔 때마다 닉네임 몸 다 
        private void HandleTimeStateChangedForVisibility(TimeState state, int remainingSeconds)
        {
            RaiseNicknameVisibilityChanged();
            RefreshVisualVisibilityForLocalViewer();
        }
        //----------------------------------------------------------------------------------------------------

        private void HandleSpectateInput()
        {
            if (_spectator == null) return;

            if (Input.ToggleSpectate.WasPressedThisFrame())
            {
                if (_spectator.IsSpectating && !_isDeathSpectatorLocked)
                {
                    _spectator.ExitSpectate();
                    _runnerHUD?.SetSpectatorMode(false);
                }
                else
                {
                    if (PaintController.IsPaintMode)
                        PaintController.TogglePaintMode();

                    _spectator.EnterSeekerMode();
                    _runnerHUD?.SetSpectatorMode(true);
                    _runnerHUD?.SetActiveSpectateMode(SpectateMode.Seeker);
                }
                return;
            }

            if (!_spectator.IsSpectating) return;

            if (Input.SpectateSeeker.WasPressedThisFrame() && _spectator.Mode != SpectateMode.Seeker)
            {
                _spectator.EnterSeekerMode();
                _runnerHUD?.SetActiveSpectateMode(SpectateMode.Seeker);
                return;
            }

            if (Input.SpectateRunner.WasPressedThisFrame() && _spectator.Mode != SpectateMode.Runner)
            {
                _spectator.EnterRunnerMode();
                _runnerHUD?.SetActiveSpectateMode(SpectateMode.Runner);
                return;
            }

            if (Input.SpectateFree.WasPressedThisFrame() && _spectator.Mode != SpectateMode.Free)
            {
                _spectator.EnterFreeMode();
                _runnerHUD?.SetActiveSpectateMode(SpectateMode.Free);
                return;
            }

            if (_spectator.Mode == SpectateMode.Runner || _spectator.Mode == SpectateMode.Seeker)
            {
                if (Input.CyclePrev.WasPressedThisFrame()) _spectator.CycleTarget(-1);
                if (Input.CycleNext.WasPressedThisFrame()) _spectator.CycleTarget(1);
            }
        }

        private void SyncPaintRatioIfNeeded()
        {
            if (paintCanvas == null) return;
            if (Time.time - _lastPaintRatioSyncTime < PaintRatioSyncInterval) return;

            _lastPaintRatioSyncTime = Time.time;
            float ratio = paintCanvas.GetPaintRatio();

            if (!Mathf.Approximately(_paintRatio, ratio))
                CmdUpdatePaintRatio(ratio);
        }

        [Command]
        private void CmdUpdatePaintRatio(float ratio)
        {
            _paintRatio = Mathf.Clamp01(ratio);
        }

        private void RecalculateMoveSpeed()
        {
            float targetSpeed = _baseMoveSpeed;

            if (_isWet)
                targetSpeed *= wetSpeedMultiplier;

            if (IsPaintMode)
                targetSpeed *= paintModeSpeedMultiplier;

            moveSpeed = targetSpeed;
        }

        [ClientRpc]
        private void RpcTakeEraseDamageByPixel(Vector2Int centerPixel, int pixelRadius)
        {
            if (paintCanvas == null) return;
            paintCanvas.EraseArea(centerPixel, pixelRadius);
        }

        [ClientRpc]
        private void RpcBecomeSpectator()
        {
            RaiseNicknameVisibilityChanged();

            _isVisuallyDead = true;
            if (bodyCollider != null) bodyCollider.enabled = false;
            RefreshVisualVisibilityForLocalViewer();

            if (isLocalPlayer)
            {
                _isDeathSpectatorLocked = true;

                if (PaintController.IsPaintMode)
                    PaintController.TogglePaintMode();

                _spectator?.EnterDeathSpectator();
                _runnerHUD?.SetSpectatorMode(true);
            }
        }

        private async UniTaskVoid ShowHUDAsync()
        {
            _runnerHUD = await UIManager.Instance.ShowScene<UIInGameHUD_Runner>();
            PaintController.BindHUD(_runnerHUD);
        }

        private void TryUseExposeSkill()
        {
            if (_isDead) return;

            if (Time.time < _lastExposeTime + exposeCooldown)
                return;

            _lastExposeTime = Time.time;

            if (_runnerHUD != null)
            {
                _runnerHUD.StartExposeSkillCooldown(exposeCooldown);
            }

            CmdExposeLocation();
        }

        [Server]
        public void ServerForceExpose(float duration)
        {
            if (_isDead) return;
            if (InGameManager.Instance == null) return;

            foreach (var seeker in InGameManager.Instance.Seekers)
            {
                seeker.TargetShowRunnerIndicator(this.netIdentity, duration);
            }
        }

        [Command]
        private void CmdExposeLocation()
        {
            if (InGameManager.Instance == null) return;
            if (Time.time < _lastExposeTimeServer + exposeCooldown) return;

            _lastExposeTimeServer = Time.time;

            foreach (var seeker in InGameManager.Instance.Seekers)
            {
                seeker.TargetShowRunnerIndicator(this.netIdentity, exposeDuration);
            }
            TargetActivateSeekerIndicator(exposeDuration);
        }

        [TargetRpc]
        private void TargetActivateSeekerIndicator(float duration)
        {
            _runnerHUD?.ActivateSeekerIndicator(duration);
        }

        private void OnWetStateChanged(bool oldVal, bool newVal)
        {
            RecalculateMoveSpeed();
        }

        private void HandleSpectateTargetChanged(string targetName, bool isSelf)
        {
            _runnerHUD?.SetSpectateTargetName(targetName, isSelf);
        }

        private void HandlePaintModeChanged(bool isPaint)
        {
            RecalculateMoveSpeed();

            if (isPaint && !_isDeathSpectatorLocked && _spectator != null && _spectator.IsSpectating)
            {
                _spectator.ExitSpectate();
                _runnerHUD?.SetSpectatorMode(false);
            }
        }
    }
}
