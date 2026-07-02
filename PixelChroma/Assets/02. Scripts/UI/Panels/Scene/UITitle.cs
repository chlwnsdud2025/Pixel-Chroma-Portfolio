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

            SetStatus(string.Empty);
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();
            SetStatus(string.Empty);
            SetInteractable(true);
        }

        private void HandleQuickJoinResult(bool success)
        {
            LobbyManager.Instance.OnLobbyEntered -= HandleQuickJoinResult;

        private void OnDisable()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyEntered -= HandleLobbyEntered;
        }

        private void OnClickOpenSettings()
        {
            ShowRoomSettingPopup().Forget();
        }

        private async UniTaskVoid ShowRoomSettingPopup()
        {
            var popup = await UIManager.Instance.ShowPopup<UIRoomSetting>();
            if (popup == null) return;

            popup.Setup(LobbyManager.Instance.CurrentRoomOption, false);
            popup.SetMaps(LobbyManager.Instance.AvailableMaps);

            popup.OnCreate -= OnRoomSettingCreate;
            popup.OnCreate += OnRoomSettingCreate;
        }

        private void OnRoomSettingCreate(RoomOption option)
        {
            var popup = UIManager.Instance.GetPanel<UIRoomSetting>();
            if (popup != null) popup.OnCreate -= OnRoomSettingCreate;

            SetInteractable(false);
            SetStatus("방 생성 중...");

            LobbyManager.Instance.CreateLobby(option);
            UIManager.Instance.HidePopup().Forget();
        }

        private void OnClickQuickJoin()
        {
            SetInteractable(false);
            SetStatus("공개방 찾는 중...");
            LobbyManager.Instance.JoinQuickMatch();
        }

        private void OnClickJoin()
        {
            string code = roomCodeInput.text.Trim();

            if (code.Length != 6)
            {
                SetStatus("6자리 코드를 입력해주세요.");
                return;
            }

            SetInteractable(false);
            SetStatus("방 입장 중...");
            LobbyManager.Instance.JoinLobbyByCode(code);
        }

        private void HandleLobbyEntered(bool success)
        {
            if (success)
            {
                GameManager.Instance.EnterLobby().Forget();
                return;
            }

            SetStatus("공개방을 찾을 수 없습니다.");
            SetInteractable(true);
        }

        private void SetInteractable(bool value)
        {
            if (quickJoinButton != null) quickJoinButton.interactable = value;
            if (createRoomButton != null) createRoomButton.interactable = value;
            if (findRoomButton != null) findRoomButton.interactable = value;
            if (joinRoomButton != null) joinRoomButton.interactable = value;
        }

        private void SetStatus(string message)
        {
            if (statusText == null) return;

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

            if (token.IsCancellationRequested) return;

            await statusText.DOFade(0f, statusFadeDuration)
                .ToUniTask(cancellationToken: token);
        }

        private void OnClickQuickJoin()
        {
            SetInteractable(false);
            SetStatus("공개방 찾는 중...");
            LobbyManager.Instance.OnLobbyEntered += HandleQuickJoinResult;
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
    }
}
