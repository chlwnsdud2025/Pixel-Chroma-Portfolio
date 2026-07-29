using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UILobby : UIScene, IPaintHUD
    {
        [Header("Room Info")]
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text playerCountText;

        [Header("Member List")]
        [SerializeField] private UIMemberSlot[] memberSlots;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button leaveButton;

        [SerializeField] private Button roomSettingsButton;

        [Header("Show Code")]
        [SerializeField] private Button showCodeButton;
        [SerializeField] private GameObject shownIcon;
        [SerializeField] private GameObject hiddenIcon;

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
        [SerializeField] private GameObject timerPanel;
        [SerializeField] private UIPunchText timerText;

        [Header("친구 초대")]
        [SerializeField] private Button inviteFriendButton;

        [Header("Room Option")]
        [SerializeField] private UIRoomOptionPanel roomOptionPanel;

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
            if (roomSettingsButton != null) roomSettingsButton.onClick.AddListener(OnClickRoomSettings);
            if (showCodeButton != null) showCodeButton.onClick.AddListener(OnClickToggleCode);

            if (colorPicker != null) colorPicker.Initialize();

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

            PlayerBase.OnAnyPlayerNameChanged += RefreshMemberList;
            GameRoundManager.OnTimeStateChanged += OnTimeStateChanged;
            GameRoundManager.OnTimerTick += OnTimerTick;
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

            PlayerBase.OnAnyPlayerNameChanged -= RefreshMemberList;
            GameRoundManager.OnTimeStateChanged -= OnTimeStateChanged;
            GameRoundManager.OnTimerTick -= OnTimerTick;
        }

        protected override UniTask OnShow()
        {
            if (timerPanel != null)
                timerPanel.SetActive(false);

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
            RefreshRoomOptionPanel();

            return UniTask.CompletedTask;
        }

        public override void OnCancel()
        {
            // TODO OnClickSetting
            Debug.Log("OnClickSetting");
        }

        // ── Public ───────────────────────────────────────────

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null)
                paintModeRoot.SetActive(active);

            if (roomOptionPanel != null)
                roomOptionPanel.gameObject.SetActive(!active);
        }

        // ── Private ──────────────────────────────────────────

        private void RefreshRoomTitle()
        {
            if (roomNameText == null)
                return;

            var option = LobbyManager.Instance?.CurrentRoomOption;
            roomNameText.text = option?.roomName ?? "";
        }

        private void RefreshRoomCode()
        {
            if (roomCodeText == null)
                return;

            string code = LobbyManager.Instance.CurrentRoomCode;
            string display = _isCodeVisible ? code : MaskCode(code);
            roomCodeText.text = display;
        }

        private void RefreshMemberList()
        {
            if (memberSlots == null || memberSlots.Length == 0) return;

            var sortedPlayers = LobbyPlayerState.AllPlayers.OrderBy(p => p.netId).ToList();
            bool isLocalHost = LobbyManager.Instance.IsHost;

            for (int i = 0; i < memberSlots.Length; i++)
            {
                bool active = i < sortedPlayers.Count;
                memberSlots[i].gameObject.SetActive(active);
                if (active)
                    memberSlots[i].Set(sortedPlayers[i], isHost: i == 0, isLocalHost);
            }
        }

        private void RefreshPlayerCount()
        {
            if (playerCountText == null || LobbyManager.Instance == null)
                return;
            
            int current = LobbyPlayerState.AllPlayers.Count;

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

        private void RefreshRoomOptionPanel()
        {
            if (roomOptionPanel != null)
                roomOptionPanel.Refresh(LobbyManager.Instance?.CurrentRoomOption);
        }

        private void ApplyCodeVisibility()
        {
            if (shownIcon != null) shownIcon.SetActive(_isCodeVisible);
            if (hiddenIcon != null) hiddenIcon.SetActive(!_isCodeVisible);
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
            RefreshMemberList();
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
            RefreshRoomOptionPanel();
        }

        private void OnTimeStateChanged(TimeState state, int remainingSeconds)
        {
            bool isCountdown = state == TimeState.Countdown;

            if (timerPanel != null)
                timerPanel.SetActive(isCountdown);

            if (readyButton != null)
                readyButton.interactable = !isCountdown;
        }

        private void OnTimerTick(int remainingSeconds)
        {
            if (GameRoundManager.Instance.CurrentTimeState != TimeState.Countdown) return;

            if (timerText != null)
                timerText.SetText(Mathf.Max(0, remainingSeconds).ToString());
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
    }
}
