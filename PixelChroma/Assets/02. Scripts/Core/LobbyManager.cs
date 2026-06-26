using System;
using Steamworks;
using UnityEngine;
using Mirror; // Mirror 연동 추가

namespace PixelChroma
{
    public class LobbyManager : MonoSingleton<LobbyManager>
    {
        private const int CodeLength = 6;
        private const string CodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public const string GameKey = "GameID";
        public const string GameName = "PixelChroma_Dev";
        public const string HostAddressKey = "HostAddress";
        public const string CodeKey = "RoomCode";
        public const string IsPublicKey = "IsPublic";
        public const string RoomStatusKey = "RoomStatus";
        public const string StatusLobby = "Lobby";
        public const string StatusInGame = "InGame";

        public CSteamID CurrentLobbyId { get; private set; }
        public string CurrentRoomCode { get; private set; }
        public bool IsHost { get; private set; }
        private bool isHostingPublic = false;

        public event Action<bool> OnLobbyEntered;
        public event Action OnMembersChanged;

        private Callback<LobbyCreated_t> _lobbyCreated;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private Callback<LobbyEnter_t> _lobbyEntered;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdate;
        private Callback<LobbyMatchList_t> _lobbyMatchList;

        protected override void Awake()
        {
            base.Awake();
            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
            _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            _lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
        }

        // ── 1. 방 만들기 ─────────────────────────
        public void CreateLobby(bool isPublic, int maxPlayers)
        {
            isHostingPublic = isPublic;

#if UNITY_EDITOR
            IsHost = true;
            CurrentRoomCode = GenerateCode();
            Debug.Log($"[LobbyManager] 에디터 방 생성 (localhost) | 코드: {CurrentRoomCode}");

            NetworkManager.singleton.StartHost(); // Mirror 호스트 시작
            OnLobbyEntered?.Invoke(true);
#else
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers);
#endif
        }

        private void OnLobbyCreated(LobbyCreated_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK)
            {
                OnLobbyEntered?.Invoke(false);
                return;
            }

            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            CurrentRoomCode = GenerateCode();
            IsHost = true;

            SteamMatchmaking.SetLobbyData(CurrentLobbyId, GameKey, GameName);
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, CodeKey, CurrentRoomCode);
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, HostAddressKey, SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, IsPublicKey, isHostingPublic ? "True" : "False");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomStatusKey, StatusLobby);

            Debug.Log($"[LobbyManager] 스팀 방 생성 완료 | 코드: {CurrentRoomCode}");

            NetworkManager.singleton.StartHost(); // Mirror 호스트 시작
            OnLobbyEntered?.Invoke(true);
        }

        // ── 2. 코드로 참가 ─────────────────────────
        public void JoinLobbyByCode(string code)
        {
            code = code.Trim().ToUpper();

#if UNITY_EDITOR
            IsHost = false;
            CurrentRoomCode = code;
            Debug.Log($"[LobbyManager] 에디터 방 입장 (localhost) | 코드: {code}");

            NetworkManager.singleton.networkAddress = "localhost";
            NetworkManager.singleton.StartClient(); // Mirror 클라이언트 접속
            OnLobbyEntered?.Invoke(true);
#else
            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameName, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(CodeKey, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.RequestLobbyList();
#endif
        }

        // ── 3. 빠른 참가 ─────────────────────────
        public void JoinQuickMatch()
        {
#if UNITY_EDITOR
            Debug.LogWarning("[LobbyManager] 에디터에서는 빠른 참가가 불가합니다. 코드로 참가(localhost)를 이용하세요.");
            OnLobbyEntered?.Invoke(false);
#else
            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameName, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(IsPublicKey, "True", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(RoomStatusKey, StatusLobby, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.RequestLobbyList();
#endif
        }

        private void OnLobbyMatchList(LobbyMatchList_t result)
        {
            if (result.m_nLobbiesMatching > 0)
            {
                CSteamID matchedLobbyID = SteamMatchmaking.GetLobbyByIndex(0);
                SteamMatchmaking.JoinLobby(matchedLobbyID);
            }
            else
            {
                Debug.LogWarning("[LobbyManager] 방을 찾지 못했습니다.");
                OnLobbyEntered?.Invoke(false);
            }
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t result) { SteamMatchmaking.JoinLobby(result.m_steamIDLobby); }

        private void OnLobbyEnteredCallback(LobbyEnter_t result)
        {
            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            if (NetworkServer.active) return; // 호스트는 무시

            CurrentRoomCode = SteamMatchmaking.GetLobbyData(CurrentLobbyId, CodeKey);
            string hostAddress = SteamMatchmaking.GetLobbyData(CurrentLobbyId, HostAddressKey);

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient(); // Mirror 클라이언트 접속

            Debug.Log($"[LobbyManager] 스팀 방 입장 완료 | 코드: {CurrentRoomCode}");
            OnLobbyEntered?.Invoke(true);
        }

        public void ChangeLobbyStatusToInGame()
        {
            if (NetworkServer.active) SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomStatusKey, StatusInGame);
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t result) { OnMembersChanged?.Invoke(); }

        public void LeaveLobby()
        {
            if (CurrentLobbyId.IsValid()) SteamMatchmaking.LeaveLobby(CurrentLobbyId);
            CurrentLobbyId = CSteamID.Nil;
            CurrentRoomCode = null;
            IsHost = false;

            if (NetworkServer.active) NetworkManager.singleton.StopHost();
            else NetworkManager.singleton.StopClient();
        }
        // ── 유틸 ───────────────────────────────────────────────

        public int GetMemberCount() =>
            CurrentLobbyId.IsValid() ? SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId) : 0;

        public CSteamID GetMember(int index) =>
            SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyId, index);
        private static string GenerateCode()
        {
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++) chars[i] = CodeChars[UnityEngine.Random.Range(0, CodeChars.Length)];
            return new string(chars);
        }


    }
}