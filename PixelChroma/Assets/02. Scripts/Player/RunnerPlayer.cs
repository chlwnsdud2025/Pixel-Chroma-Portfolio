using UnityEngine;
using Mirror;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace PixelChroma
{
    public class RunnerPlayer : PlayerBase
    {
        public static readonly List<RunnerPlayer> AllRunners = new();

        [Header("Runner Settings")]
        [Tooltip("캐릭터의 이동 속도를 설정합니다.")]
        public float runnerMoveSpeed = 5f;

        [Header("Free Cam Settings")]
        [Tooltip("자유 시점(고정 모드)일 때 카메라의 이동 속도")]
        public float freeCamSpeed = 15f;

        [SyncVar] public bool isDead = false;
        private bool _isFrozen = false;
        private GameObject _freeCamTarget;

        // 관전 대상을 순회하기 위한 인덱스
        private int _spectateRunnerIndex = -1;
        private int _spectateSeekerIndex = -1;

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

        public bool IsSpectating => isDead;

        // ── 네트워크 스폰 시 명부(캐싱 리스트)에 등록/해제 ────────────────
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!AllRunners.Contains(this)) AllRunners.Add(this);

            if (paintCanvas != null)
                paintCanvas.OnEraseFadeCompleted += HandleEraseFadeCompleted;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            AllRunners.Remove(this);

            if (paintCanvas != null)
                paintCanvas.OnEraseFadeCompleted -= HandleEraseFadeCompleted;
        }
        // ──────────────────────────────────────────────────────────────────

        protected override void OnLocalPlayerStarted()
        {
            moveSpeed = runnerMoveSpeed;
            ShowHUDAsync().Forget(); // HUD 정상 호출

            _freeCamTarget = new GameObject("FreeCamTarget");
            _freeCamTarget.transform.position = transform.position;
        }

        protected override void OnLocalPlayerStopped()
        {
            UnbindPaintHUD();

            if (_freeCamTarget != null)
                Destroy(_freeCamTarget);
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

        // 현재 중첩(wetStack)에 비례해서 실제 이동속도를 깎아내는 함수
        private void RecalculateMoveSpeed()
        {
            if (!isWet || wetStack == 0)
            {
                moveSpeed = runnerMoveSpeed; // 원래 속도로 복구
            }
            else
            {
                float currentMultiplier = 1f - (wetStack * speedDebuffPerHit);
                currentMultiplier = Mathf.Max(currentMultiplier, minSpeedMultiplier);
                moveSpeed = runnerMoveSpeed * currentMultiplier;

                Debug.Log($"[디버프] 현재 {wetStack}중첩! 이동 속도가 {moveSpeed} 로 감소됨.");
            }
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
                // 💡 픽셀이 다 지워졌다고 죽는 로직은 어몽어스 킬 기획에 의해 비활성화합니다.
                // CheckIfCaught(); 
            }
        }

        /* 💡 기존의 픽셀 0개 아웃 로직 (사용 안 함)
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
            Debug.Log($"[Runner] {netId}번 플레이어 아웃! (처형당함)");
            // TODO: 게임 매니저에 아웃 알림 연동

            RpcBecomeSpectator();
        }

        [ClientRpc]
        private void RpcBecomeSpectator()
        {
            isDead = true;
            _isFrozen = true;

            // 1. 캐릭터 숨기기 (렌더러, 콜라이더, 캔버스 끄기)
            if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.enabled = false;
            if (paintCanvas != null) paintCanvas.gameObject.SetActive(false);

            // 2. 당사자 화면에서만 자유 시점(관전) 세팅
            if (isLocalPlayer)
            {
                if (_freeCamTarget == null)
                {
                    _freeCamTarget = new GameObject($"FreeCamTarget_{netId}");
                    _freeCamTarget.transform.position = transform.position;
                }

                var vcam = FindObjectOfType<Unity.Cinemachine.CinemachineCamera>();
                if (vcam != null) vcam.Follow = _freeCamTarget.transform;

                Debug.Log("사망: 영구 관전 모드 진입 완료!");
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
            if (AllRunners.Count == 0) return;

            if (_spectateRunnerIndex >= AllRunners.Count)
                _spectateRunnerIndex = -1;

            _spectateRunnerIndex = (_spectateRunnerIndex + 1) % AllRunners.Count;

            var target = AllRunners[_spectateRunnerIndex];
            if (target != null)
                SetSpectateTarget(target.transform);
        }

        private void CycleSpectateSeeker()
        {
            if (SeekerPlayer.AllSeekers.Count == 0) return;

            if (_spectateSeekerIndex >= SeekerPlayer.AllSeekers.Count)
                _spectateSeekerIndex = -1;

            _spectateSeekerIndex = (_spectateSeekerIndex + 1) % SeekerPlayer.AllSeekers.Count;

            var target = SeekerPlayer.AllSeekers[_spectateSeekerIndex];
            if (target != null)
                SetSpectateTarget(target.transform);
        }

        private void SetSpectateTarget(Transform targetTransform)
        {
            if (targetTransform == null || cinemachineCamera == null) return;

            _isFrozen = true;

            if (GetComponent<Rigidbody2D>() != null)
                GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;

            cinemachineCamera.Follow = targetTransform;
        }

        // 💡 통합 및 최적화된 이동 로직 (생존/사망 공용)
        protected override void HandleMovement()
        {
            if (GetComponent<Rigidbody2D>() == null) return;

            Vector2 input = moveAction.ReadValue<Vector2>();

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
                        }
                        else
                        {
                            // 산 유저: 관전 모드 끄고 즉시 이동 재개
                            _isFrozen = false;
                            if (cinemachineCamera != null) cinemachineCamera.Follow = transform;
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
            Vector2 input = _isFrozen ? Vector2.zero : moveAction.ReadValue<Vector2>();
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
            var hud = await UIManager.Instance.ShowScene<UIInGameHUD>();
            BindPaintHUD(hud);
        }
    }
}