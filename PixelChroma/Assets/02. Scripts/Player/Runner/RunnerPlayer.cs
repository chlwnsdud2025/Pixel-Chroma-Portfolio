using UnityEngine;
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

        [SerializeField] private GameObject visual;
        [SerializeField] private Collider2D bodyCollider;

        [Header("Wet Debuff Settings")]
        [Tooltip("물에 맞았을 때 적용되는 이동 속도 배율 (예: 0.5 = 기본 속도의 50%)")]
        [SerializeField, Range(0f, 1f)] private float wetSpeedMultiplier = 0.5f;

        [Header("Paint Mode Settings")]
        [Tooltip("페인트 모드를 켰을 때 적용되는 속도 배율 (예: 0.2 = 평소 속도의 20%로 매우 느려짐)")]
        [SerializeField, Range(0f, 1f)] private float paintModeSpeedMultiplier = 0.2f;

        [Header("Camouflage")]
        [Tooltip("위장으로 인정되는 최소 색칠 비율 (0.3 = 30%)")]
        [SerializeField, Range(0f, 1f)] private float camouflagedThreshold = 0.3f;

        [Header("Expose Skill")]
        [SerializeField] private float exposeDuration = 1f;
        [SerializeField] private float exposeCooldown = 1f;

        [SyncVar] private bool _isDead;
        [SyncVar] private float _paintRatio;

        [SyncVar(hook = nameof(OnWetStateChanged))]
        private bool _isWet;

        private float _baseMoveSpeed;
        private float _wetTimer = 0f;
        private float _lastExposeTime = float.NegativeInfinity;
        private float _lastExposeTimeServer = float.NegativeInfinity;
        private float _lastPaintRatioSyncTime;
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
            PaintController.OnPaintModeChanged += HandlePaintModeChanged;
        }

        protected override void OnLocalPlayerStopped()
        {
            PaintController.UnbindHUD();
            _runnerHUD = null;

            _spectator?.Dispose();
            _spectator = null;

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

        [Server]
        public void ServerNotifyCaught()
        {
            if (_isDead) return;

            Debug.Log($"[Runner] {netId}번 플레이어 아웃! (처형당함)");

            _isDead = true;
            RpcBecomeSpectator();

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.NotifyRunnerDead();
        }

        private void HandleSpectateInput()
        {
            if (_spectator == null) return;

            if (Input.ToggleFreeCam.WasPressedThisFrame())
            {
                if (_spectator.Mode == SpectateMode.Free && !_isDead)
                    _spectator.ExitSpectate();
                else
                    _spectator.EnterFreeMode();
                return;
            }

            if (Input.SpectateRunner.WasPressedThisFrame())
            {
                if (_spectator.Mode == SpectateMode.Runner && !_isDead)
                    _spectator.ExitSpectate();
                else
                    _spectator.EnterRunnerMode();
                return;
            }

            if (Input.SpectateSeeker.WasPressedThisFrame())
            {
                if (_spectator.Mode == SpectateMode.Seeker && !_isDead)
                    _spectator.ExitSpectate();
                else
                    _spectator.EnterSeekerMode();
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

            if (visual != null) visual.SetActive(false);
            if (bodyCollider != null) bodyCollider.enabled = false;

            if (isLocalPlayer)
                _spectator?.EnterDeathSpectator();
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
        }

        private void OnWetStateChanged(bool oldVal, bool newVal)
        {
            RecalculateMoveSpeed();
        }

        private void HandlePaintModeChanged(bool isPaint)
        {
            RecalculateMoveSpeed();
        }
    }
}
