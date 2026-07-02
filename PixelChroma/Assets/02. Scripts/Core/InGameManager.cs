using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma
{
    // 💡 다시 평화로운 MonoSingleton으로 돌아왔습니다!
    public class InGameManager : MonoSingleton<InGameManager>
    {
        private Transform _enemySpawnPoint;
        private readonly List<Transform> _playerSpawnPoints = new();
        private int _currentPlayerSpawnIndex = 0;

        public void RegisterSpawnPoint(PlayerRole role, Transform spawnTransform)
        {
            if (role == PlayerRole.Seeker)
            {
                _enemySpawnPoint = spawnTransform;
                Debug.Log($"[InGameManager] 술래(Enemy) 스폰 포인트 등록 완료: {spawnTransform.name}");
            }
            else if (role == PlayerRole.Runner)
            {
                if (!_playerSpawnPoints.Contains(spawnTransform))
                {
                    _playerSpawnPoints.Add(spawnTransform);
                    Debug.Log($"[InGameManager] 도망자(Player) 스폰 포인트 추가 완료 (총 {_playerSpawnPoints.Count}개)");
                }
            }
        }

        public void ClearSpawnPoints()
        {
            _enemySpawnPoint = null;
            _playerSpawnPoints.Clear();
            _currentPlayerSpawnIndex = 0;
            Debug.Log("[InGameManager] 이전 판의 스폰 포인트 데이터가 초기화되었습니다.");
        }

        public Vector3 GetSpawnPosition(PlayerRole role)
        {
            if (role == PlayerRole.Seeker)
            {
                if (_enemySpawnPoint != null)
                {
                    Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0);
                    return _enemySpawnPoint.position + offset;
                }
                Debug.LogWarning("[InGameManager] 등록된 Enemy 스폰 포인트가 없습니다! Vector3.zero를 반환합니다.");
                return Vector3.zero;
            }

            if (_playerSpawnPoints == null || _playerSpawnPoints.Count == 0)
            {
                Debug.LogWarning("[InGameManager] 등록된 Player 스폰 포인트가 없습니다! Vector3.zero를 반환합니다.");
                return Vector3.zero;
            }

            Transform selectedPoint = _playerSpawnPoints[_currentPlayerSpawnIndex];
            _currentPlayerSpawnIndex = (_currentPlayerSpawnIndex + 1) % _playerSpawnPoints.Count;

            Vector3 playerOffset = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f), 0);
            return selectedPoint.position + playerOffset;
        }
    }
}