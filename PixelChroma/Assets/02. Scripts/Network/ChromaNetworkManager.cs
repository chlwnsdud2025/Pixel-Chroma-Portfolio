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

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.Initialize();

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.NotifyClientConnected();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.Cleanup();

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.NotifyClientDisconnected();
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
                    bool isWaiting = (assignedRole == PlayerRole.Seeker);
                    spawnPos = InGameManager.Instance.GetSpawnPosition(assignedRole, isWaiting);
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
        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            // 매니저들 초기화
            if (LobbyManager.Instance != null) LobbyManager.Instance.NotifyClientDisconnected();
            if (InGameManager.Instance != null) InGameManager.Instance.ClearSpawnPoints();
            if (GameRoundManager.Instance != null) GameRoundManager.Instance.Cleanup();

            if (GameManager.Instance == null) return;

            Debug.LogWarning("[Network] 서버와의 연결이 끊어졌습니다. 메인 메뉴로 복귀합니다.");

            // 스팀 로비 탈퇴 및 메인 메뉴로 멱살 잡고 끌고 가기
            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyId.IsValid())
                LobbyManager.Instance.LeaveLobby();

            GameManager.Instance.EnterMainMenu().Forget();
        }
    

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);

            if (IsInGameScene(GetSceneName(sceneName)) && GameRoundManager.Instance != null)
                GameRoundManager.Instance.BeginRound();
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            _playerNicknames.Remove(conn.connectionId);

            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.RemovePlayer(conn.connectionId);
        }

        public void RegisterNickname(int connId, string nickname)
        {
            _playerNicknames[connId] = nickname;
            Debug.Log($"[NetworkManager] 명부에 닉네임 등록 완료 - ConnID: {connId}, Name: {nickname}");
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

    }
        
}
