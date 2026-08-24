using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using UnityEngine;

namespace PixelChroma
{
    public class ScoreManager : MonoSingleton<ScoreManager>
    {
        [Header("Settings")]
        [SerializeField] private float broadcastInterval = 5f;

        // 기존 필드에 추가
        [Header("Vision Score")]
        [SerializeField] private float visionCheckInterval = 1f;  // 시야 체크 주기
        [SerializeField] private int scorePerSeekerVisible = 10;   // Seeker 1명 보일 때마다 주는 점수
        [SerializeField] private float visionRange = 12f;          // 시야 범위 (카메라 대리)

        [Header("Survival Bonus")]
        [SerializeField] private float survivalBonusPercent = 0.3f;  // 생존 보너스 30%

        private readonly Dictionary<int, int> _scores = new();
        private readonly HashSet<int> _frozenPlayers = new();
        private CancellationTokenSource _cts;
        private CancellationTokenSource _visionCts;

        public float SurvivalBonusPercent => survivalBonusPercent;

        public float VisionRange => visionRange;
        public int TopScorerConnectionId => _topScorerConnId;

        // 1등 플레이어 connectionId (게임 종료 시 저장, 로비에서 사용)
        private int _topScorerConnId = -1;

        // ── 점수 조작 API (서버에서 호출) ──

        public void AddScore(int connectionId, int amount)
        {
            if (_frozenPlayers.Contains(connectionId)) return;   // ← 동결 체크

            if (!_scores.ContainsKey(connectionId))
                _scores[connectionId] = 0;

            _scores[connectionId] += amount;
        }
        public void SetScore(int connectionId, int value)
        {
            _scores[connectionId] = value;
        }

        public int GetScore(int connectionId)
        {
            return _scores.TryGetValue(connectionId, out var s) ? s : 0;
        }

        // ── 브로드캐스트 시작/종료 ──

        public void StartBroadcast()
        {
            StopBroadcast();
            _cts = new CancellationTokenSource();
            BroadcastLoop(_cts.Token).Forget();
        }

        public void StopBroadcast()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void ResetAll()
        {
            StopBroadcast();
            StopVisionScoring();
            _scores.Clear();
            _frozenPlayers.Clear();
        }

        private void OnDestroy()
        {
            StopBroadcast();
            StopVisionScoring();
        }

        // ── 주기적 전송 루프 ──

        private async UniTaskVoid BroadcastLoop(CancellationToken token)
        {
            // 모든 클라이언트 준비 대기 (최대 5초, 초과 시 그냥 진행)
            float timeout = 5f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (NetworkServer.connections.Values.All(conn => conn.isReady))
                    break;

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(0.5f),
                    cancellationToken: token
                );
                elapsed += 0.5f;
            }

            SendScoreBoard();

            // 이후 정상 주기
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(broadcastInterval),
                    cancellationToken: token
                );
                SendScoreBoard();
            }
        }

        private void SendScoreBoard()
        {
            if (!NetworkServer.active) return;


            var sorted = _scores
                .OrderByDescending(kv => kv.Value)
                .Select((kv, index) => new ScoreEntry
                {
                    rank = index + 1,
                    connectionId = kv.Key,
                    netId = GetNetIdByConnectionId(kv.Key),
                    playerName = NicknameRegistry.TryGet(kv.Key, out var n) ? n : $"Player {kv.Key}",
                    score = kv.Value,
                    pixelNicknamePng = NicknameRegistry.TryGetPixel(kv.Key, out var png) ? png : System.Array.Empty<byte>(),
                    isDead = _frozenPlayers.Contains(kv.Key)
                })
                .ToArray();


            NetworkServer.SendToAll(new ScoreBoardMessage
            {
                entries = sorted,
                refreshInterval = broadcastInterval
            });
        }

        // ── 시야 점수 시작/종료 ──

        public void StartVisionScoring()
        {
            StopVisionScoring();
            _visionCts = new CancellationTokenSource();
            VisionScoringLoop(_visionCts.Token).Forget();
        }

        public void StopVisionScoring()
        {
            _visionCts?.Cancel();
            _visionCts?.Dispose();
            _visionCts = null;
        }

        private async UniTaskVoid VisionScoringLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(visionCheckInterval),
                    cancellationToken: token
                );
                CheckDistanceAndAddScore();
            }
        }

        private void CheckDistanceAndAddScore()
        {
            if (!NetworkServer.active) return;
            if (InGameManager.Instance == null) return;
            if (GameRoundManager.Instance == null) return;
            if (GameRoundManager.Instance.CurrentTimeState != TimeState.SeekTime) return;

            var seekers = InGameManager.Instance.Seekers;
            var runners = InGameManager.Instance.Runners;

            foreach (var runner in runners)                // Runner 기준 루프
            {
                if (runner == null || runner.IsDead) continue;
                int connId = runner.connectionToClient != null
                    ? runner.connectionToClient.connectionId : -1;
                if (connId < 0) continue;

                foreach (var seeker in seekers)
                {
                    if (seeker == null) continue;

                    float dist = Vector2.Distance(
                        runner.transform.position,
                        seeker.transform.position
                    );
                    if (dist <= visionRange)
                        AddScore(connId, scorePerSeekerVisible);   // Runner에게 점수
                }
            }
        }


        //-----------------------헬퍼함수---------------------------------------

        private uint GetNetIdByConnectionId(int connectionId)
        {
            if (NetworkServer.connections.TryGetValue(connectionId, out var conn))
                return conn.identity != null ? conn.identity.netId : 0;
            return 0;
        }

        public void FreezeScore(int connectionId)
        {
            _frozenPlayers.Add(connectionId);
            SendScoreBoard();
        }

        public void SaveTopScorer()
        {
            if (_scores.Count == 0)
            {
                _topScorerConnId = -1;
                return;
            }

            _topScorerConnId = _scores.OrderByDescending(kv => kv.Value).First().Key;
        }

        public void ClearTopScorer()
        {
            _topScorerConnId = -1;
        }
    }
}