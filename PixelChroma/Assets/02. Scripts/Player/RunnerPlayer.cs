using UnityEngine;
using Mirror;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;
using UnityEngine.InputSystem;

namespace PixelChroma
{
    public class RunnerPlayer : PlayerBase
    {
        [Header("Runner Settings")]
        [Tooltip("캐릭터의 이동 속도를 설정합니다.")]
        public float runnerMoveSpeed = 5f;

        [Header("Free Cam Settings")]
        [Tooltip("자유 시점(고정 모드)일 때 카메라의 이동 속도")]
        public float freeCamSpeed = 15f;

        [SerializeField] private GameObject visual;
        [SerializeField] private Collider2D bodyCollider;

        [SyncVar] public bool isDead = false;
        private bool _isFrozen = false;
        private GameObject _freeCamTarget;

        // 관전 대상을 순회하기 위한 인덱스
        private int _spectateRunnerIndex = -1;
        private int _spectateSeekerIndex = -1;

        private UISpectatorPopup _spectatorPopup;
        private UIInGameHUD_Runner _runnerHUD;

        // ── 물에 젖음 및 디버프 상태 ────────────────────────────────
        [Header("Status Effect")]
        [SyncVar(hook = nameof(OnWetStateChanged))]
        public bool isWet = false;

        [SyncVar(hook = nameof(OnWetStackChanged))]
        public int wetStack = 0;
        private float _wetTimer = 0f;

        [Header("Wet Debuff Settings")]
        [Tooltip("물에 한 번 맞을 때마다 감소하는 이동 속도 비율 (예: 0.2 = 20% 감소)")]
        [SerializeField] private float speedDebuffPerHit = 0.2f;
        [Tooltip("아무리 많이 맞아도 최소한 보장되는 이동 속도 비율 (예: 0.3 = 기본 속도의 30% 유지)")]
        [SerializeField] private float minSpeedMultiplier = 0.3f;

        [Header("Paint Mode Settings")]
        [Tooltip("페인트 모드를 켰을 때 적용되는 속도 배율 (예: 0.2 = 평소 속도의 20%로 매우 느려짐)")]
        [SerializeField] private float paintModeSpeedMultiplier = 0.2f;

        [Header("Expose Skill")]
        public float exposeDuration = 1f;  // 화살표가 떠있는 시간
        public float exposeCooldown = 1f; // 스킬 쿨타임
        private float _lastExposeTime = -100f;

        public override bool IsSpectator => isDead;
        public bool IsSpectating => IsSpectator;

        // ── 네트워크 스폰 시 명부(캐싱 리스트)에 등록/해제 ────────────────
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (InGameManager.Instance != null)
                InGameManager.Instance.RegisterPlayer(this);

            if (paintCanvas != null)
                paintCanvas.OnEraseFadeCompleted += HandleEraseFadeCompleted;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (InGameManager.Instance != null)
                InGameManager.Instance.UnregisterPlayer(this);

            if (paintCanvas != null)
                paintCanvas.OnEraseFadeCompleted -= HandleEraseFadeCompleted;
        }
        // ──────────────────────────────────────────────────────────────────

        protected override void OnLocalPlayerStarted()
        {
            moveSpeed = runnerMoveSpeed;
            ShowHUDAsync().Forget();

            _freeCamTarget = new GameObject("FreeCamTarget");
            _freeCamTarget.transform.position = transform.position;

            PaintController.OnPaintModeChanged += HandlePaintModeChanged;
        }

        protected override void OnLocalPlayerStopped()
        {
            PaintController.UnbindHUD();
            _runnerHUD = null;

            if (_freeCamTarget != null)
                Destroy(_freeCamTarget);

            PaintController.OnPaintModeChanged -= HandlePaintModeChanged;
        }

        // 통합된 Update: 서버 타이머 처리 + 클라이언트 입력 감지
        protected override void Update()
        {
            base.Update();

            // [서버 전용] 젖은 상태 타이머 감소
            if (isServer && isWet)
            {
                _wetTimer -= Time.deltaTime;
                if (_wetTimer <= 0)
                {
                    isWet = false;
                    wetStack = 0; // 마르면 중첩 초기화
                }
            }

            // 내 로컬 플레이어가 아니면 아래 입력 로직 무시
            if (!isLocalPlayer) return;

            // [생존/관전 공통 단축키 로직]
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    TryUseExposeSkill();
                }

                // P키: 살아있을 때만 자유 시점 토글 허용
                if (!isDead && Keyboard.current.pKey.wasPressedThisFrame)
                    ToggleFreezeMode();

