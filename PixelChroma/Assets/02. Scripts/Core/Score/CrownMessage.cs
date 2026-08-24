using Mirror;

namespace PixelChroma
{
    public struct CrownMessage : NetworkMessage
    {
        public uint topScorerNetId;   // 1등 플레이어의 로비 netId (0이면 없음)
    }
}