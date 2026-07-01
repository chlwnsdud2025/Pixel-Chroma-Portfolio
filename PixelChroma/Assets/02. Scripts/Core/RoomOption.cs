using System;

namespace PixelChroma
{
    public enum GameMode
    {
        Basic,
        Infection,
    }

    [Serializable]
    public class RoomOption
    {
        public const int DefaultMaxPlayers = 10;
        public const int DefaultSeekerCount = 1;
        public const float DefaultHideTime = 60f;
        public const float DefaultSeekTime = 300f;

        public const int MinPlayers = 2;
        public const int MaxPlayersLimit = 10;
        public const int MinSeekerCount = 1;
        public const int MaxSeekerCount = 5;
        public const float MinHideTime = 10f;
        public const float MaxHideTime = 120f;
        public const float MinSeekTime = 60f;
        public const float MaxSeekTime = 600f;

        public bool isPublic = true;
        public GameMode gameMode = GameMode.Basic;
        public int maxPlayers = DefaultMaxPlayers;
        public int seekerCount = DefaultSeekerCount;
        public float hideTime = DefaultHideTime;
        public float seekTime = DefaultSeekTime;
        public string mapName = "";


        public RoomOption Clone()
        {
            return new RoomOption
            {
                isPublic = isPublic,
                gameMode = gameMode,
                maxPlayers = maxPlayers,
                seekerCount = seekerCount,
                hideTime = hideTime,
                seekTime = seekTime,
                mapName = mapName,
            };
        }
    }
}
