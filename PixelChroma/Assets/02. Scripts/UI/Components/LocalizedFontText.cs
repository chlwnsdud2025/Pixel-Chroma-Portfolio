using OneText.UGUI;
using UnityEngine;

namespace PixelChroma.UI
{
    /// <summary>
    /// 이 <see cref="OneTextLabel"/>이 로컬라이즈 폰트를 따라가도록 폰트 풀에 직접 등록한다.
    ///
    /// UIPanel 스캔이 닿지 않거나(월드 공간 닉네임 등) 스캔 시점 이후에 생성되는
    /// (채팅 풀 등) 라벨에 붙인다. UIPanel 아래에 있어 이미 스캔되는 라벨에 중복으로
    /// 붙어도 무방하다(등록은 HashSet이라 중복을 흡수).
    ///
    /// OneText 도입으로 Static/Dynamic 폰트 구분과 런타임 아틀라스 프레임 드랍이 사라지면서,
    /// 기존 StaticFontText/DynamicFontText 두 마커를 이 하나로 통합했다.
    /// </summary>
    [RequireComponent(typeof(OneTextLabel))]
    public class LocalizedFontText : MonoBehaviour
    {
        private OneTextLabel _label;

        public OneTextLabel Label => _label != null ? _label : (_label = GetComponent<OneTextLabel>());

        private void Awake()
        {
            _label = GetComponent<OneTextLabel>();
            LocalizationManager.Register(_label);
        }

        private void OnDestroy()
        {
            LocalizationManager.Unregister(_label);
        }
    }
}
