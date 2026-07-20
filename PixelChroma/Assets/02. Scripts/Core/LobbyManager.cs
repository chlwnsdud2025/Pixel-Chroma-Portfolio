using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using Mirror;
using UnityEngine.Localization.Settings;

using Cysharp.Threading.Tasks;

namespace PixelChroma
{
    public class LobbyManager : MonoSingleton<LobbyManager>
    {
        public const int CodeLength = 6;

        private const string CodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public const string GameKey = "GameID";
        public const string GameName = "PixelChroma_Dev";
        public const string IsPublicKey = "IsPublic";
        public const string RoomStatusKey = "RoomStatus";
        public const string StatusLobby = "Lobby";

        private const string HostAddressKey = "HostAddress";
        private const string CodeKey = "RoomCode";
        public const string StatusInGame = "InGame";

        public const string GameModeKey = "GameMode";
        public const string MapIdKey = "MapId";
        public const string LanguageKey = "Language";
        public const string RoomNameKey = "RoomName";
        public const string HostNameKey = "HostName";


        [Header("Map List")]
        [SerializeField] private MapRegistrySO mapRegistry;

        public event Action OnLobbyFailed;
        public event Action OnMembersChanged;

        private Callback<LobbyCreated_t> _lobbyCreated;             // CreateLobby() 호출 후 Steam 서버가 로비 생성 결과를 돌려줄 때
        private CallResult<LobbyMatchList_t> _lobbyMatchList;       // RequestLobbyList() 호출 후 검색 결과가 돌아왔을 때 (코드 참가 전용)
        private Callback<GameLobbyJoinRequested_t> _joinRequested;  // Steam 오버레이에서 친구가 "게임 참가"를 클릭했을 때
        private Callback<LobbyEnter_t> _lobbyEntered;               // JoinLobby() 호출 후 로비 입장이 완료되었을 때 (성공/실패 모두)
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdate;       // 로비 멤버 변동 시 (입장, 퇴장, 추방 등)

        private bool _waitingForConnection;

        public CSteamID CurrentLobbyId { get; private set; }

        // 방 코드는 RoomOption.roomCode를 단일 원본으로 사용합니다. (별도 저장 없이 통과시키는 프로퍼티)
        public string CurrentRoomCode
        {
            get => CurrentRoomOption?.roomCode;
            set { if (CurrentRoomOption != null) CurrentRoomOption.roomCode = value; }
        }

        // 호스트 여부는 Mirror 서버 활성 상태를 단일 원본으로 사용합니다. (별도 저장 없이 유도)
        public bool IsHost => NetworkServer.active;

        public RoomOption CurrentRoomOption { get; set; } = new();
        public MapRegistrySO MapRegistry => mapRegistry;

        protected override void Awake()
        {
            base.Awake();

            // 부모 싱글톤 검증 결과, 내가 진짜가 아니라 유령이라면
            // 아래에 있는 스팀 콜백 등록 로직을 실행하지 못하도록 즉시 튕겨냅니다.
            if (Instance != this) return;

            // 스팀이 활성인 빌드에서만 스팀 콜백을 등록합니다. (에디터는 localhost 테스트라 스팀 미사용)
            if (GameManager.SteamActive)
            {
                _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
                _lobbyMatchList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
                _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
                _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
                _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            }
        }

        // 진짜든 유령이든 씬 전환으로 인해 이 컴포넌트가 파괴될 때,
        // 스팀 통신망에 엮여있던 C++ 비동기 콜백 링크를 명시적으로 완벽하게 끊어줍니다.
        private void OnDestroy()
        {
            if (GameManager.SteamActive)
            {
                _lobbyCreated?.Dispose();
                _lobbyMatchList?.Dispose();
                _joinRequested?.Dispose();
                _lobbyEntered?.Dispose();
                _lobbyChatUpdate?.Dispose();
            }
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
            CurrentRoomOption.hostName = GetLocalPlayerName();

            NetworkManager.singleton.maxConnections = CurrentRoomOption.maxPlayers;

            if (!GameManager.SteamActive)
            {
                // 스팀 미사용(에디터/로컬): 곧바로 localhost 호스트로 시작합니다.
                CurrentRoomCode = GenerateCode();
                Debug.Log($"[LobbyManager] 로컬 방 생성 완료 (localhost) | 코드: {CurrentRoomCode} | 모드: {option.gameMode} | 공개: {option.isPublic}");

                NetworkManager.singleton.StartHost();
                return;
            }

            var lobbyType = option.isPublic ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly;
            // 스팀 로비 생성 (인원 제한 포함). 결과는 OnLobbyCreated 콜백에서 처리됩니다.
            SteamMatchmaking.CreateLobby(lobbyType, option.maxPlayers);
        }

        // ── 2. 코드로 참가 ─────────────────────────
        public void JoinLobbyByCode(string code)
        {
            code = code.Trim().ToUpper();

            if (!GameManager.SteamActive)
            {
                StartLocalClient(code);
                return;
            }

            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameName, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(CodeKey, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);

            SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
            _lobbyMatchList.Set(handle);
        }

        // ── 3. 빠른 참가 ─────────────────────────
        public void JoinQuickMatch()
        {
            if (!GameManager.SteamActive)
            {
                StartLocalClient();
                return;
            }

            var searcher = QuickMatchSearcher.Instance;
            searcher.OnMatchFound -= HandleQuickMatchFound;
            searcher.OnMatchFailed -= HandleQuickMatchFailed;
            searcher.OnMatchFound += HandleQuickMatchFound;
            searcher.OnMatchFailed += HandleQuickMatchFailed;
            searcher.StartSearch();
        }

        private void HandleQuickMatchFound(CSteamID lobbyId)
        {
            UnsubscribeQuickMatch();
            SteamMatchmaking.JoinLobby(lobbyId);
        }

        private void HandleQuickMatchFailed()
        {
            UnsubscribeQuickMatch();
            OnLobbyFailed?.Invoke();
        }

        public void CancelQuickMatch()
        {
            UnsubscribeQuickMatch();
            QuickMatchSearcher.Instance?.Cancel();
        }

        private void UnsubscribeQuickMatch()
        {
            var searcher = QuickMatchSearcher.Instance;
            if (searcher == null) return;
            searcher.OnMatchFound -= HandleQuickMatchFound;
            searcher.OnMatchFailed -= HandleQuickMatchFailed;
        }

        // ── 4. 방 목록에서 선택한 방에 입장 ─────────────────────────
        public void JoinRoom(RoomListEntry entry)
        {
            if (IsHost || NetworkClient.active)
            {
                Debug.LogWarning("[LobbyManager] 이미 연결 중입니다! 중복 입장을 거부합니다.");
                OnLobbyFailed?.Invoke();
                return;
            }

            if (entry.IsSteamRoom)
            {
                SteamMatchmaking.JoinLobby(entry.steamLobbyId);
            }
            else
            {
                _waitingForConnection = true;
                NetworkManager.singleton.networkAddress = entry.connectAddress;
                NetworkManager.singleton.StartClient();
            }
        }

        // ── 로비 안에서 방 설정이 변경되었을 때 호출할 함수 ──
        public void UpdateLobbySettings(RoomOption newOption)
        {
            // 방장이 아니면 튕겨냅니다. (스팀 로비 ID 체크는 여기서 하지 않습니다)
            if (!IsHost) return;

            string preservedCode = CurrentRoomCode;
            string preservedHostName = CurrentRoomOption?.hostName ?? "";
            CurrentRoomOption = newOption.Clone();
            CurrentRoomOption.roomCode = preservedCode;
            CurrentRoomOption.hostName = preservedHostName;

            // 2. Mirror 엔진 접속 제한 즉시 업데이트
            if (NetworkManager.singleton != null)
            {
                NetworkManager.singleton.maxConnections = CurrentRoomOption.maxPlayers;
            }

            // 3. 스팀이 활성일 때만 스팀 서버 쪽에 인원/공개 여부를 갱신 요청합니다.
            if (GameManager.SteamActive && CurrentLobbyId.IsValid())
            {
                SteamMatchmaking.SetLobbyMemberLimit(CurrentLobbyId, CurrentRoomOption.maxPlayers);
                ApplyLobbyMetadata();
            }

            LobbyPlayerState.BroadcastRoomOption();

            Debug.Log($"[LobbyManager] 방 설정 갱신 완료! 최대 인원: {CurrentRoomOption.maxPlayers}명, 숨는 시간: {CurrentRoomOption.hideTime}초");
        }
 
