using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 인원 수 / 시간 설정을 담당한다.
    // RoomOption의 소유권은 UIRoomSetting에 있고, 여기서는 Bind로 받은 참조에 값만 반영한다.
    public class UIGameSettingPanel : MonoBehaviour
    {
        [Header("Max Players")]
        [SerializeField] private Button maxPlayersMinusButton;
        [SerializeField] private Button maxPlayersPlusButton;
        [SerializeField] private TMP_InputField maxPlayersInput;

        [Header("Seeker Count")]
        [SerializeField] private Button seekerCountMinusButton;
        [SerializeField] private Button seekerCountPlusButton;
        [SerializeField] private TMP_InputField seekerCountInput;

        [Header("Hide Time")]
        [SerializeField] private Slider hideTimeSlider;
        [SerializeField] private TMP_InputField hideTimeInput;

        [Header("Seek Time")]
        [SerializeField] private Slider seekTimeSlider;
        [SerializeField] private TMP_InputField seekTimeInput;

        [Header("Check Time")]
        [SerializeField] private Slider checkTimeSlider;
        [SerializeField] private TMP_InputField checkTimeInput;

        [Header("Forced Expose")]
        [SerializeField] private Button forcedExposeButton;            // on/off 토글
        [SerializeField] private Image forcedExposeCheckImage;         // on일 때만 표시되는 체크 이미지
        [SerializeField] private GameObject forcedExposeActivePanel;    // 실제 작동(슬라이더/입력) 패널
        [SerializeField] private GameObject forcedExposeDisabledPanel;  // 회색 비활성 표시용 패널
        [SerializeField] private Slider forcedExposeIntervalSlider;
        [SerializeField] private TMP_InputField forcedExposeIntervalInput;

        private RoomOption _option;

        public void Initialize()
        {
            if (maxPlayersMinusButton != null) maxPlayersMinusButton.onClick.AddListener(() => AdjustMaxPlayers(-1));
            if (maxPlayersPlusButton != null) maxPlayersPlusButton.onClick.AddListener(() => AdjustMaxPlayers(1));

            if (seekerCountMinusButton != null) seekerCountMinusButton.onClick.AddListener(() => AdjustSeekerCount(-1));
            if (seekerCountPlusButton != null) seekerCountPlusButton.onClick.AddListener(() => AdjustSeekerCount(1));

            if (maxPlayersInput != null) maxPlayersInput.onEndEdit.AddListener(OnMaxPlayersInput);
            if (seekerCountInput != null) seekerCountInput.onEndEdit.AddListener(OnSeekerCountInput);
            if (hideTimeInput != null) hideTimeInput.onEndEdit.AddListener(OnHideTimeInput);
            if (seekTimeInput != null) seekTimeInput.onEndEdit.AddListener(OnSeekTimeInput);
            if (checkTimeInput != null) checkTimeInput.onEndEdit.AddListener(OnCheckTimeInput);
            if (forcedExposeIntervalInput != null) forcedExposeIntervalInput.onEndEdit.AddListener(OnForcedExposeIntervalInput);
            if (forcedExposeButton != null) forcedExposeButton.onClick.AddListener(OnForcedExposeClicked);

            SetupSlider(hideTimeSlider, RoomOption.MinHideTime, RoomOption.MaxHideTime, OnHideTimeChanged);
            SetupSlider(seekTimeSlider, RoomOption.MinSeekTime, RoomOption.MaxSeekTime, OnSeekTimeChanged);
            SetupSlider(checkTimeSlider, RoomOption.MinCheckTime, RoomOption.MaxCheckTime, OnCheckTimeChanged);
            SetupSlider(forcedExposeIntervalSlider, RoomOption.MinForcedExposeInterval, RoomOption.MaxForcedExposeInterval, OnForcedExposeIntervalChanged);
        }

        public void Bind(RoomOption option)
        {
            _option = option;
            RefreshAll();
        }

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

        // 슬라이더와 InputField가 같은 clamp 규칙을 공유하도록 시간 값 설정을 한곳에 모은다.
        private void SetHideTime(float value)
        {
            _option.hideTime = Mathf.Clamp(Mathf.Round(value), RoomOption.MinHideTime, RoomOption.MaxHideTime);
            if (hideTimeSlider != null) hideTimeSlider.SetValueWithoutNotify(_option.hideTime);
            RefreshTimeUI();
        }

        private void SetSeekTime(float value)
        {
            _option.seekTime = Mathf.Clamp(Mathf.Round(value), RoomOption.MinSeekTime, RoomOption.MaxSeekTime);
            if (seekTimeSlider != null) seekTimeSlider.SetValueWithoutNotify(_option.seekTime);
            RefreshTimeUI();
        }

        private void SetCheckTime(float value)
        {
            _option.checkTime = Mathf.Clamp(Mathf.Round(value), RoomOption.MinCheckTime, RoomOption.MaxCheckTime);
            if (checkTimeSlider != null) checkTimeSlider.SetValueWithoutNotify(_option.checkTime);
            RefreshTimeUI();
        }

        private void SetForcedExposeInterval(float value)
        {
            _option.forcedExposeInterval = Mathf.Clamp(Mathf.Round(value), RoomOption.MinForcedExposeInterval, RoomOption.MaxForcedExposeInterval);
            if (forcedExposeIntervalSlider != null) forcedExposeIntervalSlider.SetValueWithoutNotify(_option.forcedExposeInterval);
            RefreshTimeUI();
        }

        private void OnHideTimeChanged(float value)
        {
            if (_option == null) return;
            _option.hideTime = value;
            RefreshTimeUI();
        }

        private void OnSeekTimeChanged(float value)
        {
            if (_option == null) return;
            _option.seekTime = value;
            RefreshTimeUI();
        }

        private void OnCheckTimeChanged(float value)
        {
            if (_option == null) return;
            _option.checkTime = value;
            RefreshTimeUI();
        }

        private void OnForcedExposeIntervalChanged(float value)
        {
            if (_option == null) return;
            _option.forcedExposeInterval = value;
            RefreshTimeUI();
        }

        // InputField 직접 입력 처리: 숫자면 clamp 후 반영, 아니면 이전 값으로 되돌린다(재표시).
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

        private void OnHideTimeInput(string text)
        {
            if (_option == null) return;
            if (float.TryParse(text, out float value)) SetHideTime(value);
            else RefreshTimeUI();
        }

        private void OnSeekTimeInput(string text)
        {
            if (_option == null) return;
            if (float.TryParse(text, out float value)) SetSeekTime(value);
            else RefreshTimeUI();
        }

        private void OnCheckTimeInput(string text)
        {
            if (_option == null) return;
            if (float.TryParse(text, out float value)) SetCheckTime(value);
            else RefreshTimeUI();
        }

        private void OnForcedExposeIntervalInput(string text)
        {
            if (_option == null) return;
            if (float.TryParse(text, out float value)) SetForcedExposeInterval(value);
            else RefreshTimeUI();
        }

        private void OnForcedExposeClicked()
        {
            if (_option == null) return;
            _option.forcedExposeEnabled = !_option.forcedExposeEnabled;  // 설정값 토글
            RefreshForcedExposeUI();                                     // 패널 스왑
        }

        private void RefreshForcedExposeUI()
        {
            if (_option == null) return;
            bool on = _option.forcedExposeEnabled;

            if (forcedExposeCheckImage != null) forcedExposeCheckImage.gameObject.SetActive(on);

            // on: 실제 패널 / off: 회색 비활성 패널
            if (forcedExposeActivePanel != null) forcedExposeActivePanel.SetActive(on);
            if (forcedExposeDisabledPanel != null) forcedExposeDisabledPanel.SetActive(!on);
        }

        private void RefreshAll()
        {
            if (_option == null) return;

            RefreshPlayerCountUI();
            RefreshTimeUI();

            if (hideTimeSlider != null) hideTimeSlider.SetValueWithoutNotify(_option.hideTime);
            if (seekTimeSlider != null) seekTimeSlider.SetValueWithoutNotify(_option.seekTime);
            if (checkTimeSlider != null) checkTimeSlider.SetValueWithoutNotify(_option.checkTime);
            if (forcedExposeIntervalSlider != null) forcedExposeIntervalSlider.SetValueWithoutNotify(_option.forcedExposeInterval);
            RefreshForcedExposeUI();
        }

        private void RefreshPlayerCountUI()
        {
            if (maxPlayersInput != null) ForceSetText(maxPlayersInput, _option.maxPlayers.ToString());
            if (seekerCountInput != null) ForceSetText(seekerCountInput, _option.seekerCount.ToString());
        }

        private void RefreshTimeUI()
        {
            if (hideTimeInput != null) ForceSetText(hideTimeInput, _option.hideTime.ToString("0"));
            if (seekTimeInput != null) ForceSetText(seekTimeInput, _option.seekTime.ToString("0"));
            if (checkTimeInput != null) ForceSetText(checkTimeInput, _option.checkTime.ToString("0"));
            if (forcedExposeIntervalInput != null) ForceSetText(forcedExposeIntervalInput, _option.forcedExposeInterval.ToString("0"));
        }

        private static void ForceSetText(TMP_InputField input, string value)
        {
            input.text = value;

            // 텍스트 오버플로우로 밀린 textComponent의 스크롤 오프셋을 원위치시킨다.
            if (input.textComponent != null)
            {
                var rt = input.textComponent.rectTransform;
                rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
            }
        }

        private static void SetupSlider(Slider slider, float min, float max, UnityEngine.Events.UnityAction<float> onChanged)
        {
            if (slider == null) return;

            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(onChanged);
        }
    }
}
