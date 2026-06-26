using UnityEngine;
using Mirror;
using Steamworks;

public class SteamLobbyManager : MonoBehaviour
{
    // 스팀 콜백 리스너
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> joinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    // 호스트의 SteamID를 저장할 키값
    private const string HostAddressKey = "HostAddress";

    private ChromaNetworkManager networkManager;

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobbyManager] Steam이 켜져있지 않거나 초기화 실패!");
            return;
        }

        networkManager = GetComponent<ChromaNetworkManager>();

        // 콜백 함수 등록
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    // [UI 버튼 연결용] 방 만들기 버튼을 눌렀을 때
    public void HostLobby()
    {
        Debug.Log("[SteamLobbyManager] Steam 로비 생성 요청 중...");
        // 최대 10명, 친구 전용(또는 Public) 로비 생성
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 10);
    }

    // [콜백] 방 생성이 완료되었을 때
    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("[SteamLobbyManager] 로비 생성 실패");
            return;
        }

        // 1. 내가 만든 스팀 로비에 내 스팀 ID를 호스트 주소로 기록
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey, SteamUser.GetSteamID().ToString());

        // 2. Mirror 네트워크 호스트(서버+클라이언트) 시작
        networkManager.StartHost();

        Debug.Log($"[SteamLobbyManager] 로비 생성 성공! Mirror Host 구동 완료. 로비 씬으로 이동합니다.");
        // ChromaNetworkManager의 씬 이동 로직 호출
        //networkManager.ServerChangeScene("LobbyScene");
    }

    // [콜백] 스팀 오버레이에서 친구 초대를 수락했을 때
    private void OnJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("[SteamLobbyManager] 친구 초대 수락, 로비 입장 요청 중...");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    // [콜백] 로비 입장에 성공했을 때 (호스트가 아닌 클라이언트 전용)
    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        // 호스트는 이미 StartHost()를 했으므로 무시
        if (NetworkServer.active) return;

        // 방장이 등록해둔 HostAddressKey(스팀 ID)를 읽어옴
        string hostAddress = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey);

        // Mirror NetworkManager에 스팀 ID를 주소로 입력하고 클라이언트로 접속
        networkManager.networkAddress = hostAddress;
        networkManager.StartClient();
        Debug.Log($"[SteamLobbyManager] 호스트({hostAddress})에게 Mirror Client 접속 요청!");
    }
}