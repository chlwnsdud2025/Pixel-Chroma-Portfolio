using Mirror;

namespace PixelChroma
{
    public enum TimeState { Waiting, Countdown, HideTime, SeekTime, CheckTime, GameEnd }

    public struct TimeStateMessage : NetworkMessage
    {
        public TimeState state;
        public float timer;
    }

    public struct WinnerDecidedMessage : NetworkMessage
    {
        public PlayerRole winner;
    }

    public struct RoomOptionSyncMessage : NetworkMessage
    {
        public RoomOptionData option;
    }
}
