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
    public class UILobby : UIScene
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

        [Header("친구 초대")]
        [SerializeField] private Button inviteFriendButton; // ← Unity Inspector에서 버튼 연결

        [HideInInspector] public LobbyPlayerState localLobbyPlayer;

        [Header("Customize")]
        [SerializeField] private UIColorPicker colorPicker;

        public override void Initialize()
        {
            base.Initialize();
            if (copyCodeButton != null) copyCodeButton.onClick.AddListener(OnClickCopyCode);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnClickLeave);
            if (readyButton != null) readyButton.onClick.AddListener(OnClickReady);
            if (startGameButton != null) startGameButton.onClick.AddListener(OnClickStartGame);
            if (inviteFriendButton != null) inviteFriendButton.onClick.AddListener(OnClickInviteFriend);
        }

        private void OnEnable()
        {
            LobbyManager.Instance.OnMembersChanged += RefreshMemberList;
            LobbyPlayerState.OnLobbyPlayersChanged += RefreshReadyUI;

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(NetworkServer.active);
                startGameButton.interactable = false;
            }

            // 초대 버튼은 호스트만 보이게 설정 (선택사항 — 모두 보이게 하려면 이 블록 제거)
            if (inviteFriendButton != null)
                inviteFriendButton.gameObject.SetActive(NetworkServer.active);


        }

        private void OnDisable()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnMembersChanged -= RefreshMemberList;

            LobbyPlayerState.OnLobbyPlayersChanged -= RefreshReadyUI;
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
            // 1. 현재 우리가 속해있는 방(로비)의 ID가 정상적인지 확인합니다.
            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyId.IsValid())
            {
                Debug.Log("[UILobby] 스팀 친구 초대 오버레이 열림");
                
                // [핵심] 스팀 오버레이의 '친구 초대 다이얼로그'를 강제로 화면에 띄웁니다!
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
