using System;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    [RequireComponent(typeof(Button))]
    public class UIMapItem : MonoBehaviour
    {
        [SerializeField] private OneTextLabel label;
        [SerializeField] private Button button;
        [SerializeField] private GameObject focus;
        [SerializeField] private GameObject workshopBadge;

        public event Action<int> OnClicked;

        private int _index;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (label == null)
                label = GetComponentInChildren<OneTextLabel>();

            button.AddClickListener(() => OnClicked?.Invoke(_index));
        }

        public void Setup(int index, string displayName)
        {
            _index = index;
            if (label != null) label.text = displayName;
        }

        public void SetSelected(bool selected)
        {
            if (focus != null)
                focus.SetActive(selected);
        }

        /// <summary>
        /// 창작마당 뱃지 표시 여부를 설정한다.
        /// 창작마당 프리팹에서만 의미 있다.
        /// </summary>
        public void SetWorkshopBadge(bool visible)
        {
            if (workshopBadge != null)
                workshopBadge.SetActive(visible);
        }
    }
}