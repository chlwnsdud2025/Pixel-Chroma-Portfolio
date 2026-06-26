using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

public class ChromaNetworkManager : NetworkManager
{
    [Header("Chroma Scene Settings")]
    [Scene] public string gameScenePath;

    [Header("Chroma Prefabs")]
    public GameObject lobbyPlayerPrefab;

    public void StartGame()
    {
        if (NetworkServer.active)
        {
            ServerChangeScene(gameScenePath);
        }
    }

    // Auto Create Player가 켜져 있으므로, 
    // 유저가 씬 로딩을 100% 마치고 'Ready'가 되었을 때 Mirror가 자동으로 이 함수를 호출합니다!
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        string currentScene = SceneManager.GetActiveScene().name;
        GameObject playerInstance = null;

        // 현재 씬에 맞는 올바른 프리팹을 선택
        if (currentScene == "CJY_Lobby")
        {
            playerInstance = Instantiate(lobbyPlayerPrefab);
        }
        else if (currentScene == "CJY_InGame")
        {
            playerInstance = Instantiate(playerPrefab);
        }

        if (playerInstance != null)
        {
            // 에러 없이 안전하게 플레이어 객체 지급
            NetworkServer.AddPlayerForConnection(conn, playerInstance);
            Debug.Log($"[ChromaNetworkManager] {currentScene} 씬에 {conn.connectionId}번 유저 스폰 완료.");
        }
    }

    // 서버가 씬 전환을 완료했을 때
    public override void OnServerSceneChanged(string sceneName)
    {
        if (sceneName == "CJY_InGame")
        {
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn.identity != null)
                {
                    // 1. 씬에 배치된 스폰 포인트 중 하나를 가져옵니다.
                    Transform startPos = GetStartPosition();

                    // 2. 스폰 포인트가 있으면 그 위치에, 없으면 기본 위치(0,0,0)에 생성합니다.
                    GameObject newGamePlayer = startPos != null
                        ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                        : Instantiate(playerPrefab);

                    // 3. 기존 로비 플레이어를 파괴하고 새 캐릭터로 교체(자동 스폰 포함)합니다.
                    NetworkServer.ReplacePlayerForConnection(conn, newGamePlayer);
                }
            }
        }
    }
}