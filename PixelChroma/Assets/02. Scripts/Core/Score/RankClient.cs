using System;
using Mirror;

namespace PixelChroma
{
    /// <summary>
    /// 클라이언트 측 최종 순위 상태의 단일 원본입니다.
    /// 핸들러를 GameRoundManager 수명에 맞춰 한 번만 등록하고, LobbyPlayer는 C# 이벤트만 구독합니다.
    /// 상태를 보관하므로 메시지가 도착한 뒤에 스폰된 LobbyPlayer도 올바른 아이콘을 표시합니다.
    /// </summary>
    public static class RankClient
    {
        public static event Action OnRanksChanged;

        public static uint FirstNetId { get; private set; }
        public static uint SecondNetId { get; private set; }
        public static uint ThirdNetId { get; private set; }
        public static uint LastNetId { get; private set; }

        public static void RegisterHandlers()
        {
            NetworkClient.RegisterHandler<RankMessage>(OnRankMessage);
        }

        public static void UnregisterHandlers()
        {
            NetworkClient.UnregisterHandler<RankMessage>();
        }

        /// <summary>씬 전환 시 이전 판의 순위를 폐기합니다.</summary>
        public static void Reset()
        {
            FirstNetId = SecondNetId = ThirdNetId = LastNetId = 0;
            OnRanksChanged?.Invoke();
        }

        private static void OnRankMessage(RankMessage msg)
        {
            FirstNetId = msg.firstNetId;
            SecondNetId = msg.secondNetId;
            ThirdNetId = msg.thirdNetId;
            LastNetId = msg.lastNetId;

            OnRanksChanged?.Invoke();
        }
    }
}