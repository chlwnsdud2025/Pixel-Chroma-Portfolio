using UnityEngine;

namespace PixelChroma
{
    public class MapSpawnPoint : MonoBehaviour
    {
        private void Start()
        {
            if (InGameManager.Instance != null)
            {
                InGameManager.Instance.RegisterSpawnPoint(this.transform);
            }
            else
            {
                Debug.LogError("[MapSpawnPoint]Managers 하위의 InGameManager를 찾을 수 없습니다!");
            }
        }
    }
}