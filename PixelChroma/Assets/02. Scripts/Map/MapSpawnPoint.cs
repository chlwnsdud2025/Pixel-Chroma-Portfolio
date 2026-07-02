using UnityEngine;

namespace PixelChroma
{
    public class MapSpawnPoint : MonoBehaviour
    {
        [Header("이 포인트의 역할 분기 설정")]
        [SerializeField] private PlayerRole spawnRole; // 인스펙터에서 Seeker 또는 Runner 선택

        private void Start()
        {
            // 맵이 활성화/로드되자마자 전역 매니저에게 자기 변폭(위치)을 신고합니다.
            if (InGameManager.Instance != null)
            {
                InGameManager.Instance.RegisterSpawnPoint(spawnRole, this.transform);
            }
            else
            {
                Debug.LogError("[MapSpawnPoint]Managers 하위의 InGameManager를 찾을 수 없습니다!");
            }
        }
    }
}