using Mirror;

namespace PixelChroma
{
    public struct CountdownStartMessage : NetworkMessage
    {
        public float duration;
    }

    public struct CountdownTickMessage : NetworkMessage
    {
        public int remainingSeconds;
    }

    public struct CountdownEndMessage : NetworkMessage { }
}
