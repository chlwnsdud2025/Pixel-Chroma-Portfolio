using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace PixelChroma
{
    public static class ScoreBoardClient
    {
        // ── 이벤트 ──
        public static event Action<ScoreEntry[]> OnSnapshot;         // 스코어보드 갱신
        public static event Action<int> OnPlayerDead;                // connectionId
        public static event Action<int, Color32> OnPixelRevealed;    // connectionId, color
        public static event Action<int> OnCountdownChanged;          // 남은 초

        // ── 상태 ──
        private static readonly HashSet<int> _deadConnIds = new();

        // 그림 닉네임은 스냅샷에 싣지 않고, 이미 SyncVar로 받아 둔 플레이어 데이터를 netId로 찾아 쓴다.
        // netId는 감염 전환 시 바뀌므로 찾은 PNG를 connectionId 기준으로 보관한다.
        private static readonly Dictionary<int, byte[]> _pixelByConnId = new();

        public static ScoreEntry[] Entries { get; private set; } = Array.Empty<ScoreEntry>();
        public static int RefreshCountdown { get; private set; }
        public static int MyConnectionId { get; private set; } = -1;

        public static bool IsDead(int connectionId) => _deadConnIds.Contains(connectionId);

        public static bool TryGetPixelNickname(int connectionId, out byte[] pngData)
            => _pixelByConnId.TryGetValue(connectionId, out pngData);

        // ── 등록 / 해제 ──

        public static void RegisterHandlers()
        {
            NetworkClient.RegisterHandler<ScoreBoardMessage>(OnScoreBoardMessage);
            NetworkClient.RegisterHandler<PlayerDeadMessage>(OnPlayerDeadMessage);
            NetworkClient.RegisterHandler<RevealedPixelMessage>(OnRevealedPixelMessage);

            GameRoundManager.OnTimerTick -= HandleTimerTick;   //중복 구독 방지
            GameRoundManager.OnTimerTick += HandleTimerTick;
        }

        public static void UnregisterHandlers()
        {
            NetworkClient.UnregisterHandler<ScoreBoardMessage>();
            NetworkClient.UnregisterHandler<PlayerDeadMessage>();
            NetworkClient.UnregisterHandler<RevealedPixelMessage>();

            GameRoundManager.OnTimerTick -= HandleTimerTick;
        }

        //씬 전환(라운드 시작 / 로비 복귀) 시 이전 판 상태를 폐기합니다.
        public static void Reset()
        {
            Entries = Array.Empty<ScoreEntry>();
            RefreshCountdown = 0;
            MyConnectionId = -1;
            _deadConnIds.Clear();
            _pixelByConnId.Clear();

            OnSnapshot?.Invoke(Entries);
            OnCountdownChanged?.Invoke(0);
        }

        // ── 수신 ──

        private static void OnScoreBoardMessage(ScoreBoardMessage msg)
        {
            Entries = msg.entries ?? Array.Empty<ScoreEntry>();
            RefreshCountdown = Mathf.RoundToInt(msg.refreshInterval);

            ResolveMyConnectionId();
            CachePixelNicknames();

            // 서버 스냅샷의 사망 상태를 누적 집합에 합친다.
            for (int i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].isDead)
                    _deadConnIds.Add(Entries[i].connectionId);
            }

            OnSnapshot?.Invoke(Entries);
            OnCountdownChanged?.Invoke(RefreshCountdown);
        }

        private static void OnPlayerDeadMessage(PlayerDeadMessage msg)
        {
            _deadConnIds.Add(msg.connectionId);   // 누적 → 나중에 켜지는 UI도 복원 가능
            OnPlayerDead?.Invoke(msg.connectionId);
        }

        private static void OnRevealedPixelMessage(RevealedPixelMessage msg)
        {
            OnPixelRevealed?.Invoke(msg.connectionId, msg.color);
        }

        // 표시 후 감소 (10 → 1 순서 유지)
        private static void HandleTimerTick(int remainingSeconds)
        {
            OnCountdownChanged?.Invoke(RefreshCountdown);

            RefreshCountdown--;
            if (RefreshCountdown < 0) RefreshCountdown = 0;
        }

        // 서버는 클라에게 connectionId를 알려주지 않는다(NetworkClient.connection.connectionId는 항상 0).
        // 엔트리가 netId와 connectionId를 함께 담고 있으므로 여기서 역산한다.
        private static void ResolveMyConnectionId()
        {
            if (NetworkClient.localPlayer == null) return;
            uint myNetId = NetworkClient.localPlayer.netId;

            for (int i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].netId == myNetId)
                {
                    MyConnectionId = Entries[i].connectionId;
                    return;
                }
            }
            // 목록에 없으면(= 내가 Seeker) 마지막 값을 유지한다. connectionId는 불변이라 안전.
        }

        // 스냅샷의 netId로 현재 씬의 플레이어를 찾아 그림 닉네임을 connectionId별로 보관한다.
        // 못 찾으면(오브젝트 교체 직후 등) 이전에 보관한 값을 그대로 유지한다.
        private static void CachePixelNicknames()
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                var player = PixelNicknameHelper.FindPlayer(Entries[i].netId);
                byte[] png = PixelNicknameHelper.GetPixelData(player);
                if (png != null)
                    _pixelByConnId[Entries[i].connectionId] = png;
            }
        }
    }
}