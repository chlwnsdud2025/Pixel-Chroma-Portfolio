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

            // OneText 도입으로 Static/Dynamic 폰트 구분이 사라져 로케일당 폰트 하나로 통합.
            // 기존 staticFont 슬롯을 이어받되, 타입이 TMP_FontAsset → OneFontAsset으로 바뀌었으므로
            // 인스펙터에서 OneFontAsset을 다시 지정해야 한다(GUID는 남지만 대상 타입이 다름).
            [FormerlySerializedAs("staticFont")]
            [FormerlySerializedAs("font")]
            public AssetReferenceT<OneFontAsset> font;
        }

        [SerializeField] private Entry[] entries;

        [FormerlySerializedAs("staticFallback")]
        [FormerlySerializedAs("fallbackFont")]
        [SerializeField] private AssetReferenceT<OneFontAsset> fallback;

        public AssetReferenceT<OneFontAsset> Resolve(LocaleIdentifier identifier)
        {
            foreach (var entry in entries)
                foreach (var locale in entry.locales)
                    if (locale == identifier)
                        return entry.font;

            return fallback;
        }
    }
}
