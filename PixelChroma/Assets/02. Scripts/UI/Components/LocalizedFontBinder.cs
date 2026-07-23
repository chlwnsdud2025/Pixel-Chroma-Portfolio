using TMPro;
using UnityEngine;

namespace PixelChroma.UI
{
    public class LocalizedFontBinder : MonoBehaviour
    {
        private TMP_Text[] _targets;

        private void OnEnable()
        {
            _targets = GetComponentsInChildren<TMP_Text>(true);
            ApplyCurrentFont();
            LocalizationManager.OnFontChanged += HandleFontChanged;
        }

        private void OnDisable()
        {
            LocalizationManager.OnFontChanged -= HandleFontChanged;
        }

        private void HandleFontChanged(TMP_FontAsset font)
        {
            foreach (var t in _targets)
            {
                if (t != null && t.GetComponentInParent<LocalizedFontIgnore>(true) == null)
                    t.font = font;
            }
        }

        private void ApplyCurrentFont()
        {
            if (LocalizationManager.CurrentFont == null) return;
            HandleFontChanged(LocalizationManager.CurrentFont);
        }
    }
}
