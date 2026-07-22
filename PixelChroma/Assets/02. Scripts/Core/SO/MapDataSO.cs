using UnityEngine;
using UnityEngine.Localization;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "MapData", menuName = "PixelChroma/Map/MapData")]
    public class MapDataSO : ScriptableObject
    {
        public LocalizedString displayName;
        public GameObject mapPrefab;
        public Sprite previewSprite;

        public string GetDisplayName()
        {
            if (displayName == null || displayName.IsEmpty)
                return name;
            return displayName.GetLocalizedString();
        }
    }
}