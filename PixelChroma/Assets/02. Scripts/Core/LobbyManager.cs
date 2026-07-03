using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using Mirror;
using PixelChroma.UI;
using Cysharp.Threading.Tasks;

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

        public event Action OnLobbyFailed;
        public event Action OnMembersChanged;

        private Callback<LobbyCreated_t> _lobbyCreated;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private Callback<LobbyEnter_t> _lobbyEntered;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdate;
        private CallResult<LobbyMatchList_t> _lobbyMatchList;

        private bool isHostingPublic = false;
        private bool _waitingForConnection;

        public CSteamID CurrentLobbyId { get; private set; }
        public string CurrentRoomCode { get; private set; }
        public bool IsHost { get; private set; }
        public RoomOption CurrentRoomOption { get; set; } = new();
        public List<MapEntry> AvailableMaps => availableMaps;

        protected override void Awake()
        {
            base.Awake();

            // 부모 싱글톤 검증 결과, 내가 진짜가 아니라 유령이라면
            // 아래에 있는 스팀 콜백 등록 로직을 실행하지 못하도록 즉시 튕겨냅니다.
            if (Instance != this) return;

            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
            _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            _lobbyMatchList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
        }

        // 진짜든 유령이든 씬 전환으로 인해 이 컴포넌트가 파괴될 때,
        // 스팀 통신망에 엮여있던 C++ 비동기 콜백 링크를 명시적으로 완벽하게 끊어줍니다.
        private void OnDestroy()
        {
            _lobbyCreated?.Dispose();
            _joinRequested?.Dispose();
            _lobbyEntered?.Dispose();
            _lobbyChatUpdate?.Dispose();
            _lobbyMatchList?.Dispose();
        }

        public void NotifyClientConnected()
        {
            _waitingForConnection = false;
            Debug.Log($"[LobbyManager] 클라이언트 연결 성공 | 코드: {CurrentRoomCode}");
            OnLobbySuccess();
        }

        public void NotifyClientDisconnected()
        {
            if (_waitingForConnection)
            {
                _waitingForConnection = false;
                Debug.LogWarning("[LobbyManager] 클라이언트 연결 실패");
                OnLobbyFailed?.Invoke();
            }
        }

        // ── 1. 방 만들기 ─────────────────────────
        public void CreateLobby(RoomOption option)
        {
            CurrentRoomOption = option.Clone();
            isHostingPublic = option.isPublic;

            // ⭐ [핵심 추가] 방 생성 시 Mirror 네트워크의 최대 접속자 수도 동기화합니다!
            NetworkManager.singleton.maxConnections = CurrentRoomOption.maxPlayers;

#if UNITY_EDITOR
            IsHost = true;
            CurrentRoomCode = GenerateCode();
            Debug.Log($"[LobbyManager] 에디터 방 생성 완료 (localhost) | 코드: {CurrentRoomCode} | 모드: {option.gameMode} | 공개: {option.isPublic}");

            NetworkManager.singleton.StartHost();
            OnLobbySuccess();
#else            
            var lobbyType = option.isPublic ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly;
            // 여기서 스팀 로비 제한은 잘 걸리고 있었습니다.
            SteamMatchmaking.CreateLobby(lobbyType, option.maxPlayers);
#endif
        }

        // ── [신규 추가] 로비 안에서 방 설정이 변경되었을 때 호출할 함수 ──
        public void UpdateLobbySettings(RoomOption newOption)
        {
            // 방장이 아니면 튕겨냅니다. (스팀 로비 ID 체크는 여기서 하지 않습니다)
            if (!IsHost) return;

            // 1. 장부부터 무조건 최신화합니다!
            CurrentRoomOption = newOption.Clone();

            // 2. Mirror 엔진 접속 제한 즉시 업데이트
            if (NetworkManager.singleton != null)
            {
                NetworkManager.singleton.maxConnections = CurrentRoomOption.maxPlayers;
            }

            // 3. 실제 스팀 빌드일 때만 스팀 서버 쪽에 인원/공개 여부를 갱신 요청합니다.
#if !UNITY_EDITOR
            if (CurrentLobbyId.IsValid())
            {
                SteamMatchmaking.SetLobbyMemberLimit(CurrentLobbyId, CurrentRoomOption.maxPlayers);
                SteamMatchmaking.SetLobbyData(CurrentLobbyId, IsPublicKey, CurrentRoomOption.isPublic ? "True" : "False");
            }
#endif

            Debug.Log($"[LobbyManager] 방 설정 갱신 완료! 최대 인원: {CurrentRoomOption.maxPlayers}명, 숨는 시간: {CurrentRoomOption.hideTime}초");
        }

        private void OnLobbyCreated(LobbyCreated_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK)
            {
                OnLobbyFailed?.Invoke();
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
            OnLobbySuccess();
        }

        private void OnLobbySuccess()
        {
            GameManager.Instance.EnterLobby().Forget();
        }


#if UNITY_EDITOR
        // 에디터 전용 로컬 클라이언트 접속 (코드 참가 / 빠른 참가 공용)
        private void StartEditorClient(string roomCode = null)
        {
            if (NetworkServer.active)
            {
                Debug.LogWarning("[LobbyManager] 이미 당신의 에디터에서 서버가 실행 중입니다! 중복 입장을 거부합니다.");
                OnLobbyFailed?.Invoke();
                return;
            }

            IsHost = false;
            CurrentRoomCode = roomCode;
            _waitingForConnection = true;
            Debug.Log(roomCode != null
                ? $"[LobbyManager] 에디터 로컬 접속 시도 (localhost) | 입력한 코드: {roomCode}"
                : "[LobbyManager] 에디터 빠른 참가 시도 (localhost)");

            NetworkManager.singleton.networkAddress = "localhost";
            NetworkManager.singleton.StartClient();
        }
#endif

        // ── 2. 코드로 참가 ─────────────────────────
        public void JoinLobbyByCode(string code)
        {
            code = code.Trim().ToUpper();

#if UNITY_EDITOR
            StartEditorClient(code);
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
            StartEditorClient();
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
                OnLobbyFailed?.Invoke();
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
                OnLobbyFailed?.Invoke();
                return;
            }

            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            if (NetworkServer.active) return;

            CurrentRoomCode = SteamMatchmaking.GetLobbyData(CurrentLobbyId, CodeKey);
            string hostAddress = SteamMatchmaking.GetLobbyData(CurrentLobbyId, HostAddressKey);

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();

            Debug.Log($"[LobbyManager] 스팀 방 입장 완료, Mirror 연결 대기 중 | 코드: {CurrentRoomCode}");
            _waitingForConnection = true;
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