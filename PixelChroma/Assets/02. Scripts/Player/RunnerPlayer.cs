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

        private bool _isFrozen = false;
        private GameObject _freeCamTarget;

        protected override void OnLocalPlayerStarted()
        {
            moveSpeed = runnerMoveSpeed;
            ShowHUDAsync().Forget();

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

            // 단축키 'F'를 누르면 고정 모드 토글! (원하시는 키로 변경 가능)
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                ToggleFreezeMode();
            }
        }

        public void ToggleFreezeMode()
        {
            _isFrozen = !_isFrozen;

            if (_isFrozen)
            {
                // [고정 모드 ON]
                // 가짜 타겟을 현재 내 위치로 불러온 뒤, 카메라가 가짜 타겟을 따라가게 덮어씌움
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
                // [고정 모드 OFF]
                // 카메라가 다시 내 캐릭터를 따라가도록 원상 복구
                if (cinemachineCamera != null)
                {
                    cinemachineCamera.Follow = transform;
                }
            }
        }

        protected override void HandleMovement()
        {
            if (GetComponent<Rigidbody2D>() == null) return;

            Vector2 input = moveAction.ReadValue<Vector2>();

            if (_isFrozen)
            {
                // 고정 모드일 때는 캐릭터 대신 "가짜 타겟(카메라)"만 이동시킴
                GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;

                if (_freeCamTarget != null)
                {
                    _freeCamTarget.transform.position += (Vector3)input * freeCamSpeed * Time.fixedDeltaTime;
                }
            }
            else
            {
                // 평소에는 정상적으로 내 캐릭터 이동
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

        // ── 피격 및 지워짐 로직 (동기화 100% 보장 버전) ──────────────────────

        
        //서버에서만 실행됩니다. 오차가 없는 서버의 절대 좌표를 기준으로 픽셀을 미리 계산합니다.
        [Server]
        public void ServerTakeEraseDamage(Vector2 hitWorldPos, float aoeRadius)
        {
            if (paintCanvas == null) return;

            // 1. 서버의 "단일 진실(Single Source of Truth)" 위치를 기준으로 픽셀 중앙점을 계산합니다.
            Vector2Int centerPixel = paintCanvas.WorldToPixel(hitWorldPos);

            // 2. 픽셀 반경도 서버에서 미리 계산합니다.
            float lossyScale = transform.lossyScale.x;
            float localAoeRadius = aoeRadius / lossyScale;
            int pixelRadius = Mathf.RoundToInt(localAoeRadius * paintCanvas.PixelsPerUnit);

            // 3. 변환이 완벽하게 끝난 '순수 픽셀 데이터(int)'만 클라이언트들에게 전송합니다!
            RpcTakeEraseDamageByPixel(centerPixel, pixelRadius);
        }

        //모든 클라이언트에서 실행됩니다. 서버가 지정해준 픽셀만 무조건 똑같이 지웁니다.
        [ClientRpc]
        public void RpcTakeEraseDamageByPixel(Vector2Int centerPixel, int pixelRadius)
        {
            if (paintCanvas == null) return;

            // 이미 서버가 계산해준 정확한 centerPixel과 pixelRadius를 그대로 사용하므로 
            // 클라이언트 간 픽셀 오차가 0%가 됩니다!
            for (int x = -pixelRadius; x <= pixelRadius; x++)
            {
                for (int y = -pixelRadius; y <= pixelRadius; y++)
                {
                    if (x * x + y * y <= pixelRadius * pixelRadius)
                    {
                        int px = centerPixel.x + x;
                        int py = centerPixel.y + y;

                        if (paintCanvas.IsInBounds(px, py))
                        {
                            paintCanvas.RestorePixelLocal(px, py);
                        }
                    }
                }
            }

            paintCanvas.ApplyChanges();

            if (isLocalPlayer)
            {
                CheckIfCaught();
            }
        }

        private void CheckIfCaught()
        {
            if (paintCanvas == null) return;

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