using System.Text;
using System.Linq;
using Cysharp.Threading.Tasks;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Start (Host)")]
        [SerializeField] private GameObject startButtonPanel;
        [SerializeField] private Button startGameButton;
        [SerializeField] private TMP_Text startReadyCountText;

        [Header("Ready (Client)")]
        [SerializeField] private GameObject readyButtonPanel;
        [SerializeField] private Button readyButton;
        [SerializeField] private Image readyCheckImage;
        [SerializeField] private TMP_Text readyCountText;

        [Header("Countdown")]
        [SerializeField] private TMP_Text timerText;

        [Header("친구 초대")]
        [SerializeField] private Button inviteFriendButton;

        [Header("Customize")]
        [SerializeField] private GameObject paintModeRoot;
        [SerializeField] private UIColorPicker colorPicker;

        [HideInInspector] public LobbyPlayerState localLobbyPlayer;

        private bool _isCodeVisible;

        // ── Override ─────────────────────────────────────────

        public UIColorPicker ColorPicker => colorPicker;

        public override void Initialize()
        {
            base.Initialize();
            if (copyCodeButton != null) copyCodeButton.onClick.AddListener(OnClickCopyCode);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnClickLeave);
            if (startGameButton != null) startGameButton.onClick.AddListener(OnClickStartGame);
            if (readyButton != null) readyButton.onClick.AddListener(OnClickReady);
            if (inviteFriendButton != null) inviteFriendButton.onClick.AddListener(OnClickInviteFriend);
            if (roomSettingsButton != null) roomSettingsButton.onClick.AddListener(OnClickRoomSettings);
            if (showCodeButton != null) showCodeButton.onClick.AddListener(OnClickToggleCode);

            if (paintModeRoot != null) paintModeRoot.SetActive(false);
        }

        /// <summary>
        /// 패널이 켜지기 직전에 호출. GameObject 활성화 전에 이벤트를 구독한다.
        /// </summary>
        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            LobbyPlayerState.OnLobbyPlayersChanged += OnLobbyPlayersChanged;
            LobbyPlayerState.OnReadyChanged += OnReadyChanged;
            LobbyPlayerState.OnRoomOptionSynced += OnRoomOptionSynced;

            GameRoundManager.OnCountdownTick += OnCountdownTick;
            GameRoundManager.OnCountdownEnd += OnCountdownEnd;
        }

        /// <summary>
        /// 패널이 완전히 내려간 뒤 호출. OnBeforeShow와 대칭으로 구독을 해제한다.
        /// </summary>
        public override void OnAfterHide()
        {
            base.OnAfterHide();

            LobbyPlayerState.OnLobbyPlayersChanged -= OnLobbyPlayersChanged;
            LobbyPlayerState.OnReadyChanged -= OnReadyChanged;
            LobbyPlayerState.OnRoomOptionSynced -= OnRoomOptionSynced;

            GameRoundManager.OnCountdownTick -= OnCountdownTick;
            GameRoundManager.OnCountdownEnd -= OnCountdownEnd;
        }

        protected override UniTask OnShow()
        {
            if (timerText != null)
                timerText.gameObject.SetActive(false);

            bool isHost = LobbyManager.Instance.IsHost;

            if (startButtonPanel != null) startButtonPanel.SetActive(isHost);
            if (readyButtonPanel != null) readyButtonPanel.SetActive(!isHost);

            if (isHost && startGameButton != null)
                startGameButton.interactable = false;

            if (roomSettingsButton != null)
                roomSettingsButton.gameObject.SetActive(isHost);

            if (inviteFriendButton != null)
                inviteFriendButton.gameObject.SetActive(isHost);

            _isCodeVisible = false;
            ApplyCodeVisibility();

            RefreshRoomTitle();
            RefreshRoomCode();
            RefreshMemberList();
            RefreshPlayerCount();
            RefreshReadyUI();

            return UniTask.CompletedTask;
        }

        // ── Public ───────────────────────────────────────────

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null)
                paintModeRoot.SetActive(active);
        }

        // ── Private ──────────────────────────────────────────

        private void RefreshRoomTitle()
        {
            if (roomTitleText == null)
                return;

            string hostName = LobbyManager.Instance?.CurrentRoomOption?.hostName ?? "Player";
            roomTitleText.text = $"{hostName}의 방";
        }

        private void RefreshRoomCode()
        {
            if (roomCodeText == null) 
                return;

            string code = LobbyManager.Instance.CurrentRoomCode;
            string display = _isCodeVisible ? code : MaskCode(code);
            roomCodeText.text = display;
        }

        /// <summary>
        /// Mirror 네트워크 객체에서 동기화된 닉네임으로 멤버 목록을 구성한다.
        /// </summary>
        private void RefreshMemberList()
        {
            var sortedPlayers = LobbyPlayerState.AllPlayers.OrderBy(p => p.netId).ToList();

            if (memberListText == null) return;

            var sb = new StringBuilder();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var p = sortedPlayers[i];
                var playerBase = p.GetComponent<PlayerBase>();
                string name = playerBase != null ? playerBase.playerName : "Player";
                bool isHost = (i == 0);
                sb.AppendLine(isHost ? $"{name} (방장)" : name);
            }

            memberListText.text = sb.ToString();
        }

        private void RefreshPlayerCount()
        {
            if (playerCountText == null || LobbyManager.Instance == null)
                return;

            int current = LobbyManager.Instance.GetMemberCount();
            if (current == 0)
                current = LobbyPlayerState.AllPlayers.Count;

            int max = LobbyManager.Instance.CurrentRoomOption != null
                ? LobbyManager.Instance.CurrentRoomOption.maxPlayers
                : RoomOption.DefaultMaxPlayers;

            playerCountText.text = $"{current}/{max}";
        }

        private void RefreshReadyUI()
        {
            var allPlayers = LobbyPlayerState.AllPlayers;
            int totalPlayers = allPlayers.Count;
            int readyPlayers = allPlayers.Count(p => p.isReady);

            localLobbyPlayer = allPlayers.FirstOrDefault(p => p.isLocalPlayer);

            string countDisplay = $"{readyPlayers}/{totalPlayers}";

            if (readyCountText != null)
                readyCountText.text = countDisplay;
            if (startReadyCountText != null)
                startReadyCountText.text = countDisplay;

            if (totalPlayers == 0) return;

            bool isEveryoneReady = (readyPlayers == totalPlayers);

            if (LobbyManager.Instance.IsHost && startGameButton != null)
                startGameButton.interactable = isEveryoneReady;

            if (!LobbyManager.Instance.IsHost && readyCheckImage != null)
            {
                bool isLocalReady = localLobbyPlayer != null && localLobbyPlayer.isReady;
                readyCheckImage.gameObject.SetActive(isLocalReady);
            }
        }

        private void ApplyCodeVisibility()
        {
            var sprite = _isCodeVisible ? shownSprite : hiddenSprite;

            if (showCodeIcon != null && sprite != null)
                showCodeIcon.sprite = sprite;
            else if (showCodeLabel != null)
                showCodeLabel.text = _isCodeVisible ? "Hide" : "Show";
        }

        private static string MaskCode(string code)
        {
            return string.IsNullOrEmpty(code) ? string.Empty : new string('*', code.Length);
        }

        // ── 이벤트 바인딩 ────────────────────────────────────

        /// <summary>
        /// 플레이어 입장/퇴장 시 호출.
        /// </summary>
        private void OnLobbyPlayersChanged()
        {
            RefreshMemberList();
            RefreshPlayerCount();
            RefreshReadyUI();
        }

        /// <summary>
        /// 레디 상태 변경 시 호출.
        /// </summary>
        private void OnReadyChanged()
        {
            RefreshReadyUI();
        }

        /// <summary>
        /// 서버로부터 RoomOption이 동기화되었을 때 호출.
        /// </summary>
        private void OnRoomOptionSynced(RoomOption option)
        {
            RefreshRoomTitle();
            RefreshRoomCode();
            RefreshPlayerCount();
        }

        private void OnCountdownTick(int remainingSeconds)
        {
            if (timerText == null)
                return;

            timerText.gameObject.SetActive(true);
            timerText.text = $"게임 시작까지... {remainingSeconds}";
        }

        private void OnCountdownEnd()
        {
            if (timerText != null)
                timerText.gameObject.SetActive(false);
        }

        // ── 클릭 이벤트 ──────────────────────────────────────

        private void OnClickRoomSettings()
        {
            UIManager.Instance.ShowPopup<UIRoomSetting>().Forget();
        }

        private void OnClickReady()
        {
            if (localLobbyPlayer != null)
                localLobbyPlayer.CmdToggleReady();
        }

        private void OnClickStartGame()
        {
            if (LobbyManager.Instance.IsHost)
            {
                LobbyManager.Instance.ChangeLobbyStatusToInGame();
                GameRoundManager.Instance?.StartGameWithOption(LobbyManager.Instance.CurrentRoomOption);
            }
        }

