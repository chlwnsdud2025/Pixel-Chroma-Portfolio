using UnityEngine;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace PixelChroma.UI
{
    public class UISpectatorPopup : UIPopup
    {
        [Header("Spectator UI")]
        [SerializeField] private TMP_Text targetNameText; // 관전 대상의 닉네임을 띄울 텍스트
        [SerializeField] private RectTransform panel;     // 팝업 애니메이션용 패널
        [SerializeField] private float animationDuration = 0.25f;

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();
            if (panel != null) panel.localScale = Vector3.zero;
        }

        protected override async UniTask OnShowAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack).ToUniTask();
        }

        protected override async UniTask OnHideAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.zero, animationDuration).SetEase(Ease.InBack).ToUniTask();
        }

        // 외부(RunnerPlayer)에서 관전 대상이 바뀔 때마다 이 함수를 호출해 이름을 바꿉니다.
        public void SetTargetName(string name)
        {
            if (targetNameText != null)
            {
                targetNameText.text = $"관전 중: {name}";
            }
        }
    }
}