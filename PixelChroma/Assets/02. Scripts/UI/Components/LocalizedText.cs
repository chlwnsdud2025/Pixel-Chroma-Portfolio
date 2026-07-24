using TMPro;
using UnityEngine;

namespace PixelChroma.UI
{
    // 로케일 폰트를 따라가야 하는 개별 텍스트에 붙인다.
    // 패널 로드 시 자동 등록되지 않는 런타임 생성 텍스트(리스트 아이템 등)에 사용한다.
    // 정적 패널 텍스트는 UIPanel.RegisterLocalizedTexts가 일괄 등록하므로 필요 없다.
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        private TMP_Text _text;

        private void Awake() => _text = GetComponent<TMP_Text>();

        private void OnEnable() => LocalizationManager.Register(_text);

        private void OnDisable() => LocalizationManager.Unregister(_text);
    }
}
