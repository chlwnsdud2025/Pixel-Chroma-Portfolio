using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIRoomCodeEntry : UIPopup
    {
        [Header("Code Input")]
        [SerializeField] private TMP_InputField roomCodeInput;

        [Header("Show Code")]
        [SerializeField] private Button showCodeButton;
        [SerializeField] private Image showCodeIcon;
        [SerializeField] private Sprite shownSprite;
        [SerializeField] private Sprite hiddenSprite;

        [Header("Actions")]
        [SerializeField] private Button joinButton;
        [SerializeField] private Button closeButton;

        [Header("Status")]
        [SerializeField] private GameObject statusText;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        private bool _isCodeVisible;

        public override void Initialize()
        {
            base.Initialize();

            if (joinButton != null) joinButton.onClick.AddListener(OnClickJoin);
            if (closeButton != null) closeButton.onClick.AddListener(OnClickBack);
            if (showCodeButton != null) showCodeButton.onClick.AddListener(ToggleCodeVisibility);

            if (roomCodeInput != null)
            {
                roomCodeInput.characterLimit = LobbyManager.CodeLength;
                roomCodeInput.onValueChanged.AddListener(OnCodeChanged);
            }
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            if (roomCodeInput != null) roomCodeInput.SetTextWithoutNotify(string.Empty);

            _isCodeVisible = false;
            ApplyCodeVisibility();
            SetStatusVisible(false);
            RefreshJoinButton(0);

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
            return UniTask.CompletedTask;
        }

        protected override UniTask OnHide()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyFailed -= HandleLobbyFailed;

            return UniTask.CompletedTask;
        }

        private void OnCodeChanged(string value)
        {
            string upper = value.ToUpper();
            if (upper != value) roomCodeInput.SetTextWithoutNotify(upper);

            SetStatusVisible(false);
            RefreshJoinButton(upper.Length);
        }

        private void RefreshJoinButton(int length)
        {
            if (joinButton != null) joinButton.interactable = length == LobbyManager.CodeLength;
        }

        private void ToggleCodeVisibility()
        {
            _isCodeVisible = !_isCodeVisible;
            ApplyCodeVisibility();
        }

        private void ApplyCodeVisibility()
        {
            if (roomCodeInput != null)
            {
                roomCodeInput.contentType = _isCodeVisible
                    ? TMP_InputField.ContentType.Standard
                    : TMP_InputField.ContentType.Password;
                roomCodeInput.ForceLabelUpdate();
            }

            var sprite = _isCodeVisible ? shownSprite : hiddenSprite;

            if (showCodeIcon != null && sprite != null)
            {
                showCodeIcon.sprite = sprite;
            }
        }

        private void OnClickJoin()
        {
            string code = roomCodeInput != null ? roomCodeInput.text.Trim() : string.Empty;
            if (code.Length != LobbyManager.CodeLength) 
                return;

            if (joinButton != null) 
                joinButton.interactable = false;

            SetStatusVisible(false);
            LobbyManager.Instance.JoinLobbyByCode(code);
        }

        private void HandleLobbyFailed()
        {
            SetStatusVisible(true);

            if (roomCodeInput != null)
                RefreshJoinButton(roomCodeInput.text.Trim().Length);
        }

        private void SetStatusVisible(bool visible)
        {
            if (statusText != null) statusText.SetActive(visible);
        }
    }
}
