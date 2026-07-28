using TMPro;
using UnityEngine;

namespace PixelChroma.UI
{
    /// <summary>
    /// UIPanel 스캔 경로가 닿지 않는 텍스트(맵 오브젝트 등)를
    /// Static 폰트 풀에 직접 등록한다. DynamicFontText의 Static 버전.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class StaticFontText : MonoBehaviour
    {
        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            LocalizationManager.Register(_text);
        }

        private void OnDestroy()
        {
            LocalizationManager.Unregister(_text);
        }
    }
}