<<<<<<< HEAD
=======
        // ── 카운트다운 ────────────────────────────────────────────

        private void HandleCountdownTick(int remainingSeconds)
        {
            if (timerText == null) return;

            timerText.gameObject.SetActive(true);
            timerText.text = $"게임 시작까지... {remainingSeconds}";

            // 카운트다운 시작 시 Ready 버튼 비활성화 처리 (트롤링 방지)
            if (readyButton != null)
                readyButton.interactable = false;
        }

        private void HandleCountdownEnd()
        {
            if (timerText != null)
                timerText.gameObject.SetActive(false);

            // 카운트다운이 끝나고 인게임 씬으로 안 가고 방이 유지될 때를 대비해 원상복구
            if (readyButton != null)
                readyButton.interactable = true;
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

>>>>>>> c4ca2978bf838cccb3052cc0f24110d37f544745
        private void OnClickCopyCode()
        {
            GUIUtility.systemCopyBuffer = LobbyManager.Instance.CurrentRoomCode;
        }

        private void OnClickLeave()
        {
            LobbyManager.Instance.LeaveLobby();
            GameManager.Instance.EnterMainMenu().Forget();
        }

        private void OnClickToggleCode()
        {
            _isCodeVisible = !_isCodeVisible;
            ApplyCodeVisibility();
            RefreshRoomCode();
        }

        private void OnClickInviteFriend()
        {
#if UNITY_EDITOR
            Debug.LogWarning("[UILobby] 에디터에서는 스팀 오버레이 초대가 작동하지 않습니다.");
#else
            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyId.IsValid())
            {
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
