using UnityEngine;

namespace PixelChroma.UI
{
    // IndicatorSlot 프리팹 루트에 붙는 컴포넌트.
    // Slot(자기 자신)은 위치만 담당하고 절대 회전하지 않음. RingArrow만 방향에 따라 회전함.
    public class IndicatorIconView : MonoBehaviour
    {
        [SerializeField] private RectTransform ringArrow;

        public RectTransform Slot => (RectTransform)transform;
        public RectTransform RingArrow => ringArrow;
    }
}