using System;
using Steamworks;
using UnityEngine;
using Mirror;

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
        private CallResult<LobbyMatchList_t> _lobbyMatchList;

        // OnLobbyMatchList 호출 맥락을 구분하기 위한 플래그
        private enum MatchListContext { None, JoinByCode, QuickMatch }
        private MatchListContext _matchListContext = MatchListContext.None;
        private string _pendingCode = null; // JoinByCode 시 입력한 코드 보관

        // ──────────────────────────────────────────
        protected override void Awake()
        {
            base.Awake();
            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
            _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            _lobbyMatchList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
        }

        // ── 1. 방 만들기 ─────────────────────────
        public void CreateLobby(bool isPublic, int maxPlayers)
        {
            isHostingPublic = isPublic;
            Debug.Log($"[LobbyManager] CreateLobby 호출 | isPublic={isPublic} | maxPlayers={maxPlayers}");

#if UNITY_EDITOR
            IsHost = true;
            CurrentRoomCode = GenerateCode();

            Debug.Log($"[LobbyManager] 에디터 방 생성 완료 (localhost) | 코드: {CurrentRoomCode}");
            Debug.LogWarning("[LobbyManager] 에디터 방은 스팀에 등록되지 않으므로 빠른 찾기로 검색할 수 없습니다. 코드 입장만 가능합니다.");

            NetworkManager.singleton.StartHost();
            OnLobbyEntered?.Invoke(true);
#else
            // isPublic 값을 ELobbyType에 실제로 반영합니다.
            SteamMatchmaking.CreateLobby(
                isPublic ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypePrivate,
                maxPlayers
            );
#endif
        }

        private void OnLobbyCreated(LobbyCreated_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError($"[LobbyManager] 방 생성 실패 | 결과 코드: {result.m_eResult}");
                OnLobbyEntered?.Invoke(false);
                return;
            }

            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            CurrentRoomCode = GenerateCode();
            IsHost = true;

            // ── SetLobbyData 결과를 모두 검증합니다 ──
            bool ok1 = SteamMatchmaking.SetLobbyData(CurrentLobbyId, GameKey, GameName);
            bool ok2 = SteamMatchmaking.SetLobbyData(CurrentLobbyId, CodeKey, CurrentRoomCode);
            bool ok3 = SteamMatchmaking.SetLobbyData(CurrentLobbyId, HostAddressKey, SteamUser.GetSteamID().ToString());
            bool ok4 = SteamMatchmaking.SetLobbyData(CurrentLobbyId, IsPublicKey, isHostingPublic ? "True" : "False");
            bool ok5 = SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomStatusKey, StatusLobby);

            Debug.Log($"[LobbyManager] 스팀 방 생성 완료 | 공개: {isHostingPublic} | 코드: {CurrentRoomCode}");
            Debug.Log($"[LobbyManager] SetLobbyData 결과 → GameKey:{ok1} / CodeKey:{ok2} / HostAddress:{ok3} / IsPublic:{ok4} / RoomStatus:{ok5}");

            // 하나라도 실패하면 경고를 띄웁니다.
            if (!ok1 || !ok2 || !ok3 || !ok4 || !ok5)
                Debug.LogWarning("[LobbyManager] ⚠️ SetLobbyData 일부가 실패했습니다! 빠른 찾기 / 코드 검색이 정상 작동하지 않을 수 있습니다.");

            NetworkManager.singleton.StartHost();
            OnLobbyEntered?.Invoke(true);
        }

        // ── 2. 코드로 참가 ─────────────────────────
        public void JoinLobbyByCode(string code)
        {
            code = code.Trim().ToUpper();

#if UNITY_EDITOR
            if (NetworkServer.active)
            {
                Debug.LogWarning("[LobbyManager] 이미 에디터에서 서버가 실행 중입니다! 중복 입장을 거부합니다.");
                OnLobbyEntered?.Invoke(false);
                return;
            }

            IsHost = false;
            CurrentRoomCode = code;
            Debug.Log($"[LobbyManager] 에디터 로컬 접속 시도 (localhost) | 입력한 코드: {code}");

            NetworkManager.singleton.networkAddress = "localhost";
            NetworkManager.singleton.StartClient();
            OnLobbyEntered?.Invoke(true);
#else
            Debug.Log($"[LobbyManager] 코드로 방 검색 시작 | 코드: {code}");
            _matchListContext = MatchListContext.JoinByCode;
            _pendingCode      = code;

            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameName, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(CodeKey, code,     ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(10); // 결과 수 명시

            SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
            _lobbyMatchList.Set(handle);
#endif
        }

        // ── 3. 빠른 참가 ─────────────────────────
        public void JoinQuickMatch()
        {
#if UNITY_EDITOR
            Debug.LogWarning("[LobbyManager] 에디터에서는 빠른 참가가 불가합니다. 코드로 참가(localhost)를 이용하세요.");
            OnLobbyEntered?.Invoke(false);
#else
            Debug.Log("[LobbyManager] 빠른 찾기 시작");
            _matchListContext = MatchListContext.QuickMatch;
            _pendingCode      = null;

            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey,      GameName,    ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(IsPublicKey,  "True",      ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(RoomStatusKey, StatusLobby, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(10); // 결과 수 명시

            SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
            _lobbyMatchList.Set(handle);
#endif
        }

        // ── 매칭 결과 처리 ─────────────────────────
        private void OnLobbyMatchList(LobbyMatchList_t result, bool ioFailure)
        {
            Debug.Log($"[LobbyManager] OnLobbyMatchList 도착 | 맥락: {_matchListContext} | ioFailure: {ioFailure} | 매칭된 방 수: {result.m_nLobbiesMatching}");

            if (ioFailure || result.m_nLobbiesMatching == 0)
            {
                Debug.LogWarning("[LobbyManager] 조건에 맞는 방을 찾지 못했습니다.");

                // 빠른 찾기 실패 시 원인 안내
                if (_matchListContext == MatchListContext.QuickMatch)
                    Debug.LogWarning("[LobbyManager] ▶ 빠른 찾기 실패 원인 후보: ① 호스트가 isPublic=false로 방을 만들었음 ② 방의 RoomStatus가 Lobby가 아님 ③ 스팀 메타데이터 전파 딜레이 (방 생성 직후 조회)");

                _matchListContext = MatchListContext.None;
                OnLobbyEntered?.Invoke(false);
                return;
            }

            // 매칭된 방의 메타데이터를 로그로 확인합니다.
            for (uint i = 0; i < result.m_nLobbiesMatching; i++)
            {
                CSteamID lid = SteamMatchmaking.GetLobbyByIndex((int)i);
                string lobbyIsPublic = SteamMatchmaking.GetLobbyData(lid, IsPublicKey);
                string lobbyStatus = SteamMatchmaking.GetLobbyData(lid, RoomStatusKey);
                string lobbyGameKey = SteamMatchmaking.GetLobbyData(lid, GameKey);
                Debug.Log($"[LobbyManager] 검색된 방[{i}] | LobbyID: {lid} | GameKey: {lobbyGameKey} | IsPublic: {lobbyIsPublic} | RoomStatus: {lobbyStatus}");
            }

            CSteamID matchedLobbyID = SteamMatchmaking.GetLobbyByIndex(0);
            _matchListContext = MatchListContext.None;
            SteamMatchmaking.JoinLobby(matchedLobbyID);
        }

        // ── Steam 초대 수락 ─────────────────────────
        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t result)
        {
            Debug.Log($"[LobbyManager] 스팀 초대 수락 | LobbyID: {result.m_steamIDLobby}");
            SteamMatchmaking.JoinLobby(result.m_steamIDLobby);
        }

        // ── 방 입장 콜백 ─────────────────────────
        private void OnLobbyEnteredCallback(LobbyEnter_t result)
        {
            bool success = result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;
            if (!success)
            {
                Debug.LogError($"[LobbyManager] 방 입장 실패 | 응답 코드: {result.m_EChatRoomEnterResponse}");
                OnLobbyEntered?.Invoke(false);
                return;
            }

            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);

            // 호스트는 이미 StartHost()를 마쳤으므로 여기서 중단합니다.
            if (NetworkServer.active)
            {
                Debug.Log("[LobbyManager] 호스트 본인의 LobbyEntered 콜백 — 무시합니다.");
                return;
            }

            CurrentRoomCode = SteamMatchmaking.GetLobbyData(CurrentLobbyId, CodeKey);
            string hostAddress = SteamMatchmaking.GetLobbyData(CurrentLobbyId, HostAddressKey);

            Debug.Log($"[LobbyManager] 스팀 방 입장 완료 | 코드: {CurrentRoomCode} | 호스트 주소: {hostAddress}");

            NetworkManager.singleton.networkAddress = hostAddress;
            NetworkManager.singleton.StartClient();
            OnLobbyEntered?.Invoke(true);
        }

        // ── 인게임 전환 시 로비 상태 업데이트 ─────
        public void ChangeLobbyStatusToInGame()
        {
            if (!NetworkServer.active) return;
            bool ok = SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomStatusKey, StatusInGame);
            Debug.Log($"[LobbyManager] 로비 상태 → InGame 변경 결과: {ok}");
        }

        // ── 멤버 변동 콜백 ─────────────────────────
        private void OnLobbyChatUpdate(LobbyChatUpdate_t result) { OnMembersChanged?.Invoke(); }

        // ── 방 나가기 ─────────────────────────────
        public void LeaveLobby()
        {
            if (CurrentLobbyId.IsValid())
            {
                SteamMatchmaking.LeaveLobby(CurrentLobbyId);
                Debug.Log($"[LobbyManager] 스팀 로비 퇴장 | LobbyID: {CurrentLobbyId}");
            }

            CurrentLobbyId = CSteamID.Nil;
            CurrentRoomCode = null;
            IsHost = false;

            if (NetworkServer.active) NetworkManager.singleton.StopHost();
            else NetworkManager.singleton.StopClient();
        }

        // ── 유틸 ──────────────────────────────────
        public int GetMemberCount() => CurrentLobbyId.IsValid() ? SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId) : 0;
        public CSteamID GetMember(int index) => SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyId, index);

        private static string GenerateCode()
        {
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
                chars[i] = CodeChars[UnityEngine.Random.Range(0, CodeChars.Length)];
            return new string(chars);
        }
    }
}