                // O키, I키: 죽었든 살았든 다른 유저 관전 허용
                if (Keyboard.current.oKey.wasPressedThisFrame) CycleSpectateRunner();
                if (Keyboard.current.iKey.wasPressedThisFrame) CycleSpectateSeeker();
            }
        }

        // ── 물총 피격 및 상태이상 로직 ──────────────────────────────────────

        [Server]
        public void ApplyWetState(float duration)
        {
            isWet = true;
            _wetTimer = duration;
            wetStack++;
        }

        private void OnWetStackChanged(int oldVal, int newVal)
        {
            RecalculateMoveSpeed();
        }

        private void OnWetStateChanged(bool oldVal, bool newVal)
        {
            RecalculateMoveSpeed();
        }

        private void HandlePaintModeChanged(bool isPaint)
        {
            RecalculateMoveSpeed();
        }



        // 현재 중첩(wetStack)에 비례해서 실제 이동속도를 깎아내는 함수
        private void RecalculateMoveSpeed()
        {
            if (!isLocalPlayer) return;

            float targetSpeed = runnerMoveSpeed; // 원래 속도에서 시작

            // 1. 물에 젖었을 때 이속 감소 적용
            if (isWet && wetStack > 0)
            {
                float currentMultiplier = 1f - (wetStack * speedDebuffPerHit);
                currentMultiplier = Mathf.Max(currentMultiplier, minSpeedMultiplier);
                targetSpeed *= currentMultiplier;
            }

            // 2. 페인트 모드일 때 정밀 조준을 위해 속도 대폭 감소 (물에 젖은 상태와 중첩됨)
            if (PaintController.IsPaintMode)
            {
                targetSpeed *= paintModeSpeedMultiplier;
            }

            moveSpeed = targetSpeed;
            // Debug.Log($"[디버프] 이동 속도 갱신됨: {moveSpeed} (젖음스택: {wetStack}, 페인트모드: {isPaintMode})");
        }
        // ── 피격 및 지워짐 로직 ──────────────────────────────────────────

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

        [ClientRpc]
        public void RpcTakeEraseDamageByPixel(Vector2Int centerPixel, int pixelRadius)
        {
            if (paintCanvas == null) return;

            // 맞은 영역이 서서히 지워집니다.
            paintCanvas.EraseArea(centerPixel, pixelRadius);
        }

        private void HandleEraseFadeCompleted()
        {
            if (isLocalPlayer)
            {
                // 픽셀이 다 지워졌다고 죽는 로직은 어몽어스 킬 기획에 의해 비활성화합니다.
                // CheckIfCaught(); 
            }
        }

        /* 기존의 픽셀 0개 아웃 로직 (사용 안 함)
        private void CheckIfCaught()
        {
            if (paintCanvas == null) return;
            bool isAlive = paintCanvas.HasAnyPaintedPixels();
            if (!isAlive) CmdNotifyCaught();
        }
        */

        // ── 킬/사망 처리 및 관전 모드 전환 ────────────────────────────────

        // Seeker가 호출할 수 있도록 requiresAuthority = false 설정 필수!
        [Command(requiresAuthority = false)]
        public void CmdNotifyCaught()
        {
            if (isDead) return;

            Debug.Log($"[Runner] {netId}번 플레이어 아웃! (처형당함)");

            isDead = true;

            RpcBecomeSpectator();

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.NotifyRunnerDead();
        }

        [ClientRpc]
        private void RpcBecomeSpectator()
        {
            _isFrozen = true;

            // 관전 전환 시 닉네임 가시성 재평가 (사망한 로컬 플레이어는 모든 닉네임이 보임)
            RaiseNicknameVisibilityChanged();

            if (visual != null) visual.SetActive(false);
            if (bodyCollider != null) bodyCollider.enabled = false;

            if (isLocalPlayer)
            {
                if (_freeCamTarget == null)
                {
                    _freeCamTarget = new GameObject($"FreeCamTarget_{netId}");
                    _freeCamTarget.transform.position = transform.position;
                }

                if (cinemachineCamera != null)
                    cinemachineCamera.Follow = _freeCamTarget.transform;

                UpdateSpectatorUIAsync("자유 시점").Forget();
            }
        }

        private async UniTaskVoid UpdateSpectatorUIAsync(string targetName)
        {
            // 팝업이 아직 화면에 없다면 새로 엽니다.
            if (_spectatorPopup == null)
            {
                _spectatorPopup = await UIManager.Instance.ShowPopup<UISpectatorPopup>();
            }

            // 이름을 갱신합니다.
            if (_spectatorPopup != null)
            {
                _spectatorPopup.SetTargetName(targetName);
            }
        }

        // ── 이동, 애니메이션, 카메라 제어 ───────────────────────────────────

        public void ToggleFreezeMode()
        {
            _isFrozen = !_isFrozen;

            if (_isFrozen)
            {
                _freeCamTarget.transform.position = transform.position;
                if (cinemachineCamera != null)
                {
                    cinemachineCamera.Follow = _freeCamTarget.transform;
                }

                if (GetComponent<Rigidbody2D>() != null)
                {
                    GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                }
            }
            else
            {
                if (cinemachineCamera != null)
                {
                    cinemachineCamera.Follow = transform;
                }
            }
        }

        private void CycleSpectateRunner()
        {
            var runners = InGameManager.Instance?.Runners;
            if (runners == null || runners.Count == 0) return;

            int attemptCount = 0;

            while (attemptCount < runners.Count)
            {
                _spectateRunnerIndex = (_spectateRunnerIndex + 1) % runners.Count;
                var target = runners[_spectateRunnerIndex];

                // 타겟이 존재하고, '죽지 않은(!target.isDead)' 상태일 때만 관전 타겟으로 잡고 루프를 종료합니다.
                if (target != null && !target.isDead)
                {
                    SetSpectateTarget(target);
                    return;
                }

                attemptCount++;
            }

            Debug.Log("[Spectate] 현재 관전할 수 있는 살아있는 도망자가 없습니다.");
        }

        private void CycleSpectateSeeker()
        {
            var seekers = InGameManager.Instance?.Seekers;
            if (seekers == null || seekers.Count == 0) return;

            if (_spectateSeekerIndex >= seekers.Count)
                _spectateSeekerIndex = -1;

            _spectateSeekerIndex = (_spectateSeekerIndex + 1) % seekers.Count;

            var target = seekers[_spectateSeekerIndex];
            if (target != null)
                SetSpectateTarget(target);
        }

        private void SetSpectateTarget(PlayerBase targetPlayer)
        {
            if (targetPlayer == null || cinemachineCamera == null) return;

            _isFrozen = true;
            if (GetComponent<Rigidbody2D>() != null) GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;

            cinemachineCamera.Follow = targetPlayer.transform;

            UpdateSpectatorUIAsync(targetPlayer.playerName).Forget();
        }

        // 통합 및 최적화된 이동 로직 (생존/사망 공용)
        protected override void HandleMovement()
        {
            if (GetComponent<Rigidbody2D>() == null) return;

            Vector2 input = Input.Move.ReadValue<Vector2>();

            if (_isFrozen) // 고정 모드이거나 죽었을 때
            {
                GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;

                if (input.sqrMagnitude > 0.01f)
                {
                    // 누군가를 관전 중이었다면 즉시 해제
                    if (_spectateRunnerIndex != -1 || _spectateSeekerIndex != -1)
                    {
                        _spectateRunnerIndex = -1;
                        _spectateSeekerIndex = -1;

                        if (isDead)
                        {
                            // 죽은 유저: 내 시체 위치(FreeCamTarget)로 카메라만 복귀
                            if (cinemachineCamera != null) cinemachineCamera.Follow = _freeCamTarget.transform;
                            UpdateSpectatorUIAsync("자유 시점").Forget();
                        }
                        else
                        {
                            // 산 유저: 관전 모드 끄고 UI도 닫고 즉시 이동 재개
                            _isFrozen = false;
                            if (cinemachineCamera != null) cinemachineCamera.Follow = transform;

                            if (_spectatorPopup != null)
                            {
                                _spectatorPopup.OnClickBack(); // UIPopup의 창 닫기 기능 활용
                                _spectatorPopup = null; // 연결 해제
                            }

                            base.HandleMovement();
                        }
                    }
                    else
                    {
                        // 기본 자유 카메라 이동 (WASD)
                        if (_freeCamTarget != null)
                            _freeCamTarget.transform.position += (Vector3)input * freeCamSpeed * Time.fixedDeltaTime;
                    }
                }
            }
            else // 평상시 이동
            {
                base.HandleMovement();
            }
        }

        protected override void UpdateAnimation()
        {
            if (animator == null) return;

            // 고정 모드면 입력값을 강제로 (0,0)으로 만들어서 애니메이터에 전달
            Vector2 input = _isFrozen ? Vector2.zero : Input.Move.ReadValue<Vector2>();
            bool moving = input.sqrMagnitude > 0.01f;

            animator.SetBool(IsMoving, moving);
            if (moving)
            {
                animator.SetFloat(MoveX, input.x);
                animator.SetFloat(MoveY, input.y);
            }
        }

        private async UniTaskVoid ShowHUDAsync()
        {
            // 어드레서블 명부에 등록된 "도망자 전용 HUD 프리팹"을 스폰합니다.
            _runnerHUD = await UIManager.Instance.ShowScene<UIInGameHUD_Runner>();
            PaintController.BindHUD(_runnerHUD);
        }

        private void TryUseExposeSkill()
        {
            if (Time.time < _lastExposeTime + exposeCooldown)
            {
                //Debug.Log($"스킬 쿨타임 중입니다! 남은 시간: {(_lastExposeTime + exposeCooldown) - Time.time:F1}초");
                return;
            }

            _lastExposeTime = Time.time;
            //Debug.Log("[Runner] 위치 노출 스킬 사용!");

            if (_runnerHUD != null)
            {
                _runnerHUD.StartExposeSkillCooldown(exposeCooldown);
            }

            CmdExposeLocation(exposeDuration);
        }
        [Command]
        private void CmdExposeLocation(float duration)
        {
            if (InGameManager.Instance == null) return;
            foreach (var seeker in InGameManager.Instance.Seekers)
            {
                seeker.RpcShowRunnerIndicator(this.netIdentity, duration);
            }
        }

    }
}