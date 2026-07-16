using System;
using Mirror;

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
        public const int DefaultMaxPlayers = 12;
        public const int DefaultSeekerCount = 1;
        public const float DefaultHideTime = 60f;
        public const float DefaultSeekTime = 300f;
        public const float DefaultCheckTime = 30f;
        public const float DefaultForcedExposeInterval = 40f;

        public const int MinPlayers = 2;
        public const int MaxPlayersLimit = 12;
        public const int MinSeekerCount = 1;
        public const int MaxSeekerCount = 5;
        public const float MinHideTime = 10f;
        public const float MaxHideTime = 120f;
        public const float MinSeekTime = 60f;
        public const float MaxSeekTime = 600f;
        public const float MinCheckTime = 10f; 
        public const float MaxCheckTime = 100f;
        public const float MinForcedExposeInterval = 0f;  
        public const float MaxForcedExposeInterval = 120f; 

        public bool isPublic = true;
        public GameMode gameMode = GameMode.Basic;
        public int maxPlayers = DefaultMaxPlayers;
        public int seekerCount = DefaultSeekerCount;
        public float hideTime = DefaultHideTime;
        public float seekTime = DefaultSeekTime;
        public float checkTime = DefaultCheckTime;
        public float forcedExposeInterval = DefaultForcedExposeInterval;

        public string roomName = "";
        public string mapName = "";
        public string roomCode = "";
        public string hostName = "";

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
                checkTime = checkTime,
                forcedExposeInterval = forcedExposeInterval,
                roomName = roomName,
                mapName = mapName,
                roomCode = roomCode,
                hostName = hostName,
            };
        }

        public RoomOptionData ToData()
        {
            return new RoomOptionData
            {
                isPublic = isPublic,
                gameMode = (int)gameMode,
                maxPlayers = maxPlayers,
                seekerCount = seekerCount,
                hideTime = hideTime,
                seekTime = seekTime,
                checkTime = checkTime,
                forcedExposeInterval = forcedExposeInterval,
                roomName = roomName,
                mapName = mapName,
                roomCode = roomCode,
                hostName = hostName,
            };
        }

        public static RoomOption FromData(RoomOptionData data)
        {
            return new RoomOption
            {
                isPublic = data.isPublic,
                gameMode = (GameMode)data.gameMode,
                maxPlayers = data.maxPlayers,
                seekerCount = data.seekerCount,
                hideTime = data.hideTime,
                seekTime = data.seekTime,
                checkTime = data.checkTime,
                forcedExposeInterval = data.forcedExposeInterval,
                roomName = data.roomName,
                mapName = data.mapName,
                roomCode = data.roomCode,
                hostName = data.hostName,
            };
        }
    }

    public struct RoomOptionData
    {
        public bool isPublic;
        public int gameMode;
        public int maxPlayers;
        public int seekerCount;
        public float hideTime;
        public float seekTime;
        public float checkTime;
        public float forcedExposeInterval;
        public string roomName;
        public string mapName;
        public string roomCode;
        public string hostName;
    }
}
