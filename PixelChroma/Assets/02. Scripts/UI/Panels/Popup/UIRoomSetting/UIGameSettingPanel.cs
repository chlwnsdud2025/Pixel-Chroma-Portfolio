using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 시간 / 강제 노출 설정을 담당한다. (인원 수는 UIRoomInfoPanel로 이동)
    // RoomOption의 소유권은 UIRoomSetting에 있고, 여기서는 Bind로 받은 참조에 값만 반영한다.
    public class UIGameSettingPanel : MonoBehaviour
    {
        [Header("Hide Time")]
        [SerializeField] private Slider hideTimeSlider;
        [SerializeField] private OneTextInputField hideTimeInput;

        [Header("Seek Time")]
        [SerializeField] private Slider seekTimeSlider;
        [SerializeField] private OneTextInputField seekTimeInput;

        [Header("Check Time")]
        [SerializeField] private Slider checkTimeSlider;
        [SerializeField] private OneTextInputField checkTimeInput;

        [Header("Forced Expose")]
        [SerializeField] private Toggle forcedExposeToggle;            // on/off 토글
        [SerializeField] private GameObject forcedExposeActivePanel;    // 실제 작동(슬라이더/입력) 패널
        [SerializeField] private GameObject forcedExposeDisabledPanel;  // 회색 비활성 표시용 패널
        [SerializeField] private Slider forcedExposeIntervalSlider;
        [SerializeField] private OneTextInputField forcedExposeIntervalInput;

        private RoomOption _option;

        public void Initialize()
        {
            if (hideTimeInput != null) hideTimeInput.onEndEdit.AddListener(OnHideTimeInput);
            if (seekTimeInput != null) seekTimeInput.onEndEdit.AddListener(OnSeekTimeInput);
            if (checkTimeInput != null) checkTimeInput.onEndEdit.AddListener(OnCheckTimeInput);
            if (forcedExposeIntervalInput != null) forcedExposeIntervalInput.onEndEdit.AddListener(OnForcedExposeIntervalInput);
            if (forcedExposeToggle != null) forcedExposeToggle.onValueChanged.AddListener(OnForcedExposeChanged);

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

        private void OnForcedExposeChanged(bool isOn)
        {
            if (_option == null) return;
            _option.forcedExposeEnabled = isOn;  // 토글 상태 반영
            RefreshForcedExposeUI();             // 패널 스왑
        }

        private void RefreshForcedExposeUI()
        {
            if (_option == null) return;
            bool on = _option.forcedExposeEnabled;

            // 체크 표시는 Toggle 컴포넌트가 처리하므로 값만 알림 없이 반영한다.
            if (forcedExposeToggle != null) forcedExposeToggle.SetIsOnWithoutNotify(on);

            // on: 실제 패널 / off: 회색 비활성 패널
            if (forcedExposeActivePanel != null) forcedExposeActivePanel.SetActive(on);
            if (forcedExposeDisabledPanel != null) forcedExposeDisabledPanel.SetActive(!on);
        }

        private void RefreshAll()
        {
            if (_option == null) return;

            RefreshTimeUI();

            if (hideTimeSlider != null) hideTimeSlider.SetValueWithoutNotify(_option.hideTime);
            if (seekTimeSlider != null) seekTimeSlider.SetValueWithoutNotify(_option.seekTime);
            if (checkTimeSlider != null) checkTimeSlider.SetValueWithoutNotify(_option.checkTime);
            if (forcedExposeIntervalSlider != null) forcedExposeIntervalSlider.SetValueWithoutNotify(_option.forcedExposeInterval);
            RefreshForcedExposeUI();
        }

        private void RefreshTimeUI()
        {
            if (hideTimeInput != null) ForceSetText(hideTimeInput, _option.hideTime.ToString("0"));
            if (seekTimeInput != null) ForceSetText(seekTimeInput, _option.seekTime.ToString("0"));
            if (checkTimeInput != null) ForceSetText(checkTimeInput, _option.checkTime.ToString("0"));
            if (forcedExposeIntervalInput != null) ForceSetText(forcedExposeIntervalInput, _option.forcedExposeInterval.ToString("0"));
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
