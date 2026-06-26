using System;
using Steamworks;
using UnityEngine;

namespace PixelChroma
{
    public class LobbyManager : MonoSingleton<LobbyManager>
    {
        private const int MaxMembers = 8;
        private const string CodeKey = "room_code";
        private const string GameKey = "game";
        private const string GameName = "PixelChroma";
        public const string HostAddressKey = "HostAddress";
        private const int CodeLength = 6;
        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public CSteamID CurrentLobbyId { get; private set; }
        public string CurrentRoomCode { get; private set; }
        public bool IsHost { get; private set; }

        public event Action<bool> OnLobbyEntered;
        public event Action OnMembersChanged;

        private Callback<LobbyCreated_t> _lobbyCreated;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private Callback<LobbyEnter_t> _lobbyEntered;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdate;
        private CallResult<LobbyMatchList_t> _lobbyMatchList;

        private string _pendingCode;

        protected override void Awake()
        {
            base.Awake();
            _lobbyCreated    = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _joinRequested   = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _lobbyEntered    = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
            _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            _lobbyMatchList  = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
        }

        // ── 방 만들기 ──────────────────────────────────────────

        public void CreateLobby()
        {
#if UNITY_EDITOR
            IsHost = true;
            CurrentRoomCode = GenerateCode();
            Debug.Log($"[LobbyManager] 에디터 방 생성 | 코드: {CurrentRoomCode}");
            OnLobbyEntered?.Invoke(true);
#else
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, MaxMembers);
#endif
        }

        private void OnLobbyCreated(LobbyCreated_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError($"[LobbyManager] 방 생성 실패: {result.m_eResult}");
                OnLobbyEntered?.Invoke(false);
                return;
            }

            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            CurrentRoomCode = GenerateCode();
            IsHost = true;

            SteamMatchmaking.SetLobbyData(CurrentLobbyId, GameKey, GameName);
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, CodeKey, CurrentRoomCode);
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, HostAddressKey, SteamUser.GetSteamID().ToString());

            Debug.Log($"[LobbyManager] 방 생성 완료 | 코드: {CurrentRoomCode}");
        }

        // ── 코드로 입장 ────────────────────────────────────────

        public void JoinLobbyByCode(string code)
        {
            code = code.Trim().ToUpper();

            if (code.Length != CodeLength)
            {
                Debug.LogWarning("[LobbyManager] 코드는 6자리여야 합니다.");
                OnLobbyEntered?.Invoke(false);
                return;
            }

#if UNITY_EDITOR
            IsHost = false;
            Debug.Log($"[LobbyManager] 에디터 방 입장 | 코드: {code}");
            OnLobbyEntered?.Invoke(true);
#else
            _pendingCode = code;

            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameName, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(CodeKey, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);

            var handle = SteamMatchmaking.RequestLobbyList();
            _lobbyMatchList.Set(handle);
#endif
        }

        private void OnLobbyMatchList(LobbyMatchList_t result, bool failure)
        {
            if (failure || result.m_nLobbiesMatching == 0)
            {
                Debug.LogWarning($"[LobbyManager] 코드 [{_pendingCode}] 에 해당하는 방을 찾지 못했습니다.");
                _pendingCode = null;
                OnLobbyEntered?.Invoke(false);
                return;
            }

            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(0);
            SteamMatchmaking.JoinLobby(lobbyId);
            _pendingCode = null;
        }

        // Steam 친구 초대를 통한 참가
        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t result)
        {
            SteamMatchmaking.JoinLobby(result.m_steamIDLobby);
        }

        private void OnLobbyEnteredCallback(LobbyEnter_t result)
        {
            bool success = result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;

            if (!success)
            {
                Debug.LogError($"[LobbyManager] 방 입장 실패: {result.m_EChatRoomEnterResponse}");
                OnLobbyEntered?.Invoke(false);
                return;
            }

            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);

            if (!IsHost)
                CurrentRoomCode = SteamMatchmaking.GetLobbyData(CurrentLobbyId, CodeKey);

            Debug.Log($"[LobbyManager] 방 입장 완료 | 코드: {CurrentRoomCode}");
            OnLobbyEntered?.Invoke(true);
        }

        // ── 멤버 변경 ──────────────────────────────────────────

        private void OnLobbyChatUpdate(LobbyChatUpdate_t result)
        {
            OnMembersChanged?.Invoke();
        }

        // ── 방 나가기 ──────────────────────────────────────────

        public void LeaveLobby()
        {
            if (!CurrentLobbyId.IsValid()) return;

            SteamMatchmaking.LeaveLobby(CurrentLobbyId);
            CurrentLobbyId = CSteamID.Nil;
            CurrentRoomCode = null;
            IsHost = false;

            Debug.Log("[LobbyManager] 방 나감");
        }

        // ── 유틸 ───────────────────────────────────────────────

        public int GetMemberCount() =>
            CurrentLobbyId.IsValid() ? SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId) : 0;

        public CSteamID GetMember(int index) =>
            SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyId, index);

        private static string GenerateCode()
        {
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
                chars[i] = CodeChars[UnityEngine.Random.Range(0, CodeChars.Length)];
            return new string(chars);
        }
    }
}
