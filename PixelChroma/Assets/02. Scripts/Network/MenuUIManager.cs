using UnityEngine;
using Mirror;

public class MenuUIManager : MonoBehaviour
{
    [Header("Offline Scene UI")]
    public SteamLobbyManager steamLobbyManager;

    // [변경] 방 목록 스크롤뷰를 포함하고 있는 부모 패널 UI
    public GameObject roomListPanel;

    private void Start()
    {
        // 게임이 처음 켜졌을 때는 방 목록 패널을 닫아둡니다.
        if (roomListPanel != null)
        {
            roomListPanel.SetActive(false);
        }
    }

    // '방 만들기' 버튼에 할당
    public void OnClickCreateRoom()
    {
        if (steamLobbyManager != null)
        {
            steamLobbyManager.HostLobby();
        }
    }

    // [신규] '방 찾기(Client)' 버튼을 눌렀을 때 스크롤뷰 패널을 껐다 켰다 하는 함수
    public void OnClickToggleRoomList()
    {
        if (roomListPanel != null)
        {
            // 현재 활성화 상태의 반대값(True -> False, False -> True)을 넣어서 토글합니다.
            bool isActive = !roomListPanel.activeSelf;
            roomListPanel.SetActive(isActive);

            // [UX 디테일] 패널이 켜지는 순간, 자동으로 스팀 서버에 방 목록을 한 번 요청해 줍니다.
            if (isActive && steamLobbyManager != null)
            {
                steamLobbyManager.GetLobbyList();
                Debug.Log("[MenuUI] 방 목록 패널이 열리며 자동으로 새로고침을 요청했습니다.");
            }
        }
    }
}