//using UnityEngine;
//using Steamworks;
//using System.Collections.Generic;

//public class RoomListUIManager : MonoBehaviour
//{
//    public GameObject roomEntryPrefab; // 방금 만든 UI 프리팹
//    public Transform contentTransform; // Scroll View의 Content 객체

//    private void Start()
//    {
//        // 스팀 로비 매니저가 방을 찾으면 자동으로 화면을 그리도록 구독
//        SteamLobbyManager.instance.OnLobbyListUpdated += UpdateLobbyUI;
//    }

//    private void OnDestroy()
//    {
//        if (SteamLobbyManager.instance != null)
//            SteamLobbyManager.instance.OnLobbyListUpdated -= UpdateLobbyUI;
//    }

//    // 새로고침 버튼에 연결
//    public void OnClickRefresh()
//    {
//        SteamLobbyManager.instance.GetLobbyList();
//    }

//    private void UpdateLobbyUI(List<CSteamID> lobbyIDs)
//    {
//        // 1. 기존에 그려진 옛날 방 목록들 싹 지우기
//        foreach (Transform child in contentTransform)
//        {
//            Destroy(child.gameObject);
//        }

//        // 2. 새로 찾은 방 목록대로 프리팹 생성
//        foreach (CSteamID lobbyID in lobbyIDs)
//        {
//            GameObject entry = Instantiate(roomEntryPrefab, contentTransform);
//            RoomEntryUI entryUI = entry.GetComponent<RoomEntryUI>();

//            if (entryUI != null)
//            {
//                entryUI.SetLobbyData(lobbyID);
//            }
//        }
//    }
//}