using UnityEngine;
using TMPro;

namespace PixelChroma.UI
{
    public class IndicatorIconView : MonoBehaviour
    {
        [SerializeField] private RectTransform ringArrow;
        [SerializeField] private TMP_Text nicknameText;

        public RectTransform Slot => (RectTransform)transform;
        public RectTransform RingArrow => ringArrow;

        public void SetNickname(string name)
        {
            if (nicknameText != null)
                nicknameText.text = name;
        }
    }
}