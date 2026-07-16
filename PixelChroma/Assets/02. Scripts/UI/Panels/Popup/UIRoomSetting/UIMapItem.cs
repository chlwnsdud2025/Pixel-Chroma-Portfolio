using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    [RequireComponent(typeof(Button))]
    public class UIMapItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button button;
        [SerializeField] private GameObject focus;

        public event Action<int> OnClicked;

        private int _index;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (label == null)
                label = GetComponentInChildren<TMP_Text>();

            button.onClick.AddListener(() => OnClicked?.Invoke(_index));
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
    }
}
