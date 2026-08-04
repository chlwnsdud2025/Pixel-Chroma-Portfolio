using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UISettingDisplayPanel : MonoBehaviour
    {
        [Header("Screen Mode")]
        [SerializeField] private TMP_Dropdown screenModeDropdown;

        [Header("Resolution")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Header("Toggles")]
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private Toggle screenShakeToggle;

        [Header("Localization")]
        [SerializeField] private SettingTextDataSO textData;

        private readonly List<Vector2Int> _resolutions = new();

        public void Initialize()
        {
            BuildScreenModeOptions();
            BuildResolutionOptions();

            if (screenModeDropdown != null)
                screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            if (vSyncToggle != null)
                vSyncToggle.onValueChanged.AddListener(GameSettings.SetVSync);
            if (screenShakeToggle != null)
                screenShakeToggle.onValueChanged.AddListener(GameSettings.SetScreenShake);

            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged(Locale locale)
        {
            BuildScreenModeOptions();
        }

        public void Refresh()
        {
            if (screenModeDropdown != null)
                screenModeDropdown.SetValueWithoutNotify((int)GameSettings.ScreenMode);

            if (resolutionDropdown != null)
                resolutionDropdown.SetValueWithoutNotify(CurrentResolutionIndex());

            if (vSyncToggle != null)
                vSyncToggle.SetIsOnWithoutNotify(GameSettings.VSyncEnabled);
            if (screenShakeToggle != null)
                screenShakeToggle.SetIsOnWithoutNotify(GameSettings.ScreenShakeEnabled);
        }

        private void BuildScreenModeOptions()
        {
            if (screenModeDropdown == null) return;

            screenModeDropdown.ClearOptions();

            if (textData != null)
            {
                screenModeDropdown.AddOptions(new List<string>
                {
                    textData.optionFullscreen.GetLocalizedString(),
                    textData.optionBorderless.GetLocalizedString(),
                    textData.optionWindowed.GetLocalizedString(),
                });
            }
            else
            {
                screenModeDropdown.AddOptions(new List<string> { "Fullscreen", "Borderless", "Windowed" });
            }

            screenModeDropdown.SetValueWithoutNotify((int)GameSettings.ScreenMode);
        }

        // 모니터가 지원하는 해상도를 (너비×높이) 기준으로 중복 제거해 나열한다(주사율 차이는 합침).
        private void BuildResolutionOptions()
        {
            if (resolutionDropdown == null) return;

            _resolutions.Clear();
            var seen = new HashSet<long>();

            foreach (var r in Screen.resolutions)
            {
                long key = ((long)r.width << 32) | (uint)r.height;
                if (seen.Add(key))
                    _resolutions.Add(new Vector2Int(r.width, r.height));
            }

            // 저장된 해상도가 목록에 없으면(모니터 변경 등) 추가해 항상 선택 가능하게 한다.
            var current = new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight);
            if (current.x > 0 && current.y > 0 && !_resolutions.Contains(current))
                _resolutions.Add(current);

            var labels = new List<string>(_resolutions.Count);
            foreach (var res in _resolutions)
                labels.Add($"{res.x} x {res.y}");

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
        }

        private int CurrentResolutionIndex()
        {
            var current = new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight);
            int index = _resolutions.IndexOf(current);
            return index >= 0 ? index : 0;
        }

        private void OnScreenModeChanged(int index)
        {
            GameSettings.SetScreenMode((ScreenMode)index);
        }

        private void OnResolutionChanged(int index)
        {
            if (index < 0 || index >= _resolutions.Count) return;

            var res = _resolutions[index];
            GameSettings.SetResolution(res.x, res.y);
        }
    }
}
