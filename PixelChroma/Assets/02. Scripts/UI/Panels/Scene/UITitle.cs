using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UITitle : UIScene
    {
        [Header("Main Menu Buttons & Inputs")]
        [SerializeField] private Button openRoomSettingsButton; // (수정) 방 만들기 버튼 (누르면 패널 열림)
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Room Settings Panel")]
        [SerializeField] private GameObject roomSettingsPanel;  // (추가) 방 설정 팝업 패널 전체
        [SerializeField] private Toggle publicToggle;
        [SerializeField] private Slider maxPlayerSlider;
        [SerializeField] private TMP_Text maxPlayerText;
        [SerializeField] private Button confirmCreateButton;    // (추가) 패널 안의 최종 생성 버튼
        [SerializeField] private Button closeSettingsButton;    // (추가) 패널 안의 닫기/취소 버튼

        protected override void Awake()
        {
            base.Awake();

            // 메인 화면 버튼 이벤트
            if (openRoomSettingsButton != null) openRoomSettingsButton.onClick.AddListener(OnClickOpenSettings);
            if (quickJoinButton != null) quickJoinButton.onClick.AddListener(OnClickQuickJoin);
            if (joinButton != null) joinButton.onClick.AddListener(OnClickJoin);

            // 방 설정 패널 내부 버튼 이벤트
            if (confirmCreateButton != null) confirmCreateButton.onClick.AddListener(OnClickConfirmCreate);
            if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(OnClickCloseSettings);

            roomCodeInput.characterLimit = 6;
            roomCodeInput.onValueChanged.AddListener(v =>
                roomCodeInput.SetTextWithoutNotify(v.ToUpper()));

            if (maxPlayerSlider != null)
            {
                maxPlayerSlider.onValueChanged.AddListener(v =>
                    maxPlayerText.text = $"최대 인원: {v}명");
            }

            // (추가) 시작 시 방 설정 패널은 숨겨둡니다.
            if (roomSettingsPanel != null) roomSettingsPanel.SetActive(false);

            SetStatus("");
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();
            SetStatus(""); // 화면이 다시 켜질 때마다 남아있는 에러 텍스트 싹 비우기

            SetInteractable(true);
        }

        private void OnEnable()
        {
            LobbyManager.Instance.OnLobbyEntered += HandleLobbyEntered;
        }

        private void OnDisable()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnLobbyEntered -= HandleLobbyEntered;
        }

        // ── 버튼 핸들러 ────────────────────────────────────────

        // 1. 방 설정 패널 열기
        private void OnClickOpenSettings()
        {
            if (roomSettingsPanel != null)
            {
                roomSettingsPanel.SetActive(true);
                // 패널 열 때 현재 슬라이더 값으로 텍스트 갱신
                if (maxPlayerSlider != null && maxPlayerText != null)
                    maxPlayerText.text = $"최대 인원: {maxPlayerSlider.value}명";
            }
        }

        // 2. 방 설정 패널 닫기
        private void OnClickCloseSettings()
        {
            if (roomSettingsPanel != null) roomSettingsPanel.SetActive(false);
        }

        // 3. 진짜 방 생성 시작 (패널 내부 버튼)
        private void OnClickConfirmCreate()
        {
            SetInteractable(false);
            SetStatus("방 생성 중...");

            bool isPublic = publicToggle != null ? publicToggle.isOn : true;
            int maxPlayers = maxPlayerSlider != null ? (int)maxPlayerSlider.value : 8;

            LobbyManager.Instance.CreateLobby(isPublic, maxPlayers);
            if (roomSettingsPanel != null) roomSettingsPanel.SetActive(false);
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

        // ── 콜백 ───────────────────────────────────────────────

        private void HandleLobbyEntered(bool success)
        {
            if (success)
            {
                GameManager.Instance.EnterLobby().Forget();
                return;
            }

            SetStatus("방을 찾을 수 없거나 생성에 실패했습니다.");
            SetInteractable(true);
        }

        private void SetInteractable(bool value)
        {
            if (openRoomSettingsButton != null) openRoomSettingsButton.interactable = value;
            if (quickJoinButton != null) quickJoinButton.interactable = value;
            if (joinButton != null) joinButton.interactable = value;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }


    }
}