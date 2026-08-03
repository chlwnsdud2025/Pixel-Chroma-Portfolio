using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIQuickMatch : UIPopup
    {
        [Header("Status")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject loadingIndicator;

        [Header("Localization")]
        [SerializeField] private QuickMatchTextDataSO textData;
        [SerializeField] private LobbyFailureTextDataSO failureTextData;

        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button closeButton;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        public override void Initialize()
        {
            base.Initialize();

            if (retryButton != null) retryButton.onClick.AddListener(OnClickRetry);
            if (closeButton != null) closeButton.onClick.AddListener(OnClickBack);
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            SetLoading();

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

        protected override UniTask OnShow()
        {
            LobbyManager.Instance.OnLobbyFailed += HandleLobbyFailed;
            LobbyManager.Instance.JoinQuickMatch();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnHide()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyFailed -= HandleLobbyFailed;

            return UniTask.CompletedTask;
        }

        private void HandleLobbyFailed(LobbyFailureReason reason)
        {
            SetFailed(reason);
        }

        private void SetLoading()
        {
            if (statusText != null) statusText.text = textData.findingRoomStatus.GetLocalizedString();
            if (loadingIndicator != null) loadingIndicator.SetActive(true);
            if (retryButton != null) retryButton.gameObject.SetActive(false);
        }

        private void SetFailed(LobbyFailureReason reason)
        {
            if (statusText != null && failureTextData != null)
                statusText.text = failureTextData.GetLocalized(reason);
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            if (retryButton != null) retryButton.gameObject.SetActive(true);
        }

        private void OnClickRetry()
        {
            SetLoading();
            LobbyManager.Instance.JoinQuickMatch();
        }
    }
}
