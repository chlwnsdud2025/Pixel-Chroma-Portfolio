using UnityEngine;

namespace PixelChroma
{
    // 대기실 장난감이 스폰될 지점. NetworkIdentity를 달지 않는다 —
    // 대기실 프리팹은 창작마당 맵에서 런타임 Instantiate 되므로 씬 오브젝트가 될 수 없다.
    public class ToySpawnPoint : MonoBehaviour
    {
        private bool _registered;

        private void Start() => Register();

        public void Register()
        {
            if (_registered) return;
            if (InGameManager.Instance == null) return;

            InGameManager.Instance.RegisterToySpawnPoint(transform);
            _registered = true;
        }
    }
}