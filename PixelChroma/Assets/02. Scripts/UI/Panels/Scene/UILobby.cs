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
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text memberListText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button leaveButton;

        [SerializeField] private Button roomSettingsButton;

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

            // 💡 설정 열기 버튼 이벤트 등록
            if (roomSettingsButton != null) roomSettingsButton.onClick.AddListener(OnClickRoomSettings);

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

            ChromaNetworkManager.OnCountdownTick += HandleCountdownTick;
            ChromaNetworkManager.OnCountdownEnd += HandleCountdownEnd;

            if (timerText != null)
                timerText.gameObject.SetActive(false);

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(NetworkServer.active);
                startGameButton.interactable = false;
            }

            // 💡 호스트(방장)에게만 방 설정 변경 버튼과 초대 버튼이 보이게 합니다.
            if (roomSettingsButton != null)
                roomSettingsButton.gameObject.SetActive(NetworkServer.active);

            if (inviteFriendButton != null)
                inviteFriendButton.gameObject.SetActive(NetworkServer.active);
        }

        private void OnDisable()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnMembersChanged -= RefreshMemberList;

            LobbyPlayerState.OnLobbyPlayersChanged -= RefreshReadyUI;

            ChromaNetworkManager.OnCountdownTick -= HandleCountdownTick;
            ChromaNetworkManager.OnCountdownEnd -= HandleCountdownEnd;
        }

        protected override UniTask OnShow()
        {
            RefreshRoomCode();
            RefreshMemberList();
            RefreshReadyUI();

            return UniTask.CompletedTask;
        }

        // ── 방 설정 관리 (로비용) ──────────────────────────────────────────

        private void OnClickRoomSettings()
        {
            ShowRoomSettingPopup().Forget();
        }

        private async UniTaskVoid ShowRoomSettingPopup()
        {
            var popup = await UIManager.Instance.ShowPopup<UIRoomSetting>();
            if (popup == null) return;

            popup.Setup(LobbyManager.Instance.CurrentRoomOption, true);
            popup.SetMaps(LobbyManager.Instance.AvailableMaps);

            popup.OnCreate -= OnRoomSettingUpdated;
            popup.OnCreate += OnRoomSettingUpdated;
        }

        private void OnRoomSettingUpdated(RoomOption option)
        {
            var popup = UIManager.Instance.GetPanel<UIRoomSetting>();
            if (popup != null) popup.OnCreate -= OnRoomSettingUpdated;

            LobbyManager.Instance.UpdateLobbySettings(option);

            UIManager.Instance.HidePopup().Forget();
            Debug.Log("[UILobby] 방 설정이 업데이트되었습니다!");
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

                // 수정한 방 설정 옵션을 인자로 넘겨 게임을 시작합니다!
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
            if (roomCodeText != null)
                roomCodeText.text = $"방 코드: {LobbyManager.Instance.CurrentRoomCode}";
        }

        private void RefreshMemberList()
        {
            if (memberListText == null) return;

            int count = LobbyManager.Instance.GetMemberCount();
            var sb = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                CSteamID memberId = LobbyManager.Instance.GetMember(i);
                string name = SteamFriends.GetFriendPersonaName(memberId);
                bool isHost = i == 0;
                sb.AppendLine(isHost ? $"{name} (방장)" : name);
            }

            memberListText.text = sb.ToString();
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