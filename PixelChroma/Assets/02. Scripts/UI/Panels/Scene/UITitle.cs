using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;                 
using UnityEngine.Localization.Settings;

namespace PixelChroma.UI
{
    public class UITitle : UIScene
    {
        [Header("Main Menu Buttons")]
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button findRoomButton;
        [SerializeField] private Button joinRoomButton;

        [Header("Setting Buttons")]
        [SerializeField] private Button nicknameSettingButton;
        [SerializeField] private Button languageSettingButton;


        [Header("Status")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private float statusDisplayDuration = 2f;
        [SerializeField] private float statusFadeDuration = 0.5f;

        [Header("Status Localization")]          
        [SerializeField] private LocalizedString findingRoomStatus;   
        [SerializeField] private LocalizedString notFoundStatus;      


        private CancellationTokenSource _statusCts;

        public override void Initialize()
        {
            base.Initialize();

            if (quickJoinButton != null) quickJoinButton.onClick.AddListener(OnClickQuickJoin);
            if (createRoomButton != null) createRoomButton.onClick.AddListener(OnClickCreateRoom);
            if (findRoomButton != null) findRoomButton.onClick.AddListener(OnClickFindRoom);
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnClickJoinRoom);
            if (nicknameSettingButton != null) nicknameSettingButton.onClick.AddListener(OnClickNicknameSetting);
            if (languageSettingButton != null) languageSettingButton.onClick.AddListener(OnClickLanguageButton);


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
            if (languageSettingButton != null) languageSettingButton.interactable = value;

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
            SetStatus(notFoundStatus.GetLocalizedString());
            SetInteractable(true);
        }

        private void OnClickQuickJoin()
        {
            SetInteractable(false);
            SetStatus(findingRoomStatus.GetLocalizedString());
            LobbyManager.Instance.OnLobbyFailed += HandleQuickJoinFailed;
            LobbyManager.Instance.JoinQuickMatch();
        }

        private void OnClickCreateRoom()
        {
            UIManager.Instance.ShowPopup<UIRoomSetting>().Forget();
        }

        private void OnClickFindRoom()
        {
            UIManager.Instance.ShowPopup<UIRoomList>().Forget();
        }

        private void OnClickJoinRoom()
        {
            UIManager.Instance.ShowPopup<UIRoomCodeEntry>().Forget();
        }
        private void OnClickNicknameSetting()
        {
            UIManager.Instance.ShowPopup<UIMainMenuNickname>().Forget();
        }

        public void OnClickLanguageButton()
        {
            UIManager.Instance.ShowPopup<UILanguageSelectPopup>().Forget();
        }
    }
}