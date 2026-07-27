using TMPro;
using UnityEngine;

namespace PixelChroma.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public class DynamicFontText : MonoBehaviour
    {
        [SerializeField] private DynamicTextStyleSO _style;

        private TMP_Text _text;

        public TMP_Text Text => _text != null ? _text : (_text = GetComponent<TMP_Text>());

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            LocalizationManager.RegisterDynamic(this);
        }

        private void OnDestroy()
        {
            LocalizationManager.UnregisterDynamic(this);
        }

        public void Apply(TMP_FontAsset font)
        {
            var t = Text;
            t.font = font;
            t.fontSharedMaterial = _style != null ? _style.GetOrCreateMaterial(font) : font.material;
            t.UpdateMeshPadding();
        }
    }
}
