using System;
using TMPro;
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
            [FormerlySerializedAs("font")]
            public AssetReferenceT<TMP_FontAsset> staticFont;
            public AssetReferenceT<TMP_FontAsset> dynamicFont;
        }

        [SerializeField] private Entry[] entries;

        [FormerlySerializedAs("fallbackFont")]
        [SerializeField] private AssetReferenceT<TMP_FontAsset> staticFallback;
        [SerializeField] private AssetReferenceT<TMP_FontAsset> dynamicFallback;

        public AssetReferenceT<TMP_FontAsset> Resolve(LocaleIdentifier identifier)
        {
            foreach (var entry in entries)
                foreach (var locale in entry.locales)
                    if (locale == identifier)
                        return entry.staticFont;

            return staticFallback;
        }

        public AssetReferenceT<TMP_FontAsset> ResolveDynamic(LocaleIdentifier identifier)
        {
            foreach (var entry in entries)
            {
                foreach (var locale in entry.locales)
                {
                    if (locale == identifier)
                    {
                        if (entry.dynamicFont != null && entry.dynamicFont.RuntimeKeyIsValid())
                            return entry.dynamicFont;
                        return dynamicFallback;
                    }
                }
            }

            return dynamicFallback;
        }
    }
}
