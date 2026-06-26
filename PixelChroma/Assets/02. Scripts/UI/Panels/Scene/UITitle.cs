using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UITitle : UIScene
    {
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_Text statusText;

        protected override void Awake()
        {
            base.Awake();

            createRoomButton.onClick.AddListener(OnClickCreateRoom);
            quickJoinButton.onClick.AddListener(OnClickQuickJoin);
            joinButton.onClick.AddListener(OnClickJoin);

            // InputField는 영대문자 + 숫자만, 6자 제한
            roomCodeInput.characterLimit = 6;
            roomCodeInput.onValueChanged.AddListener(v =>
                roomCodeInput.SetTextWithoutNotify(v.ToUpper()));
        }

        private void OnEnable()
        {
            LobbyManager.Instance.OnLobbyEntered += HandleLobbyEntered;
        }

        private void OnDisable()
        {
            LobbyManager.Instance.OnLobbyEntered -= HandleLobbyEntered;
        }

        // ── 버튼 핸들러 ────────────────────────────────────────

        private void OnClickCreateRoom()
        {
            SetInteractable(false);
            SetStatus("Creating room...");
            LobbyManager.Instance.CreateLobby();
        }

        private void OnClickQuickJoin()
        {
            // TODO: Quick Join 구현
            SetStatus("Coming soon.");
        }

        private void OnClickJoin()
        {
            string code = roomCodeInput.text.Trim();

            if (code.Length != 6)
            {
                SetStatus("Please enter a 6-character code.");
                return;
            }

            SetInteractable(false);
            SetStatus("Joining room...");
            LobbyManager.Instance.JoinLobbyByCode(code);
        }

        // ── 콜백 ───────────────────────────────────────────────

        private void HandleLobbyEntered(bool success)
        {
            if (success)
            {
                GameManager.Instance.EnterLobby().Forget();
                return;
            }

            SetStatus("Room not found.");
            SetInteractable(true);
        }

        // ── 유틸 ───────────────────────────────────────────────

        private void SetInteractable(bool value)
        {
            createRoomButton.interactable = value;
            quickJoinButton.interactable = value;
            joinButton.interactable = value;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }
    }
}
