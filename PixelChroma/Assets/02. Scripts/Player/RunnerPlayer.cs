using UnityEngine;
using Mirror;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic; // 💡 List 사용을 위해 추가

namespace PixelChroma
{
    public class RunnerPlayer : PlayerBase
    {
        // 💡 씬에 존재하는 모든 도망자를 담아둘 전역(Static) 리스트
        public static readonly List<RunnerPlayer> AllRunners = new();

        [Header("Runner Settings")]
        [Tooltip("캐릭터의 이동 속도를 설정합니다.")]
        public float runnerMoveSpeed = 5f;

        [Header("Free Cam Settings")]
        [Tooltip("자유 시점(고정 모드)일 때 카메라의 이동 속도")]
        public float freeCamSpeed = 15f;
        private bool _isSpectating = false;

        private bool _isFrozen = false;
        private GameObject _freeCamTarget;

        // 관전 대상을 순회하기 위한 인덱스
        private int _spectateRunnerIndex = -1;
        private int _spectateSeekerIndex = -1;

        // ── 💡 네트워크 스폰 시 명부(캐싱 리스트)에 등록/해제 ────────────────
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

        protected override void Update()
        {
            base.Update();

            if (!isLocalPlayer) return;

            if (Keyboard.current != null)
            {
                // P키: 자유 시점 토글
                if (Keyboard.current.pKey.wasPressedThisFrame)
                {
                    ToggleFreezeMode();
                }

                // O키: 다른 러너(도망자) 관전
                if (Keyboard.current.oKey.wasPressedThisFrame)
                {
                    CycleSpectateRunner();
                }

                // I키: 술래 관전
                if (Keyboard.current.iKey.wasPressedThisFrame)
                {
                    CycleSpectateSeeker();
                }
            }
        }

        public void ToggleFreezeMode()
        {
            _isFrozen = !_isFrozen;

            if (_isFrozen)
            {
                // [고정 모드 ON] 가짜 타겟을 현재 내 위치로 불러온 뒤, 카메라가 따라가게 덮어씌움
                _freeCamTarget.transform.position = transform.position;
                if (cinemachineCamera != null)
                {
                    cinemachineCamera.Follow = _freeCamTarget.transform;
                }

                // 캐릭터는 즉시 정지
                if (GetComponent<Rigidbody2D>() != null)
                {
                    GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                }
            }
            else
            {
                // [고정 모드 OFF] 카메라가 다시 내 캐릭터를 따라가도록 원상 복구
                if (cinemachineCamera != null)
                {
                    cinemachineCamera.Follow = transform;
                }
            }
        }

        // ── 관전(Spectate) 기능 ──────────────────────────────────────
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
            // SeekerPlayer의 캐싱 리스트를 참조
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
            _isSpectating = true;

            if (GetComponent<Rigidbody2D>() != null)
                GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;

            cinemachineCamera.Follow = targetTransform;
        }
        // ──────────────────────────────────────────────────────────────────

        protected override void HandleMovement()
        {
            if (GetComponent<Rigidbody2D>() == null) return;

            Vector2 input = moveAction.ReadValue<Vector2>();

            if (_isFrozen)
            {
                GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;

                if (input.sqrMagnitude > 0.01f)
                {
                    // 다른 대상을 관전 중(`_isSpectating`)이었다면 WASD를 누르는 즉시 나 자신에게 복귀!
                    if (_isSpectating)
                    {
                        _isFrozen = false;
                        _isSpectating = false;

                        if (cinemachineCamera != null)
                        {
                            cinemachineCamera.Follow = transform; // 카메라 타겟을 다시 나에게로
                        }

                        // 복귀함과 동시에 딜레이 없이 바로 내 캐릭터가 걷도록 이동 함수 실행
                        base.HandleMovement();
                    }
                    else
                    {
                        // 관전이 아닌 P키를 눌러 들어온 순수 고정 모드일 때는 기존 기획대로 카메라 맵 구경 지원
                        if (_freeCamTarget != null)
                        {
                            _freeCamTarget.transform.position += (Vector3)input * freeCamSpeed * Time.fixedDeltaTime;
                        }
                    }
                }
            }
            else
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

        // ── 피격 및 지워짐 로직──────────────────────

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

            // 맞은 영역이 서서히 지워집니다. 아웃 판정은 페이드가 끝난 뒤
            // OnEraseFadeCompleted 콜백(HandleEraseFadeCompleted)에서 수행됩니다.
            paintCanvas.EraseArea(centerPixel, pixelRadius);
        }

        private void HandleEraseFadeCompleted()
        {
            if (isLocalPlayer)
            {
                CheckIfCaught();
            }
        }

        private void CheckIfCaught()
        {
            if (paintCanvas == null) return;

            // "1픽셀이라도 남아있으면 생존" 판정 
            bool isAlive = paintCanvas.HasAnyPaintedPixels();

            if (!isAlive)
            {
                CmdNotifyCaught();
            }
        }

        [Command]
        private void CmdNotifyCaught()
        {
            Debug.Log($"[Runner] {netId}번 플레이어의 색상이 모두 지워져 아웃되었습니다!");
            // TODO: 게임 매니저에 아웃 알림
        }
    }
}