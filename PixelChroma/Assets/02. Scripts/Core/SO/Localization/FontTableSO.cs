using System;
using OneText;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Serialization;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "FontTable", menuName = "PixelChroma/Localization/FontTable")]
    public class FontTableSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public LocaleIdentifier[] locales;

            // UI/디스플레이용 폰트. 게임 분위기용 디스플레이 폰트(픽셀·둥근 폰트 등)를 쓴다.
            public AssetReferenceT<OneFontAsset> font;

            // 채팅/본문용 폰트. 가독성 위주(Noto Sans 계열 등)로 지정한다.
            public AssetReferenceT<OneFontAsset> chatFont;
        }

        [SerializeField] private Entry[] entries;

        [SerializeField] private AssetReferenceT<OneFontAsset> fallback;
        [SerializeField] private AssetReferenceT<OneFontAsset> chatFallback;

        public AssetReferenceT<OneFontAsset> Resolve(LocaleIdentifier identifier)
        {
            foreach (var entry in entries)
                foreach (var locale in entry.locales)
                    if (locale == identifier)
                        return entry.font;

            return fallback;
        }

        // 채팅/본문용 폰트 해석. chatFont가 없으면 chatFallback으로, 그마저 없으면
        // UI 폰트(entry.font/fallback)로 우아하게 대체해 아직 미설정 상태에서도 채팅이 렌더된다.
        public AssetReferenceT<OneFontAsset> ResolveChat(LocaleIdentifier identifier)
        {
            foreach (var entry in entries)
                foreach (var locale in entry.locales)
                    if (locale == identifier)
                        return Pick(entry.chatFont, chatFallback, entry.font);

            return Pick(null, chatFallback, fallback);
        }

        private static AssetReferenceT<OneFontAsset> Pick(
            AssetReferenceT<OneFontAsset> primary,
            AssetReferenceT<OneFontAsset> secondary,
            AssetReferenceT<OneFontAsset> tertiary)
        {
            if (IsValid(primary)) return primary;
            if (IsValid(secondary)) return secondary;
            return tertiary;
        }

        private static bool IsValid(AssetReferenceT<OneFontAsset> reference) =>
            reference != null && reference.RuntimeKeyIsValid();
    }
}
