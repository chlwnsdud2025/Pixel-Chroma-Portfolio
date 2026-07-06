using Mirror;

namespace PixelChroma
{
    public enum TimeState { Waiting, HideTime, SeekTime, GameEnd }

    public struct TimeStateMessage : NetworkMessage
    {
        public TimeState state;
        public float timer;
    }

    public struct CountdownTickMessage : NetworkMessage
    {
        public int remainingSeconds;
    }

    public struct CountdownEndMessage : NetworkMessage { }
}
