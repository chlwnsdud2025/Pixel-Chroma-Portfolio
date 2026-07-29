using Cysharp.Threading.Tasks;
using Mirror;
using PixelChroma.UI;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelChroma
{
    public class RunnerPlayer : PlayerBase
    {
        private const float PaintRatioSyncInterval = 0.5f;

        //[SerializeField] private GameObject visual;
        [SerializeField] private Collider2D bodyCollider;

        [Header("Wet Debuff Settings")]
        [Tooltip("물에 맞았을 때 적용되는 이동 속도 배율 (예: 0.5 = 기본 속도의 50%)")]
        [SerializeField, Range(0f, 1f)] private float wetSpeedMultiplier = 0.5f;

        [Header("Paint Mode Settings")]
        [Tooltip("페인트 모드를 켰을 때 적용되는 속도 배율 (예: 0.2 = 평소 속도의 20%로 매우 느려짐)")]
        [SerializeField, Range(0f, 1f)] private float paintModeSpeedMultiplier = 0.2f;
        protected override bool CanTogglePaintMode => !_isDead;// 사망여부를 Paint 진입 가능 여부로 설정

        [Header("Camouflage")]
        [Tooltip("위장으로 인정되는 최소 색칠 비율 (0.3 = 30%)")]
        [SerializeField, Range(0f, 1f)] private float camouflagedThreshold = 0.3f;

        [Header("Expose Skill")]
        [SerializeField] private float exposeDuration = 1f;
        [SerializeField] private float exposeCooldown = 1f;

        [Tooltip("사망 또는 서로 안보이게 할 때 껏다 켯다 할 렌더러")]
        [SerializeField] private SpriteRenderer visualRenderer;


        [Header("사망 시 넉백 테스트")]
        private Vector2 _lastKnockbackDir;
        [SerializeField] private float knockbackDuration = 0.4f;
        [SerializeField] private PhysicsMaterial2D bouncyMaterial;

        [Header("사망 연출")]
        [SerializeField] private float deathBlinkDuration = 1.5f;
        [SerializeField] private int deathBlinkCount = 3;
        private float _lastKnockbackImpulse;
        private float _lastKnockbackDrag;
        private float _lastKnockbackRotSpeed;

        [SyncVar(hook = nameof(OnDeadStateChanged))] private bool _isDead;
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
        private DeathSnapshot _deathSnapshot = new();
        private UIInGameHUD_Runner _runnerHUD;


        private bool _isKnockedBack;// 테스트용
        public bool IsKnockedBack => _isKnockedBack;
        protected override UIInGameHUD InGameHUD => _runnerHUD;

        public bool IsDead => _isDead;
        public bool IsWet => _isWet;
        public bool IsCamouflaged => _paintRatio >= camouflagedThreshold;

        public static event Action<string, string> OnPlayerCaught;

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

            SyncPaintRatioIfNeeded();

            if (IsChatActive) return;

            if (Input.ExposeSkill.WasPressedThisFrame())
                TryUseExposeSkill();

            HandleSpectateInput();
        }

        protected override void OnLocalPlayerStarted()
        {
            ShowHUDAsync().Forget();
            InitializeSpectator();
            PaintController.OnPaintModeChanged += HandlePaintModeChanged;
        }

        protected override void OnLocalPlayerStopped()
        {
            PaintController.UnbindHUD();
            _runnerHUD = null;

            CleanupSpectator();
            PaintController.OnPaintModeChanged -= HandlePaintModeChanged;
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
        public void ServerNotifyCaught(string catcherName, Vector2 knockbackDir = default,
    float impulse = 0f, float drag = 5f, float rotSpeed = 720f)
        {
            if (_isDead) return;
            _lastKnockbackDir = knockbackDir;
            _lastKnockbackImpulse = impulse;
            _lastKnockbackDrag = drag;
            _lastKnockbackRotSpeed = rotSpeed;
            _deathSnapshot.Capture(transform, paintCanvas);
            RpcNotifyCaught(catcherName, playerName);
            GameRoundManager.Instance?.CurrentRule?.OnRunnerCaught(this);
        }


        [ClientRpc]
        private void RpcNotifyCaught(string catcherName, string caughtName)
        {
            OnPlayerCaught?.Invoke(catcherName, caughtName);
        }

        [Server]
        public void ServerBecomeSpectator() // Basic모드
        {
            _isDead = true;

            // 넉백-------------------------------------
            if (_lastKnockbackDir.sqrMagnitude > 0.001f)
            {
                RpcKnockback(_lastKnockbackDir, _lastKnockbackImpulse, _lastKnockbackDrag, _lastKnockbackRotSpeed);
                StartCoroutine(DelayedBecomeSpectator());
            }
            else
            {
                RpcBecomeSpectator();
                if (GameRoundManager.Instance != null)
                    GameRoundManager.Instance.NotifyRunnerDead();
            }
            //------------------------------------------
            
        }

        [Server]
        private System.Collections.IEnumerator DelayedBecomeSpectator()
        {
            yield return new WaitForSeconds(knockbackDuration);
            RpcDeathSequence(GameMode.Basic);
        }

        [Server]
        public void ServerConvertToSeeker()// Infaction모드
        {
            if (_lastKnockbackDir.sqrMagnitude > 0.001f)
            {
                _isDead = true;
                RpcKnockback(_lastKnockbackDir, _lastKnockbackImpulse, _lastKnockbackDrag, _lastKnockbackRotSpeed);
                StartCoroutine(DelayedConvertToSeeker());
            }
            else
            {
                var netManager = NetworkManager.singleton as ChromaNetworkManager;
                netManager?.ServerConvertRunnerToSeeker(this);
                GameRoundManager.Instance?.NotifyRunnerDead();

            }

        }

        [Server]
        private System.Collections.IEnumerator DelayedConvertToSeeker()
        {
            yield return new WaitForSeconds(knockbackDuration);
            RpcDeathSequence(GameMode.Infection);
        }



        //-----------------------------------테스트-----------------
        [ClientRpc]
        private void RpcKnockback(Vector2 dir, float impulse, float drag, float rotSpeed)
        {
            // 모든 클라이언트에서 페인트 페이드
            if (paintCanvas != null)
                paintCanvas.FadeAllPaint(knockbackDuration);

            // 물리는 소유 클라이언트에서만
            if (isOwned && Rb != null)
            {
                _isKnockedBack = true;
                Rb.linearVelocity = Vector2.zero;
                Rb.linearDamping = drag;
                Rb.angularDamping = drag;
                Rb.sharedMaterial = bouncyMaterial;
                Rb.constraints = RigidbodyConstraints2D.None;
                Rb.angularVelocity = rotSpeed * (dir.x >= 0 ? -1f : 1f);
                Rb.AddForce(dir * impulse, ForceMode2D.Impulse);
                StartCoroutine(ResetKnockback());
            }
        }

        private System.Collections.IEnumerator ResetKnockback()
        {
            yield return new WaitForSeconds(knockbackDuration);
            _isKnockedBack = false;
            Rb.linearVelocity = Vector2.zero;
            Rb.angularVelocity = 0f;
            Rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            transform.rotation = Quaternion.identity;
        }
        //------------------------------------------
        [ClientRpc]
        private void RpcDeathSequence(GameMode mode)
        {
            StartCoroutine(DeathSequenceRoutine(mode));
        }

        private System.Collections.IEnumerator DeathSequenceRoutine(GameMode mode)
        {
            if (isOwned && Rb != null)
            {
                Rb.linearVelocity = Vector2.zero;
                Rb.angularVelocity = 0f;
            }
            transform.rotation = Quaternion.identity;

            // 1. 사망 애니메이션 (트리거가 준비되면 여기에 추가)
            // if (animator != null) animator.SetTrigger("Death");
            // yield return new WaitForSeconds(사망 애니메이션 길이);

            // 2. 깜빡임
            if (visualRenderer != null)
            {
                float blinkInterval = deathBlinkDuration / (deathBlinkCount * 2);
                for (int i = 0; i < deathBlinkCount; i++)
                {
                    visualRenderer.color = new Color(1f, 1f, 1f, 0.2f);
                    yield return new WaitForSeconds(blinkInterval);
                    visualRenderer.color = new Color(1f, 1f, 1f, 1f);
                    yield return new WaitForSeconds(blinkInterval);
                }
                visualRenderer.color = Color.white;
            }

            // 3. 관전 진입 (서버에서 호출해야 하므로 로컬→Command)
            if (isOwned)
                CmdDeathSequenceFinished(mode);
        }

        [Command]
        private void CmdDeathSequenceFinished(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Basic:
                    RpcRestoreDeathPosition(_deathSnapshot.Position);

                    if (_deathSnapshot.HasSnapshot && _deathSnapshot.MaskPixels != null)
                    {
                        RpcRestoreDeathPaint(_deathSnapshot.FormType, _deathSnapshot.MaskPixels);
                    }

                    RpcBecomeSpectator();

                    break;

                case GameMode.Infection:
                    RpcRestoreDeathPosition(_deathSnapshot.Position);

                    if (_deathSnapshot.HasSnapshot && _deathSnapshot.MaskPixels != null)
                    {
                        RpcRestoreDeathPaint(_deathSnapshot.FormType, _deathSnapshot.MaskPixels);
                    }

                    RpcBecomeGhost();

                    var netManager = NetworkManager.singleton as ChromaNetworkManager;
                    netManager?.ServerConvertRunnerToSeeker(this);

                    break;
            }

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.NotifyRunnerDead();
        }

        [ClientRpc]
        private void RpcBecomeGhost()
        {
            _isVisuallyDead = true;
            if (paintCanvas != null) paintCanvas.DisableActiveCollider();
            RefreshVisualVisibilityForLocalViewer();
            RaiseNicknameVisibilityChanged();
        }

        [ClientRpc]
        private void RpcRestoreDeathPosition(Vector3 pos)
        {
            transform.position = pos;
            transform.rotation = Quaternion.identity;
        }

        [ClientRpc]
        private void RpcRestoreDeathPaint(PlayerPaintCanvas.PaintFormType formType, Color32[] pixels)
        {
            if (paintCanvas != null)
                paintCanvas.RestoreMaskPixels(formType, pixels);
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
            if (state == TimeState.CheckTime && _isDead)
            {
                _isVisuallyDead = false;
            }

            RaiseNicknameVisibilityChanged();
            RefreshVisualVisibilityForLocalViewer();
        }
        //----------------------------------------------------------------------------------------------------

        protected override bool TryExitSpectateByEscape()
        {
            if (Spectator == null || !Spectator.IsSpectating) return false;

            // 사망 관전 / 체크타임 관전 중에는 ESC로 풀리지 않도록 유지
            if (_isDeathSpectatorLocked || IsCheckTimeSpectatorLocked) return false;

            Spectator.ExitSpectate();
            _runnerHUD?.SetSpectatorMode(false);
            return true;
        }

        protected override void HandleSpectateInput()
        {
            if (Spectator == null) return;

            if (Input.ToggleSpectate.WasPressedThisFrame())
            {
                if (Spectator.IsSpectating && !_isDeathSpectatorLocked && !IsCheckTimeSpectatorLocked)
                {
                    Spectator.ExitSpectate();
                    _runnerHUD?.SetSpectatorMode(false);
                }
                else
                {
                    if (PaintController.IsPaintMode)
                        PaintController.TogglePaintMode();

                    Spectator.EnterRunnerMode();
                    _runnerHUD?.SetSpectatorMode(true);
                    _runnerHUD?.SetActiveSpectateMode(SpectateMode.Runner);
                }
                return;
            }

            base.HandleSpectateInput();
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

            bool isCheckTime = GameRoundManager.Instance != null
                    && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime;

            _isVisuallyDead = !isCheckTime;
            if (paintCanvas != null) paintCanvas.DisableActiveCollider();
            RefreshVisualVisibilityForLocalViewer();

            if (isLocalPlayer)
            {
                _isDeathSpectatorLocked = true;

                if (PaintController.IsPaintMode)
                    PaintController.TogglePaintMode();

                Spectator?.EnterDeathSpectator();
                _runnerHUD?.SetSpectatorMode(true);
                _runnerHUD?.SetActiveSpectateMode(SpectateMode.Runner);
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
        }

        

        private void OnWetStateChanged(bool oldVal, bool newVal)
        {
            RecalculateMoveSpeed();
        }

        // 사망 상태 복제 시 모든 클라이언트에서 호출됩니다. 생존 인원 HUD 갱신을 트리거합니다.
        private void OnDeadStateChanged(bool oldVal, bool newVal)
        {
            if (InGameManager.Instance != null)
                InGameManager.Instance.NotifyRosterChanged();

            RaiseNicknameVisibilityChanged();
        }

        private void HandlePaintModeChanged(bool isPaint)
        {
            RecalculateMoveSpeed();

            if (isPaint && !_isDeathSpectatorLocked && Spectator != null && Spectator.IsSpectating)
            {
                Spectator.ExitSpectate();
                _runnerHUD?.SetSpectatorMode(false);
            }
        }


    }
}
