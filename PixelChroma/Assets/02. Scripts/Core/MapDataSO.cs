using Mirror;
using UnityEngine;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "MapData", menuName = "PixelChroma/Map/MapData")]
    public class MapDataSO : ScriptableObject
    {
        public string displayName;
        [Scene] public string sceneName;
        public Sprite previewSprite;

        public string SceneName => System.IO.Path.GetFileNameWithoutExtension(sceneName);
    }
}
