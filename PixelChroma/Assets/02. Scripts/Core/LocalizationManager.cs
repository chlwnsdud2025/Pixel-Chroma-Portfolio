using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace PixelChroma
{
    public static class LocalizationManager
    {
        private const string PrefsKey = "SelectedLocaleCode";

        // Boot 시점에 한 번 호출해서 저장된 언어를 복원
        public static void ApplySavedLocale()
        {
            string savedCode = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(savedCode)) return;

            var locale = LocalizationSettings.AvailableLocales.GetLocale(savedCode);
            if (locale != null)
                LocalizationSettings.SelectedLocale = locale;
        }

        public static void SetLocale(Locale locale)
        {
            if (locale == null) return;

            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(PrefsKey, locale.Identifier.Code);
        }
    }
}