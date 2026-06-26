using UnityEngine;
using Mirror;
using Steamworks;
using System.Collections.Generic;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager instance;

    protected Callback<LobbyMatchList_t> lobbyListRequested;
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> joinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    private const string HostAddressKey = "HostAddress";
    public const string RoomStatusKey = "RoomStatus";
    public const string StatusLobby = "Lobby";
    public const string StatusInGame = "InGame";

    public CSteamID currentLobbyID;
    private ChromaNetworkManager networkManager;

    // [신규] 방 생성 시 공개 여부를 기억할 변수
    private bool isHostingPublic = false;

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

    // [수정됨] 방 생성 (공개/비공개 여부를 매개변수로 받음)
    public void HostLobby(bool isPublic, int maxPlayers)
    {
        isHostingPublic = isPublic;

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK) return;

        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        // 1. 기본 정보 세팅
        SteamMatchmaking.SetLobbyData(currentLobbyID, HostAddressKey, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(currentLobbyID, RoomStatusKey, StatusLobby);
        SteamMatchmaking.SetLobbyData(currentLobbyID, "GameID", "PixelChroma_Dev");

        // 2. 6자리 랜덤 코드 발급 및 등록
        string roomCode = GenerateRoomCode();
        SteamMatchmaking.SetLobbyData(currentLobbyID, "RoomCode", roomCode);

        // 3. 공개/비공개 태그 등록
        SteamMatchmaking.SetLobbyData(currentLobbyID, "IsPublic", isHostingPublic ? "True" : "False");

        Debug.Log($"[SteamLobbyManager] 방 생성 완료! 입장 코드: {roomCode} / 공개여부: {isHostingPublic}");

        networkManager.StartHost();
    }

    public void ChangeLobbyStatusToInGame()
    {
        if (NetworkServer.active)
            SteamMatchmaking.SetLobbyData(currentLobbyID, RoomStatusKey, StatusInGame);
    }

    // [신규] 6자리 랜덤 코드 생성기
    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < 6; i++)
        {
            code += chars[Random.Range(0, chars.Length)];
        }
        return code;
    }

    // ==========================================================
    // [신규] 검색 및 매치메이킹 구역
    // ==========================================================

    // 1. 코드로 직접 입장하기
    public void JoinLobbyByCode(string inputCode)
    {
        inputCode = inputCode.ToUpper(); // 소문자로 쳐도 대문자로 변환
        Debug.Log($"[SteamLobbyManager] 코드 '{inputCode}'로 방 검색 중...");

        // 필터 1: 우리 게임인지?
        SteamMatchmaking.AddRequestLobbyListStringFilter("GameID", "PixelChroma_Dev", ELobbyComparison.k_ELobbyComparisonEqual);
        // 필터 2: 코드가 완벽히 일치하는지?
        SteamMatchmaking.AddRequestLobbyListStringFilter("RoomCode", inputCode, ELobbyComparison.k_ELobbyComparisonEqual);

        SteamMatchmaking.RequestLobbyList();
    }

    // 2. 빠른 참가 (랜덤 매치)
    public void JoinQuickMatch()
    {
        Debug.Log("[SteamLobbyManager] 공개된 방으로 빠른 참가 검색 중...");

        // 필터 1: 우리 게임인지?
        SteamMatchmaking.AddRequestLobbyListStringFilter("GameID", "PixelChroma_Dev", ELobbyComparison.k_ELobbyComparisonEqual);
        // 필터 2: 공개방(IsPublic = True)인지?
        SteamMatchmaking.AddRequestLobbyListStringFilter("IsPublic", "True", ELobbyComparison.k_ELobbyComparisonEqual);
        // 필터 3: 대기실(Lobby) 상태인지? (게임 중인 방 난입 방지)
        SteamMatchmaking.AddRequestLobbyListStringFilter(RoomStatusKey, StatusLobby, ELobbyComparison.k_ELobbyComparisonEqual);

        SteamMatchmaking.RequestLobbyList();
    }

    // 3. 검색 결과 처리 (코드로 찾든, 빠른 매치로 찾든 무조건 첫 번째 방으로 강제 입장)
    private void OnLobbyMatchList(LobbyMatchList_t callback)
    {
        if (callback.m_nLobbiesMatching > 0)
        {
            CSteamID matchedLobbyID = SteamMatchmaking.GetLobbyByIndex(0);
            Debug.Log("[SteamLobbyManager] 방을 찾았습니다! 즉시 입장합니다.");
            SteamMatchmaking.JoinLobby(matchedLobbyID);
        }
        else
        {
            Debug.LogWarning("[SteamLobbyManager] 조건에 맞는 방을 찾지 못했습니다. (코드 오류 또는 방 없음)");
            // TODO: UI 매니저를 통해 "방을 찾을 수 없습니다" 팝업 띄우기
        }
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t callback) { SteamMatchmaking.JoinLobby(callback.m_steamIDLobby); }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        if (NetworkServer.active) return;

        string hostAddress = SteamMatchmaking.GetLobbyData(currentLobbyID, HostAddressKey);
        networkManager.networkAddress = hostAddress;
        networkManager.StartClient();
    }
}