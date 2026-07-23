using System;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "FontTable", menuName = "PixelChroma/Localization/FontTable")]
    public class FontTableSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public LocaleIdentifier[] locales;
            public AssetReferenceT<TMP_FontAsset> font;
        }

        [SerializeField] private Entry[] entries;
        [SerializeField] private AssetReferenceT<TMP_FontAsset> fallbackFont;

        public AssetReferenceT<TMP_FontAsset> Resolve(LocaleIdentifier identifier)
        {
            foreach (var entry in entries)
            {
                foreach (var locale in entry.locales)
                {
                    if (locale == identifier)
                        return entry.font;
                }
            }

            return fallbackFont;
        }
    }
}