        private void OnLobbySuccess()
        {
            GameManager.Instance.EnterLobby().Forget();
        }

        // 스팀 미사용(에디터/로컬) 전용 로컬 클라이언트 접속 (코드 참가 / 빠른 참가 공용)
        private void StartLocalClient(string roomCode = null)
        {
            if (IsHost)
            {
                Debug.LogWarning("[LobbyManager] 이미 서버가 실행 중입니다! 중복 입장을 거부합니다.");
                OnLobbyFailed?.Invoke();
                return;
            }

            if (!string.IsNullOrEmpty(roomCode))
                CurrentRoomCode = roomCode;

            _waitingForConnection = true;
            Debug.Log(roomCode != null
                ? $"[LobbyManager] 로컬 접속 시도 (localhost) | 입력한 코드: {roomCode}"
                : "[LobbyManager] 로컬 빠른 참가 시도 (localhost)");

            NetworkManager.singleton.networkAddress = "localhost";
            NetworkManager.singleton.StartClient();
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

            ApplyLobbyMetadata();

            Debug.Log($"[LobbyManager] 스팀 방 생성 완료 | 코드: {CurrentRoomCode}");
            NetworkManager.singleton.StartHost();
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

            if (IsHost) 
                return;

            CurrentRoomCode = SteamMatchmaking.GetLobbyData(CurrentLobbyId, CodeKey);
            string hostAddress = SteamMatchmaking.GetLobbyData(CurrentLobbyId, HostAddressKey);

            _waitingForConnection = true;
            Debug.Log($"[LobbyManager] 스팀 방 입장 완료, Mirror 연결 대기 중 | 코드: {CurrentRoomCode}");

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t result) { OnMembersChanged?.Invoke(); }

        public void ChangeLobbyStatusToInGame()
        {
            if (GameManager.SteamActive && NetworkServer.active)
                SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomStatusKey, StatusInGame);
        }

        public void LeaveLobby()
        {
            if (CurrentLobbyId.IsValid()) 
                SteamMatchmaking.LeaveLobby(CurrentLobbyId);

            CurrentLobbyId = CSteamID.Nil;
            CurrentRoomCode = null;

            if (IsHost) 
                NetworkManager.singleton.StopHost();
            else 
                NetworkManager.singleton.StopClient();
        }

        // ── 유틸 ───────────────────────────────────────────────

        public int GetMemberCount() =>
            CurrentLobbyId.IsValid() ? SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId) : 0;

        public CSteamID GetMember(int index) =>
            SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyId, index);

        private static string GetLocalPlayerName()
        {
            string nickname = PlayerPrefs.GetString("CustomPlayerNickname", "");
            if (string.IsNullOrEmpty(nickname) && GameManager.SteamActive)
                nickname = SteamFriends.GetPersonaName();
            if (string.IsNullOrEmpty(nickname))
                nickname = "Player";
            return nickname;
        }

        private static string GenerateCode()
        {
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++) chars[i] = CodeChars[UnityEngine.Random.Range(0, CodeChars.Length)];
            return new string(chars);
        }

        private void ApplyLobbyMetadata() // 방찾기 설정
        {
            if (!CurrentLobbyId.IsValid()) return;

            var option = CurrentRoomOption;

            SteamMatchmaking.SetLobbyData(CurrentLobbyId, GameKey, GameName);
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, CodeKey, CurrentRoomCode);
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, HostAddressKey, SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, IsPublicKey, option.isPublic ? "True" : "False");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomStatusKey, StatusLobby);

            // ── 3단계 추가 ──
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, GameModeKey, ((int)option.gameMode).ToString());
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, MapIdKey, option.mapName ?? "");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomNameKey, option.roomName ?? "");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, HostNameKey, option.hostName ?? "");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, LanguageKey, GetCurrentLanguageCode());
        }

        private static string GetCurrentLanguageCode()
        {
            return LocalizationSettings.SelectedLocale != null
                ? LocalizationSettings.SelectedLocale.Identifier.Code
                : "";
        }

       
       
    }
}