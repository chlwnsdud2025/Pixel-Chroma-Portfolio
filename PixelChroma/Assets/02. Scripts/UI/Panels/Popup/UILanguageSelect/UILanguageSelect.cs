using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UILanguageSelect : UIPopup
    {
        [Header("Language List")]
        [SerializeField] private Transform contentRoot;
        [SerializeField] private LanguageButton languageButtonPrefab;

        [Header("Button")]
        [SerializeField] private Button closeButton;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        private readonly List<LanguageButton> _spawned = new();

        // 언어 선택 버튼에 표시할 이름을 명시적으로 지정한다.
        private static readonly Dictionary<string, string> DisplayNames = new()
        {
            ["ko"] = "한국어",
            ["en"] = "English",
            ["ja"] = "日本語",
            ["zh-Hans"] = "简体中文",
            ["zh-Hant"] = "繁體中文",
            ["ru"] = "Русский",
            ["pt-BR"] = "Português (Brasil)",
            ["es"] = "Español",
            ["de"] = "Deutsch",
            ["fr"] = "Français",
            ["it"] = "Italiano",
            ["pl"] = "Polski",
            ["tr"] = "Türkçe",
        };

        public override void Initialize()
        {
            base.Initialize();
            BuildLanguageButtons();

            if (closeButton != null) closeButton.onClick.AddListener(OnClickBack);
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();
            RefreshFocus();

            if (panel != null) panel.localScale = Vector3.zero;
        }

        protected override async UniTask OnShowAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack).ToUniTask();
        }

        protected override async UniTask OnHideAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.zero, animationDuration).SetEase(Ease.InBack).ToUniTask();
        }

        private void RefreshFocus()
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            var current = LocalizationSettings.SelectedLocale;

            for (int i = 0; i < _spawned.Count && i < locales.Count; i++)
                _spawned[i].SetSelected(locales[i] == current);
        }

        private void BuildLanguageButtons()
        {
            foreach (var btn in _spawned) Destroy(btn.gameObject);
            _spawned.Clear();

            var locales = LocalizationSettings.AvailableLocales.Locales;
            var current = LocalizationSettings.SelectedLocale;

            foreach (var locale in locales)
            {
                var btn = Instantiate(languageButtonPrefab, contentRoot);
                _spawned.Add(btn);

                string code = locale.Identifier.Code;
                string name = DisplayNames.TryGetValue(code, out var display)
                    ? display
                    : (locale.Identifier.CultureInfo?.NativeName ?? code);
                btn.Setup(name, locale == current);

                Locale captured = locale;
                btn.Button.onClick.AddListener(() => OnLanguageSelected(captured));
            }
        }

        private void OnLanguageSelected(Locale locale)
        {
            OnLanguageSelectedAsync(locale).Forget();
        }

        private async UniTask OnLanguageSelectedAsync(Locale locale)
        {
            await LocalizationManager.SetLocaleAsync(locale);
            RefreshFocus();
            OnClickBack();
        }
    }
}
