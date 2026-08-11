using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using OneText.UGUI;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIWorkshopSubscribePopup : UIPopup
    {
        [Header("Content")]
        [SerializeField] private OneTextLabel messageText;
        [SerializeField] private OneTextLabel statusText;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private OneTextLabel confirmButtonText;
        [SerializeField] private Button cancelButton;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        private ulong _fileId;
        private bool _processing;
        private CancellationTokenSource _cts;

        /// <summary>
        /// 구독+다운로드가 완료되면 호출된다.
        /// 호출자(UIRoomList 등)가 여기에 입장 로직을 연결한다.
        /// </summary>
        public event Action OnSubscribeCompleted;

        public override void Initialize()
        {
            base.Initialize();
            if (confirmButton != null) confirmButton.AddClickListener(OnClickConfirm);
            if (cancelButton != null) cancelButton.AddClickListener(OnClickBack);
        }

        /// <summary>
        /// 팝업 표시 전에 fileId를 설정한다.
        /// UIManager.ShowPopup 의 configure 콜백에서 호출.
        /// </summary>
        public void Setup(ulong workshopFileId)
        {
            _fileId = workshopFileId;
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            _processing = false;

            if (messageText != null)
                messageText.text = "이 방은 창작마당 맵을 사용합니다.\n구독 후 참가하시겠습니까?";

            if (statusText != null)
                statusText.gameObject.SetActive(false);

            SetButtonsInteractable(true);

            if (panel != null) panel.localScale = Vector3.zero;
        }

        public override void OnAfterHide()
        {
            base.OnAfterHide();
            CancelProcessing();
            OnSubscribeCompleted = null; // 이벤트 정리
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

        public override void OnClickBack()
        {
            if (_processing) return;
            CancelProcessing();
            base.OnClickBack();
        }

        private void OnClickConfirm()
        {
            if (_processing) return;
            _processing = true;
            SetButtonsInteractable(false);
            SubscribeAndWaitAsync().Forget();
        }

        private async UniTaskVoid SubscribeAndWaitAsync()
        {
            CancelProcessing();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                // 1. 구독 요청
                SetStatus("구독 중...");
                WorkshopSubscriptionService.SubscribeItem(_fileId);

                // Steam 구독 반영 대기
                await UniTask.Delay(1000, cancellationToken: token);

                // 2. 다운로드 시작
                SetStatus("다운로드 중...");
                WorkshopSubscriptionService.RequestDownload(_fileId);

                // 3. 설치 완료까지 폴링 (최대 60초)
                float elapsed = 0f;
                const float timeout = 60f;
                const float pollInterval = 0.5f;

                while (!token.IsCancellationRequested)
                {
                    if (WorkshopSubscriptionService.IsInstalled(_fileId))
                        break;

                    elapsed += pollInterval;
                    if (elapsed >= timeout)
                    {
                        SetStatus("다운로드 시간 초과");
                        _processing = false;
                        SetButtonsInteractable(true);
                        return;
                    }

                    await UniTask.Delay(
                        (int)(pollInterval * 1000),
                        cancellationToken: token);
                }

                if (token.IsCancellationRequested) return;

                // 4. 완료 → 팝업 닫고 입장 진행
                SetStatus("완료!");
                OnSubscribeCompleted?.Invoke();
                await UIManager.Instance.HidePopup();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogError("[WorkshopSubscribe] 구독 실패: " + e.Message);
                SetStatus("구독에 실패했습니다.");
                _processing = false;
                SetButtonsInteractable(true);
            }
        }

        private void SetStatus(string text)
        {
            if (statusText == null) return;
            statusText.gameObject.SetActive(true);
            statusText.text = text;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (confirmButton != null) confirmButton.interactable = interactable;
            if (cancelButton != null) cancelButton.interactable = interactable || !_processing;
        }

        private void CancelProcessing()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _processing = false;
        }
    }
}