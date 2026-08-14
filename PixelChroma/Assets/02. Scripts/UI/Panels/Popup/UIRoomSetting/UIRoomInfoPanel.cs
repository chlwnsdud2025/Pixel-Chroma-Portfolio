using OneText.UGUI;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 방 이름 / 인원 수(최대 인원·술래) / 공개 여부 / 게임 모드 를 담당한다.
    // RoomOption의 소유권은 UIRoomSetting에 있고, 여기서는 Bind로 받은 참조에 값만 반영한다.
    public class UIRoomInfoPanel : MonoBehaviour
    {
        [Header("Room Name")]
        [SerializeField] private OneTextInputField roomNameInput;
        [SerializeField] private OneTextLabel roomNameStatusText;
        [SerializeField] private OneTextLabel roomNameCountText;

        [Header("Max Players")]
        [SerializeField] private Button maxPlayersMinusButton;
        [SerializeField] private Button maxPlayersPlusButton;
        [SerializeField] private OneTextInputField maxPlayersInput;

        [Header("Seeker Count")]
        [SerializeField] private Button seekerCountMinusButton;
        [SerializeField] private Button seekerCountPlusButton;
        [SerializeField] private OneTextInputField seekerCountInput;

        [Header("Public / Private")]
        [SerializeField] private Button publicToggleButton;
        [SerializeField] private GameObject publicObject;
        [SerializeField] private GameObject privateObject;

        [Header("Game Mode")]
        [SerializeField] private UIGameModeSelector gameModeSelector;

        [Header("Banned Words (비속어 필터링)")]
        [SerializeField] private BannedWordsSO bannedWords;

        [Header("Localization")]
        [SerializeField] private RoomSettingTextDataSO textData;

        private RoomOption _option;

        // ── Public ──

        public void Initialize()
        {
            // 방 이름 InputField 이벤트 등록
            if (roomNameInput != null)
            {
                roomNameInput.characterLimit = RoomOption.MaxRoomNameLength;
                roomNameInput.onValueChanged.AddListener(OnRoomNameChanged);
                roomNameInput.onEndEdit.AddListener(OnRoomNameInput);
            }

            // 인원 수(최대 인원·술래) 이벤트 등록
            if (maxPlayersMinusButton != null) maxPlayersMinusButton.onClick.AddListener(() => AdjustMaxPlayers(-1));
            if (maxPlayersPlusButton != null) maxPlayersPlusButton.onClick.AddListener(() => AdjustMaxPlayers(1));
            if (maxPlayersInput != null) maxPlayersInput.onEndEdit.AddListener(OnMaxPlayersInput);

            if (seekerCountMinusButton != null) seekerCountMinusButton.onClick.AddListener(() => AdjustSeekerCount(-1));
            if (seekerCountPlusButton != null) seekerCountPlusButton.onClick.AddListener(() => AdjustSeekerCount(1));
            if (seekerCountInput != null) seekerCountInput.onEndEdit.AddListener(OnSeekerCountInput);

            // 공개 여부 이벤트 등록
            if (publicToggleButton != null) publicToggleButton.AddClickListener(TogglePublic);

            // 게임 모드 선택기 초기화 (탭 생성 포함)
            if (gameModeSelector != null) gameModeSelector.Initialize();
        }

        public void Bind(RoomOption option)
        {
            _option = option;
            if (gameModeSelector != null) gameModeSelector.Bind(option);
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

        // ── Private ──

        private void RefreshAll()
        {
            if (_option == null) return;

            RefreshNameUI();
            RefreshPlayerCountUI();
            RefreshPublicUI();
        }

        // 방 이름
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

        // 인원 수 (최대 인원 / 술래)
        // 버튼(+/-)과 InputField 직접 입력이 동일한 clamp 규칙을 공유하도록 값 설정을 한곳에 모은다.
        private void SetMaxPlayers(int value)
        {
            _option.maxPlayers = Mathf.Clamp(
                value,
                RoomOption.MinPlayers,
                RoomOption.MaxPlayersLimit);

            int maxSeeker = _option.maxPlayers - 1;
            if (_option.seekerCount > maxSeeker)
                _option.seekerCount = Mathf.Max(RoomOption.MinSeekerCount, maxSeeker);

            RefreshPlayerCountUI();
        }

        private void SetSeekerCount(int value)
        {
            int maxSeeker = Mathf.Min(RoomOption.MaxSeekerCount, _option.maxPlayers - 1);
            _option.seekerCount = Mathf.Clamp(
                value,
                RoomOption.MinSeekerCount,
                maxSeeker);

            RefreshPlayerCountUI();
        }

        private void RefreshPlayerCountUI()
        {
            if (_option == null) return;
            if (maxPlayersInput != null) ForceSetText(maxPlayersInput, _option.maxPlayers.ToString());
            if (seekerCountInput != null) ForceSetText(seekerCountInput, _option.seekerCount.ToString());
        }

        // 공개 여부
        private void RefreshPublicUI()
        {
            if (publicObject != null) publicObject.SetActive(_option.isPublic);
            if (privateObject != null) privateObject.SetActive(!_option.isPublic);
        }

        // 공용 헬퍼
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

        private static void ForceSetText(OneTextInputField input, string value)
        {
            input.text = value;

            // 텍스트 오버플로우로 밀린 textComponent의 스크롤 오프셋을 원위치시킨다.
            if (input.textComponent != null)
            {
                var rt = input.textComponent.rectTransform;
                rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
            }
        }

        // ── 이벤트 바인딩 ──

        // 방 이름: 타이핑 중에는 경고를 지우고 글자 수만 갱신한다. (값 반영은 onEndEdit에서)
        private void OnRoomNameChanged(string text)
        {
            SetStatusMessage(roomNameStatusText, string.Empty);
            SetCountMessage(roomNameCountText, text.Length, RoomOption.MaxRoomNameLength);
        }

        private void OnRoomNameInput(string text)
        {
            if (_option == null) return;
            _option.roomName = text.Trim();
        }

        // 인원 수: +/- 버튼
        private void AdjustMaxPlayers(int delta)
        {
            if (_option == null) return;
            SetMaxPlayers(_option.maxPlayers + delta);
        }

        private void AdjustSeekerCount(int delta)
        {
            if (_option == null) return;
            SetSeekerCount(_option.seekerCount + delta);
        }

        // 인원 수: InputField 직접 입력 — 숫자면 clamp 후 반영, 아니면 이전 값으로 되돌린다(재표시).
        private void OnMaxPlayersInput(string text)
        {
            if (_option == null) return;
            if (int.TryParse(text, out int value)) SetMaxPlayers(value);
            else RefreshPlayerCountUI();
        }

        private void OnSeekerCountInput(string text)
        {
            if (_option == null) return;
            if (int.TryParse(text, out int value)) SetSeekerCount(value);
            else RefreshPlayerCountUI();
        }

        // 공개 여부: 클릭할 때마다 공개/비공개를 뒤집는다.
        private void TogglePublic()
        {
            if (_option == null) return;

            _option.isPublic = !_option.isPublic;
            RefreshPublicUI();
        }
    }
}
