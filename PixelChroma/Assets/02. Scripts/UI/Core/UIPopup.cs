using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PixelChroma.UI
{
    public abstract class UIPopup : UIPanel
    {
        public void OnClickBack()
        {
            UIManager.Instance.HidePopup().Forget();
        }
    }
}