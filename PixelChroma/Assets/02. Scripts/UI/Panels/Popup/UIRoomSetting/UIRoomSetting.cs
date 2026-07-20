using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
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
        [SerializeField] private LocalizedString createButtonLocalized;
        [SerializeField] private LocalizedString editButtonLocalized;

        [Header("Close")]
        [SerializeField] private Button closeButton;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        private RoomOption _option;
        private bool _isEditMode = false;

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

        protected override UniTask OnShow()
        {
            if (mapPanel != null)
            {
                mapPanel.OnMapSelected -= OnMapSelected;
                mapPanel.OnMapSelected += OnMapSelected;
            }

            var lobby = LobbyManager.Instance;
            if (lobby != null)
                lobby.OnLobbyFailed += HandleLobbyFailed;

            return UniTask.CompletedTask;
        }

        protected override UniTask OnHide()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyFailed -= HandleLobbyFailed;

            if (mapPanel != null)
                mapPanel.OnMapSelected -= OnMapSelected;

            return UniTask.CompletedTask;
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

        private void OnMapSelected(MapDataSO map)
        {
            _option.mapName = map.name;
        }

        private void HandleLobbyFailed()
        {
            if (createButton != null) createButton.interactable = true;
        }

        private void RefreshCreateButtonUI()
        {
            if (createButtonText != null)
            {
                var localized = _isEditMode ? editButtonLocalized : createButtonLocalized;
                createButtonText.text = localized.GetLocalizedString();
            }

            if (createButton != null)
                createButton.interactable = true;
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

            if (createButton != null) createButton.interactable = false;
            LobbyManager.Instance.CreateLobby(_option);
        }
    }
}
