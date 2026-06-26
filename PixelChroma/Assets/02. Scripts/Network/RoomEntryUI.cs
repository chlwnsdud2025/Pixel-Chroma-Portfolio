//using UnityEngine;
//using UnityEngine.UI;
//using TMPro;
//using Steamworks;

//public class RoomEntryUI : MonoBehaviour
//{
//    public TextMeshProUGUI roomNameText;
//    public TextMeshProUGUI statusText;
//    public TextMeshProUGUI playerCountText;
//    public Button joinButton;

//    private CSteamID myLobbyID;

//    // 생성될 때 데이터를 주입받는 함수
//    public void SetLobbyData(CSteamID lobbyID)
//    {
//        myLobbyID = lobbyID;

//        // 스팀에서 방 이름, 상태, 인원수 메타데이터 가져오기
//        string roomName = SteamMatchmaking.GetLobbyData(lobbyID, "RoomName");
//        string status = SteamMatchmaking.GetLobbyData(lobbyID, SteamLobbyManager.RoomStatusKey);
//        int currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
//        int maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyID);

//        roomNameText.text = roomName;
//        playerCountText.text = $"{currentPlayers} / {maxPlayers}";

//        // 상태가 InGame이면 글씨를 빨간색으로 바꾸고 버튼 비활성화
//        if (status == SteamLobbyManager.StatusInGame)
//        {
//            statusText.text = "In Game";
//            statusText.color = Color.red;
//            joinButton.interactable = false;
//        }
//        else
//        {
//            statusText.text = "Lobby";
//            statusText.color = Color.green;
//            joinButton.interactable = true; // 대기방일 때만 참가 가능
//        }
//    }

//    // UI 버튼에 할당할 함수
//    public void OnClickJoin()
//    {
//        SteamLobbyManager.instance.JoinLobby(myLobbyID);
//    }
//}