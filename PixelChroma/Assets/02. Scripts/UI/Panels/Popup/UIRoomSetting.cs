using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIRoomSetting : UIPopup
    {
        [Header("Game Mode")]
        [SerializeField] private Button basicModeButton;
        [SerializeField] private Button infectionModeButton;

        [Header("Max Players")]
        [SerializeField] private Button maxPlayersMinusButton;
        [SerializeField] private Button maxPlayersPlusButton;
        [SerializeField] private TMP_Text maxPlayersText;

        [Header("Seeker Count")]
        [SerializeField] private Button seekerCountMinusButton;
        [SerializeField] private Button seekerCountPlusButton;
        [SerializeField] private TMP_Text seekerCountText;

        [Header("Hide Time")]
        [SerializeField] private Slider hideTimeSlider;
        [SerializeField] private TMP_Text hideTimeText;

        [Header("Seek Time")]
        [SerializeField] private Slider seekTimeSlider;
        [SerializeField] private TMP_Text seekTimeText;

        [Header("Public / Private")]
        [SerializeField] private Button publicButton;
        [SerializeField] private Button privateButton;

        [Header("Map Selection")]
        [SerializeField] private Image mapPreviewImage;
        [SerializeField] private Transform mapListContent;
        [SerializeField] private GameObject mapItemPrefab;

        [Header("Create")]
        [SerializeField] private Button createButton;
        [SerializeField] private TMP_Text createButtonText;

        private RoomOption _option;
        private List<MapEntry> _maps = new();
        private int _selectedMapIndex = -1;
        private bool _isEditMode = false;

        public override void Initialize()
        {
            base.Initialize();

            if (basicModeButton != null) basicModeButton.onClick.AddListener(() => SetGameMode(GameMode.Basic));
            if (infectionModeButton != null) infectionModeButton.onClick.AddListener(() => SetGameMode(GameMode.Infection));

            if (maxPlayersMinusButton != null) maxPlayersMinusButton.onClick.AddListener(() => AdjustMaxPlayers(-1));
            if (maxPlayersPlusButton != null) maxPlayersPlusButton.onClick.AddListener(() => AdjustMaxPlayers(1));

            if (seekerCountMinusButton != null) seekerCountMinusButton.onClick.AddListener(() => AdjustSeekerCount(-1));
            if (seekerCountPlusButton != null) seekerCountPlusButton.onClick.AddListener(() => AdjustSeekerCount(1));

            if (publicButton != null) publicButton.onClick.AddListener(() => SetPublic(true));
            if (privateButton != null) privateButton.onClick.AddListener(() => SetPublic(false));

            if (hideTimeSlider != null)
            {
                hideTimeSlider.minValue = RoomOption.MinHideTime;
                hideTimeSlider.maxValue = RoomOption.MaxHideTime;
                hideTimeSlider.wholeNumbers = true;
                hideTimeSlider.onValueChanged.AddListener(OnHideTimeChanged);
            }

            if (seekTimeSlider != null)
            {
                seekTimeSlider.minValue = RoomOption.MinSeekTime;
                seekTimeSlider.maxValue = RoomOption.MaxSeekTime;
                seekTimeSlider.wholeNumbers = true;
                seekTimeSlider.onValueChanged.AddListener(OnSeekTimeChanged);
            }

            if (createButton != null) createButton.onClick.AddListener(OnClickCreate);
        }

        protected override UniTask OnShow()
        {
            var lobby = LobbyManager.Instance;

            bool isEdit = GameManager.Instance != null
                && GameManager.Instance.CurrentState == GameState.Lobby;

            // 편집 모드는 현재 방 설정에서, 생성 모드는 항상 기본값에서 시작한다.
            // (생성 모드에 null을 넘기면 Setup에서 new RoomOption() 기본값이 만들어진다.)
            RoomOption startOption = isEdit ? lobby?.CurrentRoomOption : null;
            Setup(startOption, isEdit);
            SetMaps(lobby?.AvailableMaps);

            if (lobby != null)
                lobby.OnLobbyFailed += HandleLobbyFailed;

            return UniTask.CompletedTask;
        }

        protected override UniTask OnHide()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyFailed -= HandleLobbyFailed;

            return UniTask.CompletedTask;
        }

        public void Setup(RoomOption option, bool isEditMode = false)
        {
            _option = option?.Clone() ?? new RoomOption();
            _isEditMode = isEditMode;

            RefreshAllUI();
        }

        public void SetMaps(List<MapEntry> maps)
        {
            _maps = maps ?? new List<MapEntry>();
            RebuildMapList();

            if (_maps.Count > 0)
            {
                int idx = string.IsNullOrEmpty(_option.mapName)
                    ? 0
                    : _maps.FindIndex(m => m.name == _option.mapName);
                SelectMap(Mathf.Max(0, idx));
            }
        }

        private void SetPublic(bool isPublic)
        {
            _option.isPublic = isPublic;
            RefreshPublicUI();
        }

        private void SetGameMode(GameMode mode)
        {
            _option.gameMode = mode;
            RefreshGameModeUI();
        }

        private void AdjustMaxPlayers(int delta)
        {
            _option.maxPlayers = Mathf.Clamp(
                _option.maxPlayers + delta,
                RoomOption.MinPlayers,
                RoomOption.MaxPlayersLimit);

            int maxSeeker = _option.maxPlayers - 1;
            if (_option.seekerCount > maxSeeker)
                _option.seekerCount = Mathf.Max(RoomOption.MinSeekerCount, maxSeeker);

            RefreshPlayerCountUI();
        }

        private void AdjustSeekerCount(int delta)
        {
            int maxSeeker = Mathf.Min(RoomOption.MaxSeekerCount, _option.maxPlayers - 1);
            _option.seekerCount = Mathf.Clamp(
                _option.seekerCount + delta,
                RoomOption.MinSeekerCount,
                maxSeeker);

            RefreshPlayerCountUI();
        }

        private void OnHideTimeChanged(float value)
        {
            _option.hideTime = value;
            RefreshTimeUI();
        }

        private void OnSeekTimeChanged(float value)
        {
            _option.seekTime = value;
            RefreshTimeUI();
        }

        private void SelectMap(int index)
        {
            if (index < 0 || index >= _maps.Count) return;

            _selectedMapIndex = index;
            _option.mapName = _maps[index].name;

            if (mapPreviewImage != null && _maps[index].preview != null)
                mapPreviewImage.sprite = _maps[index].preview;

            RefreshMapListHighlight();
        }

        private void HandleLobbyFailed()
        {
            if (createButton != null) createButton.interactable = true;
        }

        private void RefreshAllUI()
        {
            RefreshPublicUI();
            RefreshGameModeUI();
            RefreshPlayerCountUI();
            RefreshTimeUI();
            RefreshCreateButtonUI();

            if (hideTimeSlider != null) hideTimeSlider.SetValueWithoutNotify(_option.hideTime);
            if (seekTimeSlider != null) seekTimeSlider.SetValueWithoutNotify(_option.seekTime);
        }

        private void RefreshPublicUI()
        {
            SetButtonSelected(publicButton, _option.isPublic);
            SetButtonSelected(privateButton, !_option.isPublic);
        }

        private void RefreshGameModeUI()
        {
            SetButtonSelected(basicModeButton, _option.gameMode == GameMode.Basic);
            SetButtonSelected(infectionModeButton, _option.gameMode == GameMode.Infection);
        }

        private void RefreshPlayerCountUI()
        {
            if (maxPlayersText != null) maxPlayersText.text = _option.maxPlayers.ToString();
            if (seekerCountText != null) seekerCountText.text = _option.seekerCount.ToString();
        }

        private void RefreshTimeUI()
        {
            if (hideTimeText != null) hideTimeText.text = _option.hideTime.ToString("0");
            if (seekTimeText != null) seekTimeText.text = _option.seekTime.ToString("0");
        }

        private void RefreshCreateButtonUI()
        {
            // 💡 수정 모드일 때와 생성 모드일 때 하단 버튼의 글자를 동적으로 바꿉니다!
            if (createButtonText != null)
                createButtonText.text = _isEditMode ? "변경" : "생성";

            // 팝업을 다시 열 때 이전 생성 시도로 꺼진 버튼을 항상 활성 상태로 복구한다.
            if (createButton != null)
                createButton.interactable = true;
        }

        private void RebuildMapList()
        {
            if (mapListContent == null || mapItemPrefab == null) return;

            for (int i = mapListContent.childCount - 1; i >= 0; i--)
                Destroy(mapListContent.GetChild(i).gameObject);

            for (int i = 0; i < _maps.Count; i++)
            {
                var go = Instantiate(mapItemPrefab, mapListContent);
                var text = go.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = _maps[i].name;

                int index = i;
                var button = go.GetComponent<Button>();
                if (button != null) button.onClick.AddListener(() => SelectMap(index));
            }
        }

        private void RefreshMapListHighlight()
        {
            if (mapListContent == null) return;

            for (int i = 0; i < mapListContent.childCount; i++)
            {
                var button = mapListContent.GetChild(i).GetComponent<Button>();
                if (button == null) continue;

                var colors = button.colors;
                colors.normalColor = (i == _selectedMapIndex)
                    ? new Color(0.4f, 0.4f, 0.4f)
                    : new Color(0.2f, 0.2f, 0.2f);
                button.colors = colors;
            }
        }

        private static void SetButtonSelected(Button button, bool selected)
        {
            if (button == null) 
                return;

            var outline = button.GetComponent<Outline>();
            if (outline != null) 
                outline.enabled = selected;
        }

        private void OnClickCreate()
        {
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

    [System.Serializable]
    public class MapEntry
    {
        public string name;
        public string sceneName;
        public Sprite preview;
    }
}
