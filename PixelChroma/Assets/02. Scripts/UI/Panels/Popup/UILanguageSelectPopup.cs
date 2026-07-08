using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;

namespace PixelChroma.UI
{
    public class UILanguageSelectPopup : UIPopup
    {
        [Header("Language List")]
        [SerializeField] private Transform contentRoot;
        [SerializeField] private GameObject languageButtonPrefab;

        private readonly List<GameObject> _spawned = new();

        public override void Initialize()
        {
            base.Initialize();
            BuildLanguageButtons();
        }

        private void BuildLanguageButtons()
        {
            foreach (var go in _spawned) Destroy(go);
            _spawned.Clear();

            var locales = LocalizationSettings.AvailableLocales.Locales;

            foreach (var locale in locales)
            {
                var buttonObj = Instantiate(languageButtonPrefab, contentRoot);
                _spawned.Add(buttonObj);

                var label = buttonObj.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = locale.Identifier.CultureInfo != null
                        ? locale.Identifier.CultureInfo.NativeName
                        : locale.Identifier.Code;

                var button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    Locale capturedLocale = locale;
                    button.onClick.AddListener(() => OnLanguageSelected(capturedLocale));
                }
            }
        }

        private void OnLanguageSelected(Locale locale)
        {
            LocalizationManager.SetLocale(locale);
            OnClickBack();
        }
    }
}