using OneText.UGUI;
using UnityEngine;
using UnityEngine.Localization;

namespace PixelChroma.UI
{
    // 방 이름 / 공개 여부 / 게임 모드를 담당한다.
    // RoomOption의 소유권은 UIRoomSetting에 있고, 여기서는 Bind로 받은 참조에 값만 반영한다.
    public class UIRoomInfoPanel : MonoBehaviour
    {
        [Header("Room Name")]
        [SerializeField] private OneTextInputField roomNameInput;
        [SerializeField] private OneTextLabel roomNameStatusText;
        [SerializeField] private OneTextLabel roomNameCountText;

        [Header("Banned Words (비속어 필터링)")]
        [SerializeField] private BannedWordsSO bannedWords;

        [Header("Public / Private")]
        [SerializeField] private UIFocusButton publicButton;
        [SerializeField] private UIFocusButton privateButton;

        [Header("Game Mode")]
        [SerializeField] private UIFocusButton basicModeButton;
        [SerializeField] private UIFocusButton infectionModeButton;
        [SerializeField] private GameModeRegistrySO gameModeRegistry;
        [SerializeField] private OneTextLabel gameModeDescriptionText;

        [Header("Localization")]
        [SerializeField] private RoomSettingTextDataSO textData;

        private RoomOption _option;

        public void Initialize()
        {
            if (roomNameInput != null)
            {
                roomNameInput.characterLimit = RoomOption.MaxRoomNameLength;
                roomNameInput.onValueChanged.AddListener(OnRoomNameChanged);
                roomNameInput.onEndEdit.AddListener(OnRoomNameInput);
            }

            if (publicButton != null) publicButton.OnClicked += () => SetPublic(true);
            if (privateButton != null) privateButton.OnClicked += () => SetPublic(false);

            if (basicModeButton != null) basicModeButton.OnClicked += () => SetGameMode(GameMode.Basic);
            if (infectionModeButton != null) infectionModeButton.OnClicked += () => SetGameMode(GameMode.Infection);
        }

        public void Bind(RoomOption option)
        {
            _option = option;
            RefreshAll();
        }

        // 방 이름을 검증해 option에 반영한다. 실패 시 경고를 띄우고 false.
        public bool TryApply()
        {
            if (_option == null) return false;

            if (roomNameInput != null)
            {
                string roomName = roomNameInput.text.Trim();
                var result = NicknameService.Validate(roomName, bannedWords, RoomOption.MaxRoomNameLength);

                if (result != NameValidation.Valid)
                {
                    SetStatusMessage(roomNameStatusText, GetWarningMessage(result, RoomOption.MaxRoomNameLength));
                    return false;
                }

                if (string.IsNullOrEmpty(roomName))
                    roomName = textData.defaultRoomName.GetLocalizedString(NicknameService.LoadOrDefault());
                _option.roomName = roomName;
            }

            return true;
        }

        private void OnRoomNameInput(string text)
        {
            if (_option == null) return;
            _option.roomName = text.Trim();
        }

        // 타이핑 중에는 경고를 지우고 글자 수만 갱신한다. (값 반영은 onEndEdit에서)
        private void OnRoomNameChanged(string text)
        {
            SetStatusMessage(roomNameStatusText, string.Empty);
            SetCountMessage(roomNameCountText, text.Length, RoomOption.MaxRoomNameLength);
        }

        private void SetPublic(bool isPublic)
        {
            if (_option == null) return;

            _option.isPublic = isPublic;
            RefreshPublicUI();
        }

        private void SetGameMode(GameMode mode)
        {
            if (_option == null) return;

            _option.gameMode = mode;
            RefreshGameModeUI();
        }

        private void RefreshAll()
        {
            if (_option == null) return;

            RefreshNameUI();
            RefreshPublicUI();
            RefreshGameModeUI();
        }

        private void RefreshNameUI()
        {
            if (roomNameInput != null)
            {
                string roomName = _option.roomName ?? "";
                roomNameInput.SetTextWithoutNotify(roomName);
                SetCountMessage(roomNameCountText, roomName.Length, RoomOption.MaxRoomNameLength);
            }

            UpdateRoomNamePlaceholder();
            SetStatusMessage(roomNameStatusText, string.Empty);
        }

        private void RefreshPublicUI()
        {
            if (publicButton != null) publicButton.SetSelected(_option.isPublic);
            if (privateButton != null) privateButton.SetSelected(!_option.isPublic);
        }

        private void RefreshGameModeUI()
        {
            if (basicModeButton != null) basicModeButton.SetSelected(_option.gameMode == GameMode.Basic);
            if (infectionModeButton != null) infectionModeButton.SetSelected(_option.gameMode == GameMode.Infection);

            if (gameModeDescriptionText == null) return;

            // SO나 description이 아직 안 채워진 모드는 설명을 비워둔다.
            var data = gameModeRegistry != null ? gameModeRegistry.Find(_option.gameMode) : null;
            gameModeDescriptionText.text = data != null && !data.description.IsEmpty
                ? data.description.GetLocalizedString()
                : string.Empty;
        }

        private void UpdateRoomNamePlaceholder()
        {
            if (roomNameInput == null || roomNameInput.placeholder is not OneTextLabel placeholderText) return;

            placeholderText.text = textData.defaultRoomName.GetLocalizedString(NicknameService.LoadOrDefault());
        }

        private string GetWarningMessage(NameValidation result, int maxLength)
        {
            return result switch
            {
                NameValidation.TooLong => textData.warningMaxLength.GetLocalizedString(maxLength),
                NameValidation.Banned => textData.warningBanned.GetLocalizedString(),
                _ => string.Empty,
            };
        }

        private static void SetStatusMessage(OneTextLabel target, string message)
        {
            if (target != null)
                target.text = message;
        }

        private static void SetCountMessage(OneTextLabel target, int length, int maxLength)
        {
            if (target != null)
                target.text = $"{length}/{maxLength}";
        }
    }
}
