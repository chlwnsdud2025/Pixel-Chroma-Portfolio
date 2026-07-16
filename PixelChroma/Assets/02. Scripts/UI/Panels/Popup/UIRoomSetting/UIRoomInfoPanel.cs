using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace PixelChroma.UI
{
    // 방 이름 / 닉네임 / 공개 여부 / 게임 모드를 담당한다.
    // RoomOption의 소유권은 UIRoomSetting에 있고, 여기서는 Bind로 받은 참조에 값만 반영한다.
    public class UIRoomInfoPanel : MonoBehaviour
    {
        [Header("Room Name")]
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TMP_Text roomNameStatusText;
        [SerializeField] private TMP_Text roomNameCountText;

        [Header("Nickname")]
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private TMP_Text nicknameStatusText;
        [SerializeField] private TMP_Text nicknameCountText;

        [Header("Banned Words (비속어 필터링)")]
        [SerializeField] private BannedWordsSO bannedWords;

        [Header("Public / Private")]
        [SerializeField] private UIFocusButton publicButton;
        [SerializeField] private UIFocusButton privateButton;

        [Header("Game Mode")]
        [SerializeField] private UIFocusButton basicModeButton;
        [SerializeField] private UIFocusButton infectionModeButton;
        [SerializeField] private GameModeRegistrySO gameModeRegistry;
        [SerializeField] private TMP_Text gameModeDescriptionText;

        [Header("Localization")]
        [SerializeField] private LocalizedString warningMaxLengthLocalized;
        [SerializeField] private LocalizedString warningBannedLocalized;

        private RoomOption _option;

        public void Initialize()
        {
            if (roomNameInput != null)
            {
                roomNameInput.characterLimit = RoomOption.MaxRoomNameLength;
                roomNameInput.onValueChanged.AddListener(OnRoomNameChanged);
                roomNameInput.onEndEdit.AddListener(OnRoomNameInput);
            }

            if (nicknameInput != null)
            {
                nicknameInput.characterLimit = NicknameService.MaxLength;
                nicknameInput.onValueChanged.AddListener(OnNicknameChanged);
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

        // 방 이름/닉네임을 검증해 option과 PlayerPrefs에 반영한다. 실패 시 경고를 띄우고 false.
        public bool TryApply()
        {
            if (_option == null) return false;

            if (nicknameInput != null)
            {
                string nickname = nicknameInput.text.Trim();
                var result = NameValidator.Validate(nickname, bannedWords, NicknameService.MaxLength);

                if (result != NameValidation.Valid)
                {
                    SetStatusMessage(nicknameStatusText, GetWarningMessage(result, NicknameService.MaxLength));
                    return false;
                }

                // 비워두면 스팀 이름(스팀 미사용 시 임의 이름)을 대신 쓴다.
                if (string.IsNullOrEmpty(nickname))
                    nickname = NicknameService.GetDefaultSteamName();

                // CreateLobby가 PlayerPrefs에서 hostName을 읽어가므로 방 생성보다 먼저 저장해야 한다.
                NicknameService.Save(nickname);
            }

            if (roomNameInput != null)
            {
                string roomName = roomNameInput.text.Trim();
                var result = NameValidator.Validate(roomName, bannedWords, RoomOption.MaxRoomNameLength);

                if (result != NameValidation.Valid)
                {
                    SetStatusMessage(roomNameStatusText, GetWarningMessage(result, RoomOption.MaxRoomNameLength));
                    return false;
                }

                // 빈 방 이름은 그대로 둔다. (RoomListEntry가 "{hostName}의 방"으로 대체)
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

        private void OnNicknameChanged(string text)
        {
            SetStatusMessage(nicknameStatusText, string.Empty);
            SetCountMessage(nicknameCountText, text.Length, NicknameService.MaxLength);
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
            // SetTextWithoutNotify는 onValueChanged를 태우지 않으므로 글자 수는 여기서 직접 맞춘다.
            if (roomNameInput != null)
            {
                string roomName = _option.roomName ?? "";
                roomNameInput.SetTextWithoutNotify(roomName);
                SetCountMessage(roomNameCountText, roomName.Length, RoomOption.MaxRoomNameLength);
            }

            if (nicknameInput != null)
            {
                string nickname = NicknameService.LoadOrDefault();
                nicknameInput.SetTextWithoutNotify(nickname);
                SetCountMessage(nicknameCountText, nickname.Length, NicknameService.MaxLength);
            }

            SetStatusMessage(roomNameStatusText, string.Empty);
            SetStatusMessage(nicknameStatusText, string.Empty);
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

        private string GetWarningMessage(NameValidation result, int maxLength)
        {
            return result switch
            {
                NameValidation.TooLong => warningMaxLengthLocalized.GetLocalizedString(maxLength),
                NameValidation.Banned => warningBannedLocalized.GetLocalizedString(),
                _ => string.Empty,
            };
        }

        private static void SetStatusMessage(TMP_Text target, string message)
        {
            if (target != null)
                target.text = message;
        }

        private static void SetCountMessage(TMP_Text target, int length, int maxLength)
        {
            if (target != null)
                target.text = $"{length}/{maxLength}";
        }
    }
}
