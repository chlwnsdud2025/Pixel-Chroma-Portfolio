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

        [Header("Catch Score")]
        [SerializeField] private int catchBaseScore = 100;           // 처치 기본 점수
        [SerializeField] private float catchScorePercent = 0.2f;     // 러너 점수의 20%

        [Header("Survival Bonus")]
        [SerializeField] private float survivalBonusPercent = 0.3f;  // 생존 보너스 30%

        private readonly Dictionary<int, int> _scores = new();
        private CancellationTokenSource _cts;
        private CancellationTokenSource _visionCts;
        public int CatchBaseScore => catchBaseScore;
        public float CatchScorePercent => catchScorePercent;
        public float SurvivalBonusPercent => survivalBonusPercent;

        public float VisionRange => visionRange;

        // ── 점수 조작 API (서버에서 호출) ──

        public void AddScore(int connectionId, int amount)
        {
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
        }

        private void OnDestroy()
        {
            StopBroadcast();
            StopVisionScoring();
        }

        // ── 주기적 전송 루프 ──

        private async UniTaskVoid BroadcastLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(broadcastInterval),
                    cancellationToken: token
                );
                SendScoreBoard();   // ← delay 후에 전송
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
                    pixelNicknamePng = NicknameRegistry.TryGetPixel(kv.Key, out var png) ? png : System.Array.Empty<byte>()
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

        private uint GetNetIdByConnectionId(int connectionId)
        {
            if (NetworkServer.connections.TryGetValue(connectionId, out var conn))
                return conn.identity != null ? conn.identity.netId : 0;
            return 0;
        }

        public int GetConnectionIdByNetId(uint netId)
        {
            foreach (var kv in NetworkServer.connections)
            {
                if (kv.Value.identity != null && kv.Value.identity.netId == netId)
                    return kv.Key;
            }
            return -1;
        }
    }
}