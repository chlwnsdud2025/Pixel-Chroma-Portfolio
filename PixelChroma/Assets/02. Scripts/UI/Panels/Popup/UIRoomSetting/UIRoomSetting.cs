using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
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
        [SerializeField] private TMP_Text createButtonText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Color statusNormalColor = Color.white;
        [SerializeField] private Color statusFailedColor = Color.red;
        [SerializeField] private float statusFailedHideDelay = 2f;   // 실패 문구 유지 시간(초)
        [SerializeField] private float statusFadeDuration = 0.4f;    // 사라질 때 페이드아웃 시간(초)

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

        private Sequence _statusHideSeq;

        public override bool CloseOnEscape => !_creating;

        public override void Initialize()
        {
            base.Initialize();

            if (roomInfoPanel != null) roomInfoPanel.Initialize();
            if (gameSettingPanel != null) gameSettingPanel.Initialize();
            if (mapPanel != null) mapPanel.Initialize(LobbyManager.Instance?.MapRegistry);

            if (createButton != null) createButton.onClick.AddListener(OnClickCreate);
            if (closeButton != null) closeButton.onClick.AddListener(OnClickBack);
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

            CancelStatusHide();
            SetStatusVisible(false);

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

            CancelStatusHide();
        }

        public void Setup(RoomOption option, bool isEditMode = false)
        {
            _option = option?.Clone() ?? new RoomOption();
            _isEditMode = isEditMode;

            // 패널들은 이 인스턴스를 참조로 들고 자기 담당 필드만 갱신한다.
            if (roomInfoPanel != null) roomInfoPanel.Bind(_option);
            if (gameSettingPanel != null) gameSettingPanel.Bind(_option);

            RefreshCreateButtonUI();
        }

        private void ShowCreatingStatus()
        {
            CancelStatusHide();
            SetStatus(textData.creatingStatus.GetLocalizedString(), statusNormalColor);
        }

        private void ShowFailedStatus()
        {
            CancelStatusHide();
            SetStatus(textData.createFailedStatus.GetLocalizedString(), statusFailedColor);
            if (statusText == null) return;

            // 경고 피드백
            statusText.transform.DOComplete();
            statusText.transform.DOShakePosition(0.2f, 5f, 10).SetUpdate(true);

            ScheduleHide();
        }

        private void SetStatus(string message, Color color)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.color = color;
            statusText.alpha = 1f;
            statusText.gameObject.SetActive(true);
        }

        private void ScheduleHide()
        {
            if (statusText == null || statusFailedHideDelay <= 0f) return;

            _statusHideSeq = DOTween.Sequence().SetUpdate(true)
                .AppendInterval(statusFailedHideDelay)
                .Append(DOTween.To(() => statusText.alpha, a => statusText.alpha = a, 0f, statusFadeDuration))
                .OnComplete(() =>
                {
                    _statusHideSeq = null;
                    if (statusText == null) return;
                    statusText.gameObject.SetActive(false);
                    statusText.alpha = 1f;
                });
        }

        private void CancelStatusHide()
        {
            if (_statusHideSeq == null) return;
            _statusHideSeq.Kill();
            _statusHideSeq = null;
            if (statusText != null) statusText.alpha = 1f;   // 페이드 중 취소됐을 수 있으니 복원
        }

        private void SetStatusVisible(bool visible)
        {
            if (statusText != null) statusText.gameObject.SetActive(visible);
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

        private void OnMapSelected(MapDataSO map)
        {
            _option.mapName = map.name;
        }

        private void HandleLobbyFailed()
        {
            _creating = false;
            if (createButton != null) createButton.interactable = true;
            if (closeButton != null) closeButton.interactable = true;
            ShowFailedStatus();
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
            ShowCreatingStatus();
            LobbyManager.Instance.CreateLobby(_option);
        }
    }
}
