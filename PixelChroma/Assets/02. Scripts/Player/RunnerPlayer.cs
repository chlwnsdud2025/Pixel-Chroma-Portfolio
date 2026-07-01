using UnityEngine;
using Mirror;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

namespace PixelChroma
{
    public class RunnerPlayer : PlayerBase
    {
        [Header("Runner Settings")]
        [Tooltip("캐릭터의 이동 속도를 설정합니다.")]
        public float runnerMoveSpeed = 5f;

        protected override void OnLocalPlayerStarted()
        {
            moveSpeed = runnerMoveSpeed; // 도망자는 보통 술래보다 같거나 살짝 빠릅니다.
            ShowHUDAsync().Forget();
        }

        protected override void OnLocalPlayerStopped()
        {
            UnbindPaintHUD();
        }

        private async UniTaskVoid ShowHUDAsync()
        {
            var hud = await UIManager.Instance.ShowScene<UIInGameHUD>();
            BindPaintHUD(hud);
        }

        // ── 피격 및 지워짐 로직 ──────────────────────────────────────

        [ClientRpc]
        public void RpcTakeEraseDamage(Vector2 hitWorldPos, float aoeRadius)
        {
            if (paintCanvas == null) return;

            // 1. 물총이 터진 월드 좌표를 내 캔버스의 픽셀 좌표로 변환합니다.
            Vector2Int centerPixel = paintCanvas.WorldToPixel(hitWorldPos);

            // 💡 [핵심 수정] 캐릭터의 Transform Scale이 커졌을 경우를 대비한 오차 보정
            // (월드 공간의 폭발이 캐릭터 로컬 공간에서는 몇 유닛인지 비율을 정확히 맞춥니다)
            float lossyScale = transform.lossyScale.x;
            float localAoeRadius = aoeRadius / lossyScale;

            // 2. 캔버스의 실제 PPU를 가져와 정확한 픽셀 반경을 계산합니다.
            int pixelRadius = Mathf.RoundToInt(localAoeRadius * paintCanvas.PixelsPerUnit);

            // 3. 원형 범위 내의 픽셀을 "원본 색상"으로 조용히(Local) 되돌립니다.
            for (int x = -pixelRadius; x <= pixelRadius; x++)
            {
                for (int y = -pixelRadius; y <= pixelRadius; y++)
                {
                    // 수학적 원의 방정식 (x^2 + y^2 <= r^2) 검사
                    if (x * x + y * y <= pixelRadius * pixelRadius)
                    {
                        int px = centerPixel.x + x;
                        int py = centerPixel.y + y;

                        // 캔버스 범위를 벗어나지 않는지 확인 후 원본 복구
                        if (paintCanvas.IsInBounds(px, py))
                        {
                            // 칠하기 전 원본 픽셀로 복구!
                            paintCanvas.RestorePixelLocal(px, py);
                        }
                    }
                }
            }

            // 4. 복구된 픽셀 데이터를 한 번에 텍스처에 적용(Apply)하여 화면을 갱신합니다.
            paintCanvas.ApplyChanges();

            // 5. 내 캐릭터라면 캔버스를 검사해 몸이 다 지워졌는지(체력 0) 판단합니다.
            if (isLocalPlayer)
            {
                CheckIfCaught();
            }
        }

        private void CheckIfCaught()
        {
            if (paintCanvas == null) return;

            // 캔버스의 모든 픽셀을 검사해서 페인트가 1픽셀이라도 남아있는지 확인합니다.
            bool isAlive = paintCanvas.HasAnyPaintedPixels();

            // 남아있는 덧칠 픽셀이 없다면(완전히 씻겨 나갔다면), 내가 잡혔음을 서버에 공식 선언합니다!
            if (!isAlive)
            {
                CmdNotifyCaught();
            }
        }

        [Command]
        private void CmdNotifyCaught()
        {
            // 서버에서 이 Runner가 공식적으로 잡혔음을 선언
            Debug.Log($"[Runner] {netId}번 플레이어의 색상이 모두 지워져 아웃되었습니다!");

            // 추후 InGameManager에게 "Runner 1명 탈락!" 이벤트를 보내어
            // 관전자 모드로 전환시키거나, 모든 러너가 잡혔을 경우 게임 종료 판정을 넣으시면 됩니다.
        }
    }
}