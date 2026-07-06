using Mirror;

namespace PixelChroma
{
    public enum TimeState { Waiting, HideTime, SeekTime, CheckTime, GameEnd }

    public struct TimeStateMessage : NetworkMessage
    {
        public TimeState state;
        public float timer;

        public PlayerRole winner;
    }

    public struct CountdownTickMessage : NetworkMessage
    {
        public int remainingSeconds;
    }

    public struct CountdownEndMessage : NetworkMessage { }
}
