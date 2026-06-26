using UnityEngine;
using Mirror;

public class MenuUIManager : MonoBehaviour
{
    [Header("Offline Scene UI")]
    public SteamLobbyManager steamLobbyManager;

    [Header("Lobby Scene UI")]
    public GameObject startGameButton;

    private void Start()
    {
        // 로비 씬에서, 방장(서버)이 아니면 게임 시작 버튼 숨기기
        if (startGameButton != null)
        {
            startGameButton.SetActive(NetworkServer.active);
        }
    }

    // Offline 씬의 '방 만들기' 버튼에 할당
    public void OnClickCreateRoom()
    {
        if (steamLobbyManager != null)
        {
            steamLobbyManager.HostLobby();
        }
    }

    // Lobby 씬의 '게임 시작' 버튼에 할당
    public void OnClickStartGame()
    {
        if (NetworkServer.active)
        {
            ChromaNetworkManager manager = NetworkManager.singleton as ChromaNetworkManager;
            manager?.StartGame();
        }
    }
}