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

        private string GetSceneName(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return string.Empty;
            return System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }

        public override void Awake()
        {
            // 💡 [방어벽 추가] base.Awake()보다 앞에 둡니다.
            // 메인 메뉴로 돌아올 때 새로 생성된 유령 매니저가 정적 변수인 'Transport.active'를
            // 망가뜨리는 것을 원천 차단하기 위해, 오브젝트를 즉시 끄고 파괴 세팅을 내립니다.
            if (singleton != null && singleton != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            base.Awake();

#if UNITY_EDITOR
            var fizzy = GetComponent<FizzySteamworks>();
            var kcp = GetComponent<kcp2k.KcpTransport>();
            if (fizzy != null) fizzy.enabled = false;
            if (kcp != null) { kcp.enabled = true; Transport.active = kcp; }
#endif
        }

        public void StartGame()
        {
            if (NetworkServer.active)
            {
                ServerChangeScene(inGameScene);
            }
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            GameObject playerInstance = null;

            if (currentScene == GetSceneName(onlineScene))
                playerInstance = Instantiate(lobbyPlayerPrefab);
            else if (currentScene == GetSceneName(inGameScene))
                playerInstance = Instantiate(playerPrefab);

            if (playerInstance != null)
            {
                NetworkServer.AddPlayerForConnection(conn, playerInstance);
            }
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            if (sceneName == GetSceneName(inGameScene))
            {
                foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
                {
                    if (conn.identity != null)
                    {
                        Transform startPos = GetStartPosition();
                        GameObject newGamePlayer = startPos != null
                            ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                            : Instantiate(playerPrefab);

                        NetworkServer.ReplacePlayerForConnection(conn, newGamePlayer);
                    }
                }
            }
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();

            string currentScene = SceneManager.GetActiveScene().name;

            if (currentScene == GetSceneName(inGameScene) && GameManager.Instance != null)
            {
                GameManager.Instance.EnterInGame().Forget();
            }
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            if (GameManager.Instance == null) return;

            Debug.LogWarning("[Network] 서버와의 연결이 끊어졌습니다. 메인 메뉴로 복귀합니다.");

            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyId.IsValid())
                LobbyManager.Instance.LeaveLobby();

            GameManager.Instance.EnterMainMenu().Forget();
        }
    }
}