using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UITitle : UIScene
    {
        [Header("Main Menu Buttons")]
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button findRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button nicknameSettingButton;

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private float statusDisplayDuration = 2f;
        [SerializeField] private float statusFadeDuration = 0.5f;

        private CancellationTokenSource _statusCts;

        public override void Initialize()
        {
            base.Initialize();

            if (quickJoinButton != null) quickJoinButton.onClick.AddListener(OnClickQuickJoin);
            if (createRoomButton != null) createRoomButton.onClick.AddListener(OnClickCreateRoom);
            if (findRoomButton != null) findRoomButton.onClick.AddListener(OnClickFindRoom);
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnClickJoinRoom);
            if (nicknameSettingButton != null) nicknameSettingButton.onClick.AddListener(OnClickNicknameSetting);

            SetStatus(string.Empty);
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            SetStatus(string.Empty);
            SetInteractable(true);
        }

        protected override UniTask OnHide()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyFailed -= HandleQuickJoinFailed;

            return UniTask.CompletedTask;
        }

        private void SetInteractable(bool value)
        {
            if (quickJoinButton != null) quickJoinButton.interactable = value;
            if (createRoomButton != null) createRoomButton.interactable = value;
            if (findRoomButton != null) findRoomButton.interactable = value;
            if (joinRoomButton != null) joinRoomButton.interactable = value;
            if (nicknameSettingButton != null) nicknameSettingButton.interactable = value;
        }

        private void SetStatus(string message)
        {
            if (statusText == null) 
                return;

            _statusCts?.Cancel();
            _statusCts?.Dispose();
            _statusCts = null;

            statusText.DOKill();
            statusText.text = message;
            statusText.alpha = 1f;

            if (!string.IsNullOrEmpty(message))
            {
                _statusCts = new CancellationTokenSource();
                FadeOutStatus(_statusCts.Token).Forget();
            }
        }

        private async UniTaskVoid FadeOutStatus(CancellationToken token)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(statusDisplayDuration),
                cancellationToken: token);

            if (token.IsCancellationRequested) 
                return;

            await statusText.DOFade(0f, statusFadeDuration)
                .ToUniTask(cancellationToken: token);
        }

        private void HandleQuickJoinFailed()
        {
            LobbyManager.Instance.OnLobbyFailed -= HandleQuickJoinFailed;
            SetStatus("공개방을 찾을 수 없습니다.");
            SetInteractable(true);
        }

        private void OnClickQuickJoin()
        {
            SetInteractable(false);
            SetStatus("공개방 찾는 중...");
            LobbyManager.Instance.OnLobbyFailed += HandleQuickJoinFailed;
            LobbyManager.Instance.JoinQuickMatch();
        }

        private void OnClickCreateRoom()
        {
            UIManager.Instance.ShowPopup<UIRoomSetting>().Forget();
        }

        private void OnClickFindRoom()
        {
            // TODO: 룸 리스트 팝업을 띄우고, 선택한 방으로 입장하는 흐름 구현
        }

        private void OnClickJoinRoom()
        {
            UIManager.Instance.ShowPopup<UIRoomCodeEntry>().Forget();
        }
        private void OnClickNicknameSetting()
        {
            UIManager.Instance.ShowPopup<UIMainMenuNickname>().Forget();
        }
    }
}