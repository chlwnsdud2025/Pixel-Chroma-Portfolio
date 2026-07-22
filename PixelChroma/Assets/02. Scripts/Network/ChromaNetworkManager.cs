using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using Mirror.FizzySteam;
using Cysharp.Threading.Tasks;

namespace PixelChroma
{
    public class ChromaNetworkManager : NetworkManager
    {
        [Header("Chroma Scene Settings")]
        [Scene] public string inGameScene;

        [Header("Chroma Prefabs")]
        public GameObject lobbyPlayerPrefab;
        public GameObject runnerPrefab;
        public GameObject seekerPrefab;

        private readonly Dictionary<int, string> _playerNicknames = new();
        private readonly Dictionary<int, int> _playerLikes = new();
        private readonly HashSet<(int sender, int target)> _likeRecords = new();

        private MapRegistrySO MapRegistry => LobbyManager.Instance != null ? LobbyManager.Instance.MapRegistry : null;

        public override void Awake()
        {
            if (singleton != null && singleton != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            base.Awake();

            ConfigureTransport();
        }

        // Steam 사용 여부에 따라 활성 Transport를 런타임에 확정한다.
        // Inspector의 transport 필드가 잘못 지정돼 있어도 여기서 교정하므로,
        // "에디터에선 되는데 빌드에선 Steam 연결이 깨지는" 실수를 방지한다.
        private void ConfigureTransport()
        {
            var fizzy = GetComponent<FizzySteamworks>();
            var kcp = GetComponent<kcp2k.KcpTransport>();

            if (GameManager.SteamActive)
            {
                // 빌드: Steam 릴레이(Fizzy) 사용
                if (kcp != null) kcp.enabled = false;
                if (fizzy != null) { fizzy.enabled = true; Transport.active = fizzy; }
            }
            else
            {
                // 에디터/로컬: Steam 미사용 → KCP(localhost) 사용
                if (fizzy != null) fizzy.enabled = false;
                if (kcp != null) { kcp.enabled = true; Transport.active = kcp; }
            }
        }
        public override void OnStartServer()
        {
            base.OnStartServer();

            if (!GameManager.SteamActive && ChromaNetworkDiscovery.Instance != null)
                ChromaNetworkDiscovery.Instance.AdvertiseServer();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            _playerLikes.Clear();
            _likeRecords.Clear();

            if (!GameManager.SteamActive && ChromaNetworkDiscovery.Instance != null)
                ChromaNetworkDiscovery.Instance.StopDiscovery();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.Initialize();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.Cleanup();
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            _playerLikes[conn.connectionId] = 0;
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.NotifyClientConnected();
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.NotifyClientDisconnected();

            if (GameManager.Instance != null)
                GameManager.Instance.HandleDisconnect();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            GameObject playerInstance = null;

            if (currentScene == GetSceneName(onlineScene))
            {
                playerInstance = Instantiate(lobbyPlayerPrefab);
            }
            else if (IsInGameScene(currentScene))
            {
                var round = GameRoundManager.Instance;
                PlayerRole assignedRole = (round != null && round.TryGetRole(conn.connectionId, out var r))
                    ? r
                    : PlayerRole.Runner;

                GameObject prefab = assignedRole == PlayerRole.Seeker ? seekerPrefab : runnerPrefab;

                Vector3 spawnPos = Vector3.zero;
                Quaternion spawnRot = Quaternion.identity;

                if (InGameManager.Instance != null)
                {
                    spawnPos = (assignedRole == PlayerRole.Seeker)
                        ? InGameManager.Instance.GetWaitingPosition()
                        : InGameManager.Instance.GetSpawnPosition();
                }
                else
                {
                    Transform startPos = GetStartPosition();
                    if (startPos != null)
                    {
                        spawnPos = startPos.position;
                        spawnRot = startPos.rotation;
                    }
                }

                playerInstance = Instantiate(prefab, spawnPos, spawnRot);

                var inGamePlayerBase = playerInstance.GetComponent<PlayerBase>();
                if (inGamePlayerBase != null)
                    inGamePlayerBase.role = assignedRole;

                round?.RemovePlayer(conn.connectionId);
            }

            if (playerInstance != null)
            {
                var playerBase = playerInstance.GetComponent<PlayerBase>();
                if (playerBase != null)
                {
                    if (_playerNicknames.TryGetValue(conn.connectionId, out var savedName))
                        playerBase.playerName = savedName;
                }

                NetworkServer.AddPlayerForConnection(conn, playerInstance);
            }
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();

            string currentScene = SceneManager.GetActiveScene().name;

            if (IsInGameScene(currentScene) && GameManager.Instance != null)
                GameManager.Instance.EnterInGame().Forget();
            else if (currentScene == GetSceneName(onlineScene) && GameManager.Instance != null)
            {
                GameManager.Instance.EnterLobby().Forget();
            }
        }


        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);

            string scene = GetSceneName(sceneName);

            if (IsInGameScene(scene))
            {
                ClearLikeRecords();

                if (GameRoundManager.Instance != null)
                    GameRoundManager.Instance.BeginRound();
            }
            else if (scene == GetSceneName(onlineScene))
            {
                LobbyManager.Instance?.ChangeLobbyStatusToLobby();
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            _playerNicknames.Remove(conn.connectionId);

            _playerLikes.Remove(conn.connectionId);
            _likeRecords.RemoveWhere(r => r.sender == conn.connectionId || r.target == conn.connectionId);

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.RemovePlayer(conn.connectionId);
        }

        public void RegisterNickname(int connId, string nickname)
        {
            _playerNicknames[connId] = nickname;
            Debug.Log($"[NetworkManager] 명부에 닉네임 등록 완료 - ConnID: {connId}, Name: {nickname}");
        }

        [Server]
        public void ServerConvertRunnerToSeeker(RunnerPlayer runner)
        {
            if (runner == null) return;

            NetworkConnectionToClient conn = runner.connectionToClient;
            if (conn == null) return;

            Vector3 spawnPos = InGameManager.Instance != null
                ? InGameManager.Instance.GetSpawnPosition()
                : Vector3.zero;

            GameObject seekerInstance = Instantiate(seekerPrefab, spawnPos, Quaternion.identity);

            var seekerBase = seekerInstance.GetComponent<PlayerBase>();
            if (seekerBase != null)
            {
                seekerBase.role = PlayerRole.Seeker;
                seekerBase.playerName = runner.playerName;
            }

            NetworkServer.ReplacePlayerForConnection(conn, seekerInstance, ReplacePlayerOptions.Destroy);
        }

        public bool IsInGameScene(string sceneName)
        {
            if (sceneName == GetSceneName(inGameScene))
                return true;

            return MapRegistry != null && MapRegistry.ContainsScene(sceneName);
        }

        public string ResolveMapScene()
        {
            var roomOption = GameRoundManager.Instance?.CurrentRoomOption;
            if (MapRegistry != null && roomOption != null)
            {
                var mapData = MapRegistry.FindById(roomOption.mapName);
                if (mapData != null && !string.IsNullOrEmpty(mapData.sceneName))
                    return mapData.sceneName;
            }

            return inGameScene;
        }

        private string GetSceneName(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return string.Empty;
            return System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }


        [Server]
        public bool AddLike(int senderConnId, int targetConnId)
        {
            if (!_likeRecords.Add((senderConnId, targetConnId)))
                return false;

            _playerLikes.TryGetValue(targetConnId, out int current);
            _playerLikes[targetConnId] = current + 1;
            return true;
        }

        public int GetLikeCount(int connId)
        {
            return _playerLikes.TryGetValue(connId, out int count) ? count : 0;
        }

        [Server]
        public void ClearLikeRecords()
        {
            _likeRecords.Clear();
        }

    }
        
}
