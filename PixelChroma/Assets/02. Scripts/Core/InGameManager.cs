using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma
{
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
                Debug.Log($"[InGameManager] 술래(Enemy) 본진 스폰 포인트 등록 완료: {spawnTransform.name}");
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

        public Vector3 GetSpawnPosition(PlayerRole role, bool isWaiting = false)
        {
            if (role == PlayerRole.Seeker)
            {
                // 1. 대기실 스폰을 요청한 경우 (항상 0,0,0 좌표)
                if (isWaiting)
                {
                    // 술래가 여러 명일 때 겹치지 않도록 약간의 오프셋만 더해줍니다.
                    Vector3 waitingOffset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
                    return Vector3.zero + waitingOffset;
                }

                // 2. 본 게임 시작으로 본진 스폰을 요청한 경우
                if (_enemySpawnPoint != null)
                {
                    Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0);
                    return _enemySpawnPoint.position + offset;
                }

                Debug.LogWarning("[InGameManager] 등록된 Enemy 스폰 포인트가 없습니다! Vector3.zero를 반환합니다.");
                return Vector3.zero;
            }

            // 도망자(Runner) 스폰 로직
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