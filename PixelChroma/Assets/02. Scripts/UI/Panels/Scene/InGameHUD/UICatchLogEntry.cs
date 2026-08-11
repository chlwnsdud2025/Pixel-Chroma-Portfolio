using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UICatchLogEntry : MonoBehaviour
    {
        [SerializeField] private OneTextLabel catcherText;
        [SerializeField] private OneTextLabel caughtText;

        public CanvasGroup CanvasGroup { get; private set; }
        public LayoutElement LayoutElement { get; private set; }

        private void Awake()
        {
            CanvasGroup = GetComponent<CanvasGroup>();
            if (CanvasGroup == null)
                CanvasGroup = gameObject.AddComponent<CanvasGroup>();

            LayoutElement = GetComponent<LayoutElement>();
            if (LayoutElement == null)
                LayoutElement = gameObject.AddComponent<LayoutElement>();
        }

        public void Setup(string catcherName, string caughtName)
        {
            catcherText.text = catcherName;
            caughtText.text = caughtName;
        }
    }
}