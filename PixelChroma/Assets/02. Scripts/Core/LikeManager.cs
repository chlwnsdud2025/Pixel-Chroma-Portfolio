using System.Collections.Generic;

namespace PixelChroma
{
    // 서버측 "좋아요" 상태를 소유하는 서비스. 커넥션별 좋아요 수와 (보낸이→받는이) 중복 방지 장부를 관리한다.
    public static class LikeManager
    {
        private static readonly Dictionary<int, int> _likes = new();
        private static readonly HashSet<(int sender, int target)> _records = new();

        // 커넥션 접속 시 좋아요 카운트 슬롯을 0으로 초기화.
        public static void Register(int connId) => _likes[connId] = 0;

        // 커넥션 종료 시 해당 커넥션의 카운트와, 그 커넥션이 얽힌 장부 기록을 정리.
        public static void Unregister(int connId)
        {
            _likes.Remove(connId);
            _records.RemoveWhere(r => r.sender == connId || r.target == connId);
        }

        public static int GetLikeCount(int connId)
            => _likes.TryGetValue(connId, out int count) ? count : 0;

        // 같은 (보낸이→받는이) 조합은 1회만 허용한다. 실제 카운트가 올라가면 true.
        public static bool AddLike(int senderConnId, int targetConnId)
        {
            if (!_records.Add((senderConnId, targetConnId)))
                return false;

            _likes.TryGetValue(targetConnId, out int current);
            _likes[targetConnId] = current + 1;
            return true;
        }

        // 라운드/로비 리셋 시 중복 장부만 비운다. (누적 카운트는 유지)
        public static void ClearRecords() => _records.Clear();

        // 서버 종료 시 전체 초기화.
        public static void Clear()
        {
            _likes.Clear();
            _records.Clear();
        }
    }
}
