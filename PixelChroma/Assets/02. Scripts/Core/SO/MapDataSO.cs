using Mirror;
using UnityEngine;
using UnityEngine.Localization;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "MapData", menuName = "PixelChroma/Map/MapData")]
    public class MapDataSO : ScriptableObject
    {
        public LocalizedString displayName;
        [Scene] public string sceneName;
        public Sprite previewSprite;

        public string SceneName => System.IO.Path.GetFileNameWithoutExtension(sceneName);

        public string GetDisplayName()
        {
            if (displayName == null || displayName.IsEmpty)
                return name;
            return displayName.GetLocalizedString();
        }
    }
}
