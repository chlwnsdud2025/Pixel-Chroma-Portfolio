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

            // 💡 [수정] 이벤트 명과 헷갈리지 않도록 함수 명을 Handle~ 로 변경하여 안전하게 구독합니다.
            ChromaNetworkManager.OnCountdownTick += HandleCountdownTick;
            ChromaNetworkManager.OnCountdownEnd += HandleCountdownEnd;

            if (timerText != null)
                timerText.gameObject.SetActive(false);

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(NetworkServer.active);
                startGameButton.interactable = false;
            }

            if (inviteFriendButton != null)
                inviteFriendButton.gameObject.SetActive(NetworkServer.active);
        }

        private void OnDisable()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnMembersChanged -= RefreshMemberList;

            LobbyPlayerState.OnLobbyPlayersChanged -= RefreshReadyUI;

            // 💡 [수정] 구독 해제
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
                manager?.StartGame();
            }
        }

        // ── 카운트다운 ────────────────────────────────────────────

        private void HandleCountdownTick(int remainingSeconds)
        {
            if (timerText == null) return;

            timerText.gameObject.SetActive(true);
            // 텍스트를 조금 더 친절하게 표시하도록 수정할 수 있습니다.
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