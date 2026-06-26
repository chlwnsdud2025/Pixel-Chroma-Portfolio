using UnityEngine;
using Mirror;
using Steamworks;
using System;
using System.Collections.Generic;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager instance;

    // 방 목록 UI로 데이터를 넘겨줄 이벤트 (콜백)
    public event Action<List<CSteamID>> OnLobbyListUpdated;

    protected Callback<LobbyMatchList_t> lobbyListRequested;
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> joinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    private const string HostAddressKey = "HostAddress";
    public const string RoomStatusKey = "RoomStatus"; // 상태 키
    public const string StatusLobby = "Lobby";        // 대기방 상태
    public const string StatusInGame = "InGame";      // 게임 중 상태

    // 현재 내가 속한 로비 ID를 저장 (호스트가 상태를 바꿀 때 필요)
    public CSteamID currentLobbyID;
    private ChromaNetworkManager networkManager;

    private void Awake() { instance = this; }

    private void Start()
    {
        if (!SteamManager.Initialized) return;
        networkManager = GetComponent<ChromaNetworkManager>();

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyListRequested = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
    }

    public void HostLobby()
    {
        // 리스트에 노출되도록 Public 로비로 생성합니다.
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 10);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK) return;

        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        SteamMatchmaking.SetLobbyData(currentLobbyID, HostAddressKey, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(currentLobbyID, "RoomName", SteamFriends.GetPersonaName());
        SteamMatchmaking.SetLobbyData(currentLobbyID, RoomStatusKey, StatusLobby);

        // [추가] 글로벌 480 바다에서 우리 방을 식별하기 위한 고유 키워드 셋팅
        SteamMatchmaking.SetLobbyData(currentLobbyID, "GameID", "PixelChroma_Dev");

        networkManager.StartHost();
    }

    // [신규] 방장이 게임 시작 버튼을 누를 때 이 함수를 호출하여 상태를 InGame으로 바꿉니다.
    public void ChangeLobbyStatusToInGame()
    {
        if (NetworkServer.active)
        {
            SteamMatchmaking.SetLobbyData(currentLobbyID, RoomStatusKey, StatusInGame);
        }
    }

    // UI에서 '방 새로고침' 버튼을 눌렀을 때 호출
    public void GetLobbyList()
    {
        Debug.Log("[SteamLobbyManager] 스팀 서버에 픽셀 크로마 방 목록 요청...");

        // [추가] "GameID" 값이 "PixelChroma_Dev"와 완벽히 일치(Equal)하는 방만 검색합니다!
        SteamMatchmaking.AddRequestLobbyListStringFilter("GameID", "PixelChroma_Dev", ELobbyComparison.k_ELobbyComparisonEqual);

        SteamMatchmaking.RequestLobbyList();
    }

    private void OnLobbyMatchList(LobbyMatchList_t callback)
    {
        List<CSteamID> lobbyList = new List<CSteamID>();
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            lobbyList.Add(SteamMatchmaking.GetLobbyByIndex(i));
        }

        // 방 목록 갱신 이벤트 발생 (UI 매니저가 이 신호를 받습니다)
        OnLobbyListUpdated?.Invoke(lobbyList);
    }

    // UI 방 목록에서 '참가' 버튼을 눌렀을 때 호출될 함수
    public void JoinLobby(CSteamID lobbyID)
    {
        SteamMatchmaking.JoinLobby(lobbyID);
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        if (NetworkServer.active) return;

        string hostAddress = SteamMatchmaking.GetLobbyData(currentLobbyID, HostAddressKey);
        networkManager.networkAddress = hostAddress;
        networkManager.StartClient();
    }
}