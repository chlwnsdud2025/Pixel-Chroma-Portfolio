using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace PixelChroma
{
    public class LogoAnimation : MonoBehaviour
    {
        [SerializeField] private Transform panel;
        [SerializeField] private float animationDuration = 0.25f;

        private async void Start()
        {
            if (panel != null)
                panel.localScale = Vector3.zero;

            await ShowAnimation();
        }

        public async UniTask ShowAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.one, animationDuration)
                    .SetEase(Ease.OutBack)
                    .ToUniTask();
        }
    }
}