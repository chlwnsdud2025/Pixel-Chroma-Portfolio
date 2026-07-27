using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;
using Steamworks;
using TMPro;
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

        private static readonly HashSet<TMP_Text> _texts = new();
        private static AsyncOperationHandle<TMP_FontAsset> _handle;
        private static object _currentRuntimeKey;

        private static readonly HashSet<DynamicFontText> _dynamicTexts = new();
        private static AsyncOperationHandle<TMP_FontAsset> _dynamicHandle;
        private static object _dynamicRuntimeKey;

        private static FontTableSO _fontTable;

        public static TMP_FontAsset CurrentFont { get; private set; }
        public static TMP_FontAsset CurrentDynamicFont { get; private set; }

        public static void Initialize(FontTableSO fontTable)
        {
            _fontTable = fontTable;
        }

        // ── Static register ─────────────────────────────────────────────

        public static void Register(TMP_Text text)
        {
            if (text == null || !_texts.Add(text))
                return;

            if (CurrentFont != null)
                text.font = CurrentFont;
        }

        public static void Unregister(TMP_Text text)
        {
            if (text != null)
                _texts.Remove(text);
        }

        // ── Dynamic register ────────────────────────────────────────────

        public static void RegisterDynamic(DynamicFontText dft)
        {
            if (dft == null || !_dynamicTexts.Add(dft))
                return;

            if (CurrentDynamicFont != null)
                dft.Apply(CurrentDynamicFont);
        }

        public static void UnregisterDynamic(DynamicFontText dft)
        {
            if (dft != null)
                _dynamicTexts.Remove(dft);
        }

        // ── Locale ──────────────────────────────────────────────────────

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
                await ApplyFontsAsync(locale);
        }

        public static async UniTask SetLocaleAsync(Locale locale)
        {
            if (locale == null) return;

            await ApplyFontsAsync(locale);
            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(PrefsKey, locale.Identifier.Code);
        }

        // ── Cleanup ─────────────────────────────────────────────────────
        public static void Release()
        {
            _texts.Clear();
            CurrentFont = null;
            _currentRuntimeKey = null;

            if (_handle.IsValid())
            {
                Addressables.Release(_handle);
                _handle = default;
            }

            _dynamicTexts.Clear();
            CurrentDynamicFont = null;
            _dynamicRuntimeKey = null;

            if (_dynamicHandle.IsValid())
            {
                Addressables.Release(_dynamicHandle);
                _dynamicHandle = default;
            }
        }

        private static async UniTask ApplyFontsAsync(Locale locale)
        {
            if (_fontTable == null)
            {
                Debug.LogError("[LocalizationManager] FontTable이 초기화되지 않았습니다.");
                return;
            }

            var t1 = ApplyStaticFontAsync(locale);
            var t2 = ApplyDynamicFontAsync(locale);
            await UniTask.WhenAll(t1, t2);
        }

        // ── Static font ─────────────────────────────────────────────────
        private static async UniTask ApplyStaticFontAsync(Locale locale)
        {
            var reference = _fontTable.Resolve(locale.Identifier);
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                Debug.LogWarning($"[LocalizationManager] locale '{locale.Identifier.Code}'에 대한 Static 폰트가 없습니다.");
                return;
            }

            if (_currentRuntimeKey != null && _currentRuntimeKey.Equals(reference.RuntimeKey))
                return;

            var newHandle = Addressables.LoadAssetAsync<TMP_FontAsset>(reference.RuntimeKey);
            var newFont = await newHandle.ToUniTask();
            var oldHandle = _handle;

            CurrentFont = newFont;
            _handle = newHandle;
            _currentRuntimeKey = reference.RuntimeKey;

            _texts.RemoveWhere(t => t == null);
            foreach (var text in _texts)
                text.font = newFont;

            if (oldHandle.IsValid())
                Addressables.Release(oldHandle);
        }

        // ── Dynamic font ────────────────────────────────────────────────
        private static async UniTask ApplyDynamicFontAsync(Locale locale)
        {
            var reference = _fontTable.ResolveDynamic(locale.Identifier);
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                Debug.LogWarning($"[LocalizationManager] locale '{locale.Identifier.Code}'에 대한 Dynamic 폰트가 없습니다.");
                return;
            }

            if (_dynamicRuntimeKey != null && _dynamicRuntimeKey.Equals(reference.RuntimeKey))
                return;

            var newHandle = Addressables.LoadAssetAsync<TMP_FontAsset>(reference.RuntimeKey);
            var newFont = await newHandle.ToUniTask();
            var oldHandle = _dynamicHandle;

            CurrentDynamicFont = newFont;
            _dynamicHandle = newHandle;
            _dynamicRuntimeKey = reference.RuntimeKey;

            _dynamicTexts.RemoveWhere(d => d == null);
            foreach (var dft in _dynamicTexts)
                dft.Apply(newFont);

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
