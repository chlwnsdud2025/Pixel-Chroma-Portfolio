using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace PixelChroma.UI
{
    public class UIInitialLoading : UIPanel
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 0.5f;

        public override void Initialize()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        protected override UniTask OnShowAnimation()
        {
            canvasGroup.alpha = 1f;
            return UniTask.CompletedTask;
        }

        protected override async UniTask OnHideAnimation()
        {
            await canvasGroup.DOFade(0f, fadeDuration).ToUniTask();
        }
    }
}