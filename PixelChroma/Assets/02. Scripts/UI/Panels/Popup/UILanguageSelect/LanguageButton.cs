using UnityEngine;
using UnityEngine.UI;
using OneText.UGUI;

namespace PixelChroma.UI
{
    public class LanguageButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private OneTextLabel label;
        [SerializeField] private GameObject focus;

        public Button Button => button;

        public void Setup(string text, bool isSelected)
        {
            label.text = text;
            focus.SetActive(isSelected);
        }

        public void SetSelected(bool isSelected)
        {
            focus.SetActive(isSelected);
        }
    }
}
