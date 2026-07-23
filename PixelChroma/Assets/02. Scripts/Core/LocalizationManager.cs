using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

        public static TMP_FontAsset CurrentFont { get; private set; }

        private static readonly HashSet<TMP_Text> _texts = new();

        private static FontTableSO _fontTable;
        private static AsyncOperationHandle<TMP_FontAsset> _handle;
        private static object _currentRuntimeKey;

        public static void Initialize(FontTableSO fontTable)
        {
            _fontTable = fontTable;
        }

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

        public static async UniTask ApplySavedLocaleAsync()
        {
            string savedCode = PlayerPrefs.GetString(PrefsKey, "");

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

            var newHandle = Addressables.LoadAssetAsync<TMP_FontAsset>(reference.RuntimeKey);
            var newFont = await newHandle.ToUniTask();

            var oldHandle = _handle;

            CurrentFont = newFont;
            _handle = newHandle;
            _currentRuntimeKey = reference.RuntimeKey;

            ApplyToRegistered(newFont);

            if (oldHandle.IsValid())
                Addressables.Release(oldHandle);
        }

        private static void ApplyToRegistered(TMP_FontAsset font)
        {
            _texts.RemoveWhere(t => t == null);

            foreach (var text in _texts)
                text.font = font;
        }

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
        }
    }
}
