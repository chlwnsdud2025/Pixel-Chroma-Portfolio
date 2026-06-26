using UnityEngine;

namespace PixelChroma.UI
{
    public class UIInitialLoading : MonoBehaviour
    {
        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
