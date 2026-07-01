using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma
{
    public class InGameManager : MonoSingleton<InGameManager>
    {
        // 맵이 로드되면서 동적으로 수집할 스폰 포인트 보관소
        private Transform _enemySpawnPoint;
        private readonly List<Transform> _playerSpawnPoints = new();

        private int _currentPlayerSpawnIndex = 0;

        /// <summary>
        /// 씬에 생성된 맵 내부의 스폰 포인트들이 InGameManager에게 자신을 등록할 때 호출합니다.
        /// </summary>
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

        /// <summary>
        /// 게임 한 판이 끝나고 로비나 메인메뉴로 나갈 때, 이전 맵의 찌꺼기 참조를 청소합니다.
        /// </summary>
        public void ClearSpawnPoints()
        {
            _enemySpawnPoint = null;
            _playerSpawnPoints.Clear();
            _currentPlayerSpawnIndex = 0;
            Debug.Log("[InGameManager] 이전 판의 스폰 포인트 데이터가 초기화되었습니다.");
        }

        /// <summary>
        /// 네트워크 매니저가 플레이어 배치용 좌표를 요구할 때 호출합니다.
        /// </summary>
        public Vector3 GetSpawnPosition(PlayerRole role)
        {
            // 1. 술래(Enemy) 스폰 연산
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

            // 2. 도망자(Player) 골고루 무작위 분산 스폰 연산 (라운드 로빈)
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