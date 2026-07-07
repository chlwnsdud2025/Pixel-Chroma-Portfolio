using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelChroma
{
    public class InGameManager : MonoSingleton<InGameManager>
    {
        private Transform _spawnPoint;

        private readonly List<RunnerPlayer> _runners = new();
        private readonly List<SeekerPlayer> _seekers = new();

        public IReadOnlyList<RunnerPlayer> Runners => _runners;
        public IReadOnlyList<SeekerPlayer> Seekers => _seekers;

        public void RegisterPlayer(PlayerBase player)
        {
            switch (player)
            {
                case RunnerPlayer runner when !_runners.Contains(runner):
                    _runners.Add(runner);
                    break;
                case SeekerPlayer seeker when !_seekers.Contains(seeker):
                    _seekers.Add(seeker);
                    break;
            }
        }

        public void UnregisterPlayer(PlayerBase player)
        {
            switch (player)
            {
                case RunnerPlayer runner:
                    _runners.Remove(runner);
                    break;
                case SeekerPlayer seeker:
                    _seekers.Remove(seeker);
                    break;
            }
        }

        public int GetAliveRunnerCount()
        {
            return _runners.Count(r => r != null && !r.isDead);
        }

        public bool HasAnyRunner()
        {
            return _runners.Count > 0;
        }

        public void RegisterSpawnPoint(Transform spawnTransform)
        {
            _spawnPoint = spawnTransform;
            Debug.Log($"[InGameManager] 스폰 포인트 등록 완료: {spawnTransform.name}");
        }

        public void ResetState()
        {
            _spawnPoint = null;
            _runners.Clear();
            _seekers.Clear();
            Debug.Log("[InGameManager] 이전 판의 스폰 포인트 데이터가 초기화되었습니다.");
        }

        public Vector3 GetSpawnPosition()
        {
            if (_spawnPoint != null)
            {
                Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0);
                return _spawnPoint.position + offset;
            }

            Debug.LogWarning("[InGameManager] 등록된 스폰 포인트가 없습니다! Vector3.zero를 반환합니다.");
            return Vector3.zero;
        }

        public Vector3 GetWaitingPosition()
        {
            Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
            return Vector3.zero + offset;
        }
    }
}
