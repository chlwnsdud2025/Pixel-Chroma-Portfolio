using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OneText;
using OneText.UGUI;
using Steamworks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PixelChroma
{
    public static class LocalizationManager
    {
        private const string PrefsKey = "SelectedLocaleCode";

        // OneText 도입으로 Static/Dynamic 폰트 풀 구분이 사라져 라벨 풀 하나로 통합.
        private static readonly HashSet<OneTextLabel> _labels = new();
        private static AsyncOperationHandle<OneFontAsset> _handle;
        private static object _currentRuntimeKey;

        private static FontTableSO _fontTable;

        public static OneFontAsset CurrentFont { get; private set; }

        public static void Initialize(FontTableSO fontTable)
        {
            _fontTable = fontTable;
        }

        // ── Register ────────────────────────────────────────────────────

        public static void Register(OneTextLabel label)
        {
            if (label == null || !_labels.Add(label))
                return;

            if (CurrentFont != null)
                label.Font = CurrentFont;
        }

        public static void Unregister(OneTextLabel label)
        {
            if (label != null)
                _labels.Remove(label);
        }

        // ── Locale ──────────────────────────────────────────────────────

        // 현재 선택된 로케일 코드. 선택된 로케일이 없으면 빈 문자열.
        public static string CurrentLanguageCode =>
            LocalizationSettings.SelectedLocale != null
                ? LocalizationSettings.SelectedLocale.Identifier.Code
                : "";

        public static async UniTask ApplySavedLocaleAsync()
        {
            string savedCode = PlayerPrefs.GetString(PrefsKey, "");

            // 저장된 선택이 없으면(최초 실행 등) Steam 게임 언어를 기본값으로 시도합니다.
            // Steam이 실제 초기화된 경우에만 조회합니다(초기화 실패 시 경고 패널 경로에서는 건너뜀).
            if (string.IsNullOrEmpty(savedCode) && GameManager.SteamInitialized)
                savedCode = TryGetSteamLocaleCode();

            if (!string.IsNullOrEmpty(savedCode))
            {
                var saved = LocalizationSettings.AvailableLocales.GetLocale(savedCode);
                if (saved != null)
                    LocalizationSettings.SelectedLocale = saved;
            }

            var locale = LocalizationSettings.SelectedLocale;
            if (locale != null)
                await ApplyFontAsync(locale);
        }

        public static async UniTask SetLocaleAsync(Locale locale)
        {
            if (locale == null) return;

            await ApplyFontAsync(locale);
            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(PrefsKey, locale.Identifier.Code);
        }

        // ── Cleanup ─────────────────────────────────────────────────────
        public static void Release()
        {
            _labels.Clear();
            CurrentFont = null;
            _currentRuntimeKey = null;

            if (_handle.IsValid())
            {
                Addressables.Release(_handle);
                _handle = default;
            }
        }

        // ── Font ────────────────────────────────────────────────────────
        private static async UniTask ApplyFontAsync(Locale locale)
        {
            if (_fontTable == null)
            {
                Debug.LogError("[LocalizationManager] FontTable이 초기화되지 않았습니다.");
                return;
            }

            var reference = _fontTable.Resolve(locale.Identifier);
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                Debug.LogWarning($"[LocalizationManager] locale '{locale.Identifier.Code}'에 대한 폰트가 없습니다.");
                return;
            }

            if (_currentRuntimeKey != null && _currentRuntimeKey.Equals(reference.RuntimeKey))
                return;

            var newHandle = Addressables.LoadAssetAsync<OneFontAsset>(reference.RuntimeKey);
            var newFont = await newHandle.ToUniTask();
            var oldHandle = _handle;

            CurrentFont = newFont;
            _handle = newHandle;
            _currentRuntimeKey = reference.RuntimeKey;

            _labels.RemoveWhere(l => l == null);
            foreach (var label in _labels)
                label.Font = newFont;

            // 새 폰트를 모든 라벨에 반영한 뒤에 이전 핸들을 해제한다.
            if (oldHandle.IsValid())
                Addressables.Release(oldHandle);
        }

        private static string TryGetSteamLocaleCode()
        {
            string steamLang = SteamApps.GetCurrentGameLanguage();
            return SteamLangToLocaleCode(steamLang);
        }

        private static string SteamLangToLocaleCode(string steamLang) => steamLang switch
        {
            "english" => "en",
            "koreana" => "ko",   // Steam은 한국어를 "korean"이 아닌 "koreana"로 반환
            "japanese" => "ja",
            "schinese" => "zh-Hans",
            "tchinese" => "zh-Hant",
            "french" => "fr",
            "german" => "de",
            "spanish" => "es",   // Spanish-Spain
            "latam" => "es",   // Spanish-Latin America → es로 통합
            "italian" => "it",
            "polish" => "pl",
            "russian" => "ru",
            "turkish" => "tr",
            "brazilian" => "pt-BR",
            "portuguese" => "pt-BR",
            _ => "" // 미지원 언어 → 시스템/기본 로케일로 fallback
        };
    }
}
