using Cysharp.Threading.Tasks;
using UnityEngine;

namespace PixelChroma.UI
{
    public abstract class UIPanel : MonoBehaviour
    {
        protected virtual void Awake()
        {
            gameObject.SetActive(false);
        }

        public async UniTask Show()
        {
            gameObject.SetActive(true);
            await OnShow();
        }

        public async UniTask Hide()
        {
            await OnHide();
            gameObject.SetActive(false);
        }

        protected virtual UniTask OnShow() => UniTask.CompletedTask;
        protected virtual UniTask OnHide() => UniTask.CompletedTask;

        public virtual void OnBeforeShow() { }
        public virtual void OnAfterHide() { }
    }
}
