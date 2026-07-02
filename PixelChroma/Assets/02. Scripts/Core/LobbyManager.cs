using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using Mirror;
using PixelChroma.UI;

namespace PixelChroma
{
    public class LobbyManager : MonoSingleton<LobbyManager>
    {
        public const int CodeLength = 6;
        private const string CodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public const string GameKey = "GameID";
        public const string GameName = "PixelChroma_Dev";
        public const string HostAddressKey = "HostAddress";
        public const string CodeKey = "RoomCode";
        public const string IsPublicKey = "IsPublic";
        public const string RoomStatusKey = "RoomStatus";
        public const string StatusLobby = "Lobby";
        public const string StatusInGame = "InGame";

        [Header("Map List")]
        [SerializeField] private List<MapEntry> availableMaps = new();

        public CSteamID CurrentLobbyId { get; private set; }
        public string CurrentRoomCode { get; private set; }
        public bool IsHost { get; private set; }
        public RoomOption CurrentRoomOption { get; private set; } = new();
        public List<MapEntry> AvailableMaps => availableMaps;
        private bool isHostingPublic = false;

        public event Action<bool> OnLobbyEntered;
        public event Action OnMembersChanged;

        private Callback<LobbyCreated_t> _lobbyCreated;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private Callback<LobbyEnter_t> _lobbyEntered;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdate;
        private CallResult<LobbyMatchList_t> _lobbyMatchList;

        protected override void Awake()
        {
            base.Awake();

            // 💡 [방어벽 추가] 부모 싱글톤 검증 결과, 내가 진짜가 아니라 유령이라면
            // 아래에 있는 스팀 콜백 등록 로직을 실행하지 못하도록 즉시 튕겨냅니다.
            if (Instance != this) return;

            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
            _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            _lobbyMatchList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
        }

        // [안전장치 추가] 진짜든 유령이든 씬 전환으로 인해 이 컴포넌트가 파괴될 때,
        // 스팀 통신망에 엮여있던 C++ 비동기 콜백 링크를 명시적으로 완벽하게 끊어줍니다.
        private void OnDestroy()
        {
            _lobbyCreated?.Dispose();
            _joinRequested?.Dispose();
            _lobbyEntered?.Dispose();
            _lobbyChatUpdate?.Dispose();
            _lobbyMatchList?.Dispose();
        }

        // ── 1. 방 만들기 ─────────────────────────
        public void CreateLobby(RoomOption option)
        {
            CurrentRoomOption = option.Clone();
            isHostingPublic = option.isPublic;

#if UNITY_EDITOR
            IsHost = true;
            CurrentRoomCode = GenerateCode();
            Debug.Log($"[LobbyManager] 에디터 방 생성 완료 (localhost) | 코드: {CurrentRoomCode} | 모드: {option.gameMode} | 공개: {option.isPublic}");

            NetworkManager.singleton.StartHost();
            OnLobbyEntered?.Invoke(true);
#else
            var lobbyType = option.isPublic ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly;
            SteamMatchmaking.CreateLobby(lobbyType, option.maxPlayers);
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

            NetworkManager.singleton.StartHost();
            OnLobbyEntered?.Invoke(true);
        }

        public void NotifyClientConnected()
        {
            Debug.Log($"[LobbyManager] 클라이언트 연결 성공 | 코드: {CurrentRoomCode}");
            OnLobbyEntered?.Invoke(true);
        }

        public void NotifyClientDisconnected()
        {
            if (_waitingForConnection)
            {
                _waitingForConnection = false;
                Debug.LogWarning("[LobbyManager] 클라이언트 연결 실패");
                OnLobbyEntered?.Invoke(false);
            }
        }

        private bool _waitingForConnection;

        // ── 2. 코드로 참가 ─────────────────────────
        public void JoinLobbyByCode(string code)
        {
            code = code.Trim().ToUpper();

#if UNITY_EDITOR
            if (NetworkServer.active)
            {
                Debug.LogWarning("[LobbyManager] 이미 당신의 에디터에서 서버가 실행 중입니다! 중복 입장을 거부합니다.");
                OnLobbyEntered?.Invoke(false);
                return;
            }

            IsHost = false;
            CurrentRoomCode = code;
            _waitingForConnection = true;
            Debug.Log($"[LobbyManager] 에디터 로컬 접속 시도 (localhost) | 입력한 코드: {code}");

            NetworkManager.singleton.networkAddress = "localhost";
            NetworkManager.singleton.StartClient();
#else
            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameName, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(CodeKey, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);

            SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
            _lobbyMatchList.Set(handle);
#endif
        }

        // ── 3. 빠른 참가 ─────────────────────────
        public void JoinQuickMatch()
        {
#if UNITY_EDITOR
            if (NetworkServer.active)
            {
                Debug.LogWarning("[LobbyManager] 이미 당신의 에디터에서 서버가 실행 중입니다! 중복 입장을 거부합니다.");
                OnLobbyEntered?.Invoke(false);
                return;
            }

            IsHost = false;
            _waitingForConnection = true;
            Debug.Log("[LobbyManager] 에디터 빠른 참가 시도 (localhost)");

            NetworkManager.singleton.networkAddress = "localhost";
            NetworkManager.singleton.StartClient();
#else
            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameName, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(IsPublicKey, "True", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(RoomStatusKey, StatusLobby, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);

            SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
            _lobbyMatchList.Set(handle);
#endif
        }

        private void OnLobbyMatchList(LobbyMatchList_t result, bool ioFailure)
        {
            if (ioFailure || result.m_nLobbiesMatching == 0)
            {
                Debug.LogWarning("[LobbyManager] 조건에 맞는 방을 찾지 못했습니다.");
                OnLobbyEntered?.Invoke(false);
                return;
            }

            CSteamID matchedLobbyID = SteamMatchmaking.GetLobbyByIndex(0);
            SteamMatchmaking.JoinLobby(matchedLobbyID);
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t result) { SteamMatchmaking.JoinLobby(result.m_steamIDLobby); }

        private void OnLobbyEnteredCallback(LobbyEnter_t result)
        {
            bool success = result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;
            if (!success)
            {
                Debug.LogError($"[LobbyManager] 방 입장 실패 코드: {result.m_EChatRoomEnterResponse}");
                OnLobbyEntered?.Invoke(false);
                return;
            }

            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            if (NetworkServer.active) return;

            CurrentRoomCode = SteamMatchmaking.GetLobbyData(CurrentLobbyId, CodeKey);
            string hostAddress = SteamMatchmaking.GetLobbyData(CurrentLobbyId, HostAddressKey);

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();

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