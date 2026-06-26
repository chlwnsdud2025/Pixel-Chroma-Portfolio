using System.Text;
using Cysharp.Threading.Tasks;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UILobby : UIScene
    {
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text memberListText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button leaveButton;

        protected override void Awake()
        {
            base.Awake();
            copyCodeButton.onClick.AddListener(OnClickCopyCode);
            leaveButton.onClick.AddListener(OnClickLeave);
        }

        private void OnEnable()
        {
            LobbyManager.Instance.OnMembersChanged += RefreshMemberList;
        }

        private void OnDisable()
        {
            LobbyManager.Instance.OnMembersChanged -= RefreshMemberList;
        }

        protected override UniTask OnShow()
        {
            RefreshRoomCode();
            RefreshMemberList();
            return UniTask.CompletedTask;
        }

        // ── 표시 ───────────────────────────────────────────────

        private void RefreshRoomCode()
        {
            roomCodeText.text = $"방 코드: {LobbyManager.Instance.CurrentRoomCode}";
        }

        private void RefreshMemberList()
        {
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

        // ── 버튼 핸들러 ────────────────────────────────────────

        private void OnClickCopyCode()
        {
            GUIUtility.systemCopyBuffer = LobbyManager.Instance.CurrentRoomCode;
        }

        private void OnClickLeave()
        {
            LobbyManager.Instance.LeaveLobby();
            GameManager.Instance.EnterMainMenu().Forget();
        }
    }
}
