using System;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 버튼과 선택 표시용 Focus 오브젝트를 한 컴포넌트로 묶는다.
    // Focus 참조를 버튼 자신이 들고 있으므로, 쓰는 쪽 인스펙터에 슬롯이 버튼마다 두 개씩 늘지 않는다.
    //
    // 주의: 여기서 다루는 "선택"은 EventSystem의 Selected 상태가 아니라 지속되는 선택 상태다.
    // (Button의 Transition은 호버/누름 같은 순간적 피드백 용도로 그대로 두고 쓰면 된다.)
    [RequireComponent(typeof(Button))]
    public class UIFocusButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private GameObject focus;

        public event Action OnClicked;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            button.onClick.AddListener(() => OnClicked?.Invoke());
        }

        public void SetSelected(bool selected)
        {
            if (focus != null)
                focus.SetActive(selected);
        }
    }
}
