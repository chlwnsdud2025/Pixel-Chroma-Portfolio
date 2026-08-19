using Cysharp.Threading.Tasks;
using DG.Tweening;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // RoomOption의 단일 원본을 소유하고, 각 패널에 참조를 물려준 뒤 생성/변경 흐름만 담당한다.
    public class UIRoomSetting : UIPopup
    {
        [Header("Panels")]
        [SerializeField] private UIRoomInfoPanel roomInfoPanel;
        [SerializeField] private UIGameSettingPanel gameSettingPanel;
        [SerializeField] private UIMapPanel mapPanel;

        [Header("Create")]
        [SerializeField] private Button createButton;
        [SerializeField] private OneTextLabel createButtonText;
        [SerializeField] private OneTextLabel statusText;

        [Header("Close")]
        [SerializeField] private Button closeButton;

        [Header("Localization")]
        [SerializeField] private RoomSettingTextDataSO textData;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        private RoomOption _option;
        private bool _isEditMode = false;
        private bool _creating = false;

        public override bool CloseOnEscape => !_creating;

        public override void Initialize()
        {
            base.Initialize();

            if (roomInfoPanel != null) roomInfoPanel.Initialize();
            if (gameSettingPanel != null) gameSettingPanel.Initialize();
            if (mapPanel != null) mapPanel.Initialize(LobbyManager.Instance?.MapRegistry);

            if (createButton != null) createButton.AddClickListener(OnClickCreate);
            if (closeButton != null) closeButton.AddClickListener(OnClickBack);
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            var lobby = LobbyManager.Instance;

            bool isEdit = GameManager.Instance != null
                && GameManager.Instance.CurrentState == GameState.Lobby;

            RoomOption startOption = isEdit ? lobby?.CurrentRoomOption : null;
            Setup(startOption, isEdit);

            _creating = false;
            if (closeButton != null) closeButton.interactable = true;

            SetStatusActive(false);

            if (lobby != null)
            {
                lobby.OnLobbyFailed -= HandleLobbyFailed;
                lobby.OnLobbyFailed += HandleLobbyFailed;
            }

            if (mapPanel != null)
            {
                mapPanel.OnMapSelected -= OnMapSelected;
                mapPanel.OnMapSelected += OnMapSelected;
                mapPanel.Refresh(lobby?.MapRegistry, _option.mapName);
            }

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

        public override void OnAfterHide()
        {
            base.OnAfterHide();

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyFailed -= HandleLobbyFailed;

            if (mapPanel != null)
                mapPanel.OnMapSelected -= OnMapSelected;

            SetStatusActive(false);
        }

        public void Setup(RoomOption option, bool isEditMode = false)
        {
            _option = option?.Clone() ?? new RoomOption();
            _isEditMode = isEditMode;

            // 패널들은 이 인스턴스를 참조로 들고 자기 담당 필드만 갱신한다.
            if (roomInfoPanel != null) roomInfoPanel.Bind(_option, _isEditMode);
            if (gameSettingPanel != null) gameSettingPanel.Bind(_option);

            RefreshCreateButtonUI();
        }

        private void SetStatusActive(bool active)
        {
            if (statusText != null) statusText.gameObject.SetActive(active);
        }

        private void RefreshCreateButtonUI()
        {
            if (createButtonText != null)
            {
                var localized = _isEditMode ? textData.editButtonLabel : textData.createButtonLabel;
                createButtonText.text = localized.GetLocalizedString();
            }

            if (createButton != null)
                createButton.interactable = true;
        }

        private void OnMapSelected(string mapId)
        {
            _option.mapName = mapId;
        }

        private void HandleLobbyFailed(LobbyFailureReason reason)
        {
            _creating = false;
            if (createButton != null) createButton.interactable = true;
            if (closeButton != null) closeButton.interactable = true;

            // 실패 피드백은 공통 경고 팝업으로 통일한다. ("생성 중" 상태 문구는 정리)
            SetStatusActive(false);

            UIManager.Instance.ShowPopup<UIwarningTextPopup>(p => p.SetReason(reason)).Forget();
        }

        public override void OnClickBack()
        {
            if (_creating) return;
            base.OnClickBack();
        }

        private void OnClickCreate()
        {
            if (roomInfoPanel != null && !roomInfoPanel.TryApply()) return;

            if (_isEditMode)
            {
                LobbyManager.Instance.UpdateLobbySettings(_option);
                UIManager.Instance.HidePopup().Forget();
                return;
            }

            _creating = true;
            if (createButton != null) createButton.interactable = false;
            if (closeButton != null) closeButton.interactable = false;
            SetStatusActive(true);
            LobbyManager.Instance.CreateLobby(_option);
        }
    }
}
