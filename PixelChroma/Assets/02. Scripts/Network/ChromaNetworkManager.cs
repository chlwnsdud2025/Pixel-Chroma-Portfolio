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
        [Scene] public string inGameScene; // Mirror 빌트인 인게임 씬 경로 변수

        [Header("Chroma Prefabs")]
        public GameObject lobbyPlayerPrefab;

      
        private string GetSceneName(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return string.Empty;
            return System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }

        public override void Awake()
        {
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

            // 드래그해서 넣어둔 씬 파일의 실제 이름과 현재 씬 이름을 안전하게 비교합니다.
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
            // 드래그한 인게임 씬 이름과 비교
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

            // 드래그한 인게임 씬 이름과 비교
            if (currentScene == GetSceneName(inGameScene) && GameManager.Instance != null)
            {
                GameManager.Instance.EnterInGame().Forget();
            }
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            if (GameManager.Instance != null)
            {
                Debug.LogWarning("[Network] 서버와의 연결이 끊어졌습니다. 메인 메뉴로 복귀합니다.");

            
                SceneManager.LoadScene(offlineScene);

                GameManager.Instance.EnterMainMenu().Forget();
            }
        }
    }
}