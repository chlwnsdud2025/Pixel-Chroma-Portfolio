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
        public const float DefaultEdgeHighlight = 0.1f;
        public const float DefaultEdgeShadow = 0.1f;

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
        // 외곽선 엠보스(하이라이트/쉐도우)는 0~1 비율. PlayerPaintCanvas.Recomposite()가 소비한다.
        public const float MinEmboss = 0f;
        public const float MaxEmboss = 1f;



        // 방 이름은 비워둘 수 있다. (비면 RoomListEntry가 "{hostName}의 방"으로 대체)
        public const int MaxRoomNameLength = 20;

        public bool isPublic = true;
        public GameMode gameMode = GameMode.Basic;
        public int maxPlayers = DefaultMaxPlayers;
        public int seekerCount = DefaultSeekerCount;
        public float hideTime = DefaultHideTime;
        public float seekTime = DefaultSeekTime;
        public float checkTime = DefaultCheckTime;
        public bool forcedExposeEnabled = true;
        public float forcedExposeInterval = DefaultForcedExposeInterval;
        public float edgeHighlight = DefaultEdgeHighlight;
        public float edgeShadow = DefaultEdgeShadow;

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
                forcedExposeEnabled = forcedExposeEnabled,
                forcedExposeInterval = forcedExposeInterval,
                edgeHighlight = edgeHighlight,
                edgeShadow = edgeShadow,
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
                forcedExposeEnabled = forcedExposeEnabled,
                forcedExposeInterval = forcedExposeInterval,
                edgeHighlight = edgeHighlight,
                edgeShadow = edgeShadow,
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
                forcedExposeEnabled = data.forcedExposeEnabled,
                forcedExposeInterval = data.forcedExposeInterval,
                edgeHighlight = data.edgeHighlight,
                edgeShadow = data.edgeShadow,
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
        public bool forcedExposeEnabled;
        public float forcedExposeInterval;
        public float edgeHighlight;
        public float edgeShadow;
        public string roomName;
        public string mapName;
        public string roomCode;
        public string hostName;
    }
}
