using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UITitle : UIScene
    {
        [Header("Main Menu Buttons & Inputs")]
        [SerializeField] private Button openRoomSettingsButton;
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_Text statusText;

        public override void Initialize()
        {
            base.Initialize();

            if (openRoomSettingsButton != null) openRoomSettingsButton.onClick.AddListener(OnClickOpenSettings);
            if (quickJoinButton != null) quickJoinButton.onClick.AddListener(OnClickQuickJoin);
            if (joinButton != null) joinButton.onClick.AddListener(OnClickJoin);

            roomCodeInput.characterLimit = 6;
            roomCodeInput.onValueChanged.AddListener(v =>
                roomCodeInput.SetTextWithoutNotify(v.ToUpper()));

            SetStatus("");
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();
            SetStatus("");
            SetInteractable(true);
        }

        private void OnEnable()
        {
            LobbyManager.Instance.OnLobbyEntered += HandleLobbyEntered;
        }

        private void OnDisable()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyEntered -= HandleLobbyEntered;
        }

        private void OnClickOpenSettings()
        {
            ShowRoomSettingPopup().Forget();
        }

        private async UniTaskVoid ShowRoomSettingPopup()
        {
            var popup = await UIManager.Instance.ShowPopup<UIRoomSetting>();
            if (popup == null) return;

            popup.Setup(LobbyManager.Instance.CurrentRoomOption, false);
            popup.SetMaps(LobbyManager.Instance.AvailableMaps);

            popup.OnCreate -= OnRoomSettingCreate;
            popup.OnCreate += OnRoomSettingCreate;
        }

        private void OnRoomSettingCreate(RoomOption option)
        {
            var popup = UIManager.Instance.GetPanel<UIRoomSetting>();
            if (popup != null) popup.OnCreate -= OnRoomSettingCreate;

            SetInteractable(false);
            SetStatus("방 생성 중...");

            LobbyManager.Instance.CreateLobby(option);
            UIManager.Instance.HidePopup().Forget();
        }

        private void OnClickQuickJoin()
        {
            SetInteractable(false);
            SetStatus("공개방 찾는 중...");
            LobbyManager.Instance.JoinQuickMatch();
        }

        private void OnClickJoin()
        {
            string code = roomCodeInput.text.Trim();

            if (code.Length != 6)
            {
                SetStatus("6자리 코드를 입력해주세요.");
                return;
            }

            SetInteractable(false);
            SetStatus("방 입장 중...");
            LobbyManager.Instance.JoinLobbyByCode(code);
        }

        private void HandleLobbyEntered(bool success)
        {
            if (success)
            {
                GameManager.Instance.EnterLobby().Forget();
                return;
            }

            SetStatus("방을 찾을 수 없거나 생성에 실패했습니다.");
            SetInteractable(true);
        }

        private void SetInteractable(bool value)
        {
            if (openRoomSettingsButton != null) openRoomSettingsButton.interactable = value;
            if (quickJoinButton != null) quickJoinButton.interactable = value;
            if (joinButton != null) joinButton.interactable = value;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
