using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using Mirror.FizzySteam;
using Cysharp.Threading.Tasks; // 추가됨

namespace PixelChroma
{
    public class ChromaNetworkManager : NetworkManager
    {
        [Header("Chroma Scene Settings")]
        [Scene] public string gameScenePath;

        [Header("Chroma Prefabs")]
        public GameObject lobbyPlayerPrefab;

        public override void Awake()
        {
            base.Awake();

#if UNITY_EDITOR
            // 에디터에서는 스팀 API 대신 기본 로컬호스트(kcp2k) 통신을 사용하도록 자동 전환
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
                ServerChangeScene(gameScenePath);
            }
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            GameObject playerInstance = null;

            if (currentScene == "CJY_Lobby") playerInstance = Instantiate(lobbyPlayerPrefab);
            else if (currentScene == "CJY_InGame") playerInstance = Instantiate(playerPrefab);

            if (playerInstance != null)
            {
                NetworkServer.AddPlayerForConnection(conn, playerInstance);
            }
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            if (sceneName == "CJY_InGame")
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

        // 클라이언트가 씬 로딩을 완료했을 때 UI 상태 변경 알림
        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged(); // Mirror의 기본 씬 처리 유지

            string currentScene = SceneManager.GetActiveScene().name;

            // 인게임 씬으로 넘어왔다면 GameManager를 통해 UIHUD 화면으로 전환!
            if (currentScene == "CJY_InGame" && GameManager.Instance != null)
            {
                GameManager.Instance.EnterInGame().Forget();
            }
        }
    }
}