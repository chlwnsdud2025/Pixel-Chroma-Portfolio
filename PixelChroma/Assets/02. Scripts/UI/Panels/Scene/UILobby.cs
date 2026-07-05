using System.Text;
using System.Linq;
using Cysharp.Threading.Tasks;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

namespace PixelChroma.UI
{
    public class UILobby : UIScene, IPaintHUD
    {
        [Header("Room Info")]
        [SerializeField] private TMP_Text roomTitleText;
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text memberListText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button leaveButton;

        [SerializeField] private Button roomSettingsButton;

        [Header("Show Code")]
        [SerializeField] private Button showCodeButton;
        [SerializeField] private Image showCodeIcon;
        [SerializeField] private Sprite shownSprite;
        [SerializeField] private Sprite hiddenSprite;
        [SerializeField] private TMP_Text showCodeLabel;

        // 기본값은 코드가 가려진(***) 상태입니다.
        private bool _isCodeVisible;

        [Header("Ready & Start")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private TMP_Text readyCountText;

        [Header("Countdown")]
        [SerializeField] private TMP_Text timerText;

        [Header("친구 초대")]
        [SerializeField] private Button inviteFriendButton;

        [HideInInspector] public LobbyPlayerState localLobbyPlayer;

        [Header("Customize")]
        [SerializeField] private GameObject paintModeRoot;
        [SerializeField] private UIColorPicker colorPicker;

        public UIColorPicker ColorPicker => colorPicker;

        public override void Initialize()
        {
            base.Initialize();
            if (copyCodeButton != null) copyCodeButton.onClick.AddListener(OnClickCopyCode);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnClickLeave);
            if (readyButton != null) readyButton.onClick.AddListener(OnClickReady);
            if (startGameButton != null) startGameButton.onClick.AddListener(OnClickStartGame);
            if (inviteFriendButton != null) inviteFriendButton.onClick.AddListener(OnClickInviteFriend);

            if (roomSettingsButton != null) roomSettingsButton.onClick.AddListener(OnClickRoomSettings);

            if (showCodeButton != null) showCodeButton.onClick.AddListener(ToggleCodeVisibility);

            if (paintModeRoot != null) paintModeRoot.SetActive(false);
        }

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null)
                paintModeRoot.SetActive(active);
        }

        private void OnEnable()
        {
            LobbyManager.Instance.OnMembersChanged += RefreshMemberList;
            LobbyPlayerState.OnLobbyPlayersChanged += RefreshReadyUI;

            // 누군가 레디(Ready)를 누르면 명단 텍스트(체크마크)도 즉각 갱신하도록 이벤트 추가
            LobbyPlayerState.OnLobbyPlayersChanged += RefreshMemberList;

            ChromaNetworkManager.OnCountdownTick += HandleCountdownTick;
            ChromaNetworkManager.OnCountdownEnd += HandleCountdownEnd;

            if (timerText != null)
                timerText.gameObject.SetActive(false);

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(NetworkServer.active);
                startGameButton.interactable = false;
            }

            if (roomSettingsButton != null)
                roomSettingsButton.gameObject.SetActive(NetworkServer.active);

            if (inviteFriendButton != null)
                inviteFriendButton.gameObject.SetActive(NetworkServer.active);

            // 네트워크 닉네임(SyncVar)이 클라이언트에 도착하는 미세한 핑 딜레이를 잡기 위해, 로비 창에서는 0.5초마다 UI를 안전하게 덧칠해줍니다.
            InvokeRepeating(nameof(RefreshMemberList), 0.5f, 0.5f);
        }

        private void OnDisable()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnMembersChanged -= RefreshMemberList;

            LobbyPlayerState.OnLobbyPlayersChanged -= RefreshReadyUI;
            LobbyPlayerState.OnLobbyPlayersChanged -= RefreshMemberList;

            ChromaNetworkManager.OnCountdownTick -= HandleCountdownTick;
            ChromaNetworkManager.OnCountdownEnd -= HandleCountdownEnd;

            CancelInvoke(nameof(RefreshMemberList)); // 루프 종료
        }

        protected override UniTask OnShow()
        {
            // 로비를 열 때마다 코드는 기본적으로 가려진(***) 상태로 표시합니다.
            _isCodeVisible = false;
            ApplyCodeVisibility();

            RefreshRoomCode();
            RefreshMemberList();
            RefreshReadyUI();

            return UniTask.CompletedTask;
        }

        // ── 방 설정 관리 (로비용) ──────────────────────────────────────────

        private void OnClickRoomSettings()
        {
            UIManager.Instance.ShowPopup<UIRoomSetting>().Forget();
        }

        // ── Ready UI ─────────────────────────────────────────

        private void RefreshReadyUI()
        {
            var allPlayers = LobbyPlayerState.AllPlayers;
            int totalPlayers = allPlayers.Count;
            int readyPlayers = allPlayers.Count(p => p.isReady);

            localLobbyPlayer = allPlayers.FirstOrDefault(p => p.isLocalPlayer);

            UpdateReadyCountText(readyPlayers, totalPlayers);
            if (totalPlayers == 0) return;

            bool isEveryoneReady = (readyPlayers == totalPlayers);

            if (NetworkServer.active && startGameButton != null)
                startGameButton.interactable = isEveryoneReady;
        }

        private void UpdateReadyCountText(int ready, int total)
        {
            if (readyCountText != null)
                readyCountText.text = $"{ready} / {total}";
        }

        // ── 버튼 핸들러 ──────────────────────────────────────

        private void OnClickReady()
        {
            if (localLobbyPlayer != null)
                localLobbyPlayer.CmdToggleReady();
        }

        private void OnClickStartGame()
        {
            if (NetworkServer.active)
            {
                ChromaNetworkManager manager = NetworkManager.singleton as ChromaNetworkManager;
                LobbyManager.Instance.ChangeLobbyStatusToInGame();
                manager?.StartGameWithOption(LobbyManager.Instance.CurrentRoomOption);
            }
        }

        // ── 카운트다운 ────────────────────────────────────────────

        private void HandleCountdownTick(int remainingSeconds)
        {
            if (timerText == null) return;

            timerText.gameObject.SetActive(true);
            timerText.text = $"게임 시작까지... {remainingSeconds}";
        }

        private void HandleCountdownEnd()
        {
            if (timerText != null)
                timerText.gameObject.SetActive(false);
        }

        // ── 표시 ───────────────────────────────────────────────

        private void RefreshRoomCode()
        {
            if (roomCodeText == null) return;

            string code = LobbyManager.Instance.CurrentRoomCode;
            string display = _isCodeVisible ? code : MaskCode(code);
            roomCodeText.text = display;
        }

        // ── 코드 표시/숨김 토글 ─────────────────────────────────

        private void ToggleCodeVisibility()
        {
            _isCodeVisible = !_isCodeVisible;
            ApplyCodeVisibility();
            RefreshRoomCode();
        }

        private void ApplyCodeVisibility()
        {
            var sprite = _isCodeVisible ? shownSprite : hiddenSprite;

            if (showCodeIcon != null && sprite != null)
            {
                showCodeIcon.sprite = sprite;
            }
            else if (showCodeLabel != null)
            {
                showCodeLabel.text = _isCodeVisible ? "Hide" : "Show";
            }
        }

        private static string MaskCode(string code)
        {
            return string.IsNullOrEmpty(code) ? string.Empty : new string('*', code.Length);
        }

        // 스팀 API가 아닌 Mirror 네트워크 객체에서 커스텀 닉네임을 가져옵니다.
        private void RefreshMemberList()
        {
            RefreshPlayerCount();

            if (memberListText == null) return;

            var sb = new StringBuilder();

            // 네트워크에 스폰된 로비 플레이어들을 netId 순(접속 순서)으로 정렬합니다.
            var sortedPlayers = LobbyPlayerState.AllPlayers.OrderBy(p => p.netId).ToList();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var p = sortedPlayers[i];
                var playerBase = p.GetComponent<PlayerBase>();

                // PlayerBase(공통 네트워크)에서 동기화된 커스텀 닉네임을 가져옵니다.
                string name = playerBase != null ? playerBase.playerName : "Player";

               

                // 가장 처음 스폰된 사람(netId가 제일 낮은 사람)이 방장입니다.
                bool isHost = (i == 0);
                sb.AppendLine(isHost ? $"{name} (방장)" : name);
            }

            // 만약 서버 딜레이로 미러(Mirror) 객체가 아직 없다면 스팀 API 이름으로 임시 렌더링
            if (sortedPlayers.Count == 0 && LobbyManager.Instance != null)
            {
                int count = LobbyManager.Instance.GetMemberCount();
                for (int i = 0; i < count; i++)
                {
                    CSteamID memberId = LobbyManager.Instance.GetMember(i);
                    string name = SteamFriends.GetFriendPersonaName(memberId);
                    bool isHost = (i == 0);
                    sb.AppendLine(isHost ? $"{name} (방장)" : name);
                }
            }

            memberListText.text = sb.ToString();
        }

        // 현재 입장 인원 / 최대 인원 (예: 3 / 10)
        private void RefreshPlayerCount()
        {
            if (playerCountText == null) return;

            int current = LobbyManager.Instance.GetMemberCount();
            int max = LobbyManager.Instance.CurrentRoomOption != null
                ? LobbyManager.Instance.CurrentRoomOption.maxPlayers
                : RoomOption.DefaultMaxPlayers;

            playerCountText.text = $"{current} / {max}";
        }

        // ── 유틸 버튼 ────────────────────────────────────────

        private void OnClickCopyCode()
        {
            GUIUtility.systemCopyBuffer = LobbyManager.Instance.CurrentRoomCode;
            Debug.Log("방 코드가 클립보드에 복사되었습니다.");
        }

        private void OnClickLeave()
        {
            LobbyManager.Instance.LeaveLobby();
            GameManager.Instance.EnterMainMenu().Forget();
        }

        // ── 친구 초대 ─────────────────────────────────────────
        private void OnClickInviteFriend()
        {
#if UNITY_EDITOR
            Debug.LogWarning("[UILobby] 에디터에서는 스팀 오버레이 초대가 작동하지 않습니다.");
#else            
            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyId.IsValid())
            {
                Debug.Log("[UILobby] 스팀 친구 초대 오버레이 열림");
                SteamFriends.ActivateGameOverlayInviteDialog(LobbyManager.Instance.CurrentLobbyId);
            }
            else
            {
                Debug.LogWarning("[UILobby] 유효한 스팀 로비 ID가 없어 초대 창을 띄울 수 없습니다.");
            }
#endif
        }
    }
}