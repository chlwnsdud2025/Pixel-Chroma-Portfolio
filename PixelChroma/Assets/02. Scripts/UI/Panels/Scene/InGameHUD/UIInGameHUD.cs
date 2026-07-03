using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIInGameHUD : UIScene, IPaintHUD
    {
        [Header("Paint UI")]
        [SerializeField] private GameObject paintModeRoot;
        [SerializeField] private UIColorPicker colorPicker;

        [Header("Game State UI")]
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text timerText;


        [Header("Interaction UI")]
        [SerializeField] private Button killButton;

        [Header("Skill UI")]
        [SerializeField] private GameObject waterGunRoot; // 물총 UI 전체 (술래에게만 보이게)
        [SerializeField] private Image waterGunCooldownOverlay; // 회색으로 덮일 쿨타임 이미지


        public UIColorPicker ColorPicker => colorPicker;
        public bool IsPaintModeActive => paintModeRoot != null && paintModeRoot.activeSelf;

        public override void Initialize()
        {
            base.Initialize();
            if (paintModeRoot != null)
                paintModeRoot.SetActive(false);

            if (killButton != null)
            {
                // 기본적으로 킬 버튼의 렌더링 자체를 꺼버립니다. (Runner에게 안 보이게 됨)
                killButton.gameObject.SetActive(false);
                killButton.interactable = false;
            }

            if (waterGunRoot != null)
                waterGunRoot.SetActive(false);
        }

        // Seeker만 호출할 수 있도록 킬 버튼의 표시 여부를 끄고 켜는 함수를 추가합니다.
        public void SetKillButtonVisible(bool isVisible)
        {
            if (killButton != null)
            {
                killButton.gameObject.SetActive(isVisible);
            }
        }

        // 술래(Seeker) 화면에만 물총 UI를 띄우는 함수
        public void SetWaterGunUIVisible(bool isVisible)
        {
            if (waterGunRoot != null)
            {
                waterGunRoot.SetActive(isVisible);
            }
        }

        public void UpdateWaterGunCooldown(float fillAmount)
        {
            if (waterGunCooldownOverlay != null)
            {
                waterGunCooldownOverlay.fillAmount = fillAmount;
            }
        }

        public void SetKillButtonInteractable(bool isInteractable, UnityEngine.Events.UnityAction onClickAction = null)
        {
            if (killButton == null) return;

            killButton.interactable = isInteractable;

            // 버튼 클릭 이벤트 갱신
            killButton.onClick.RemoveAllListeners();
            if (isInteractable && onClickAction != null)
            {
                killButton.onClick.AddListener(onClickAction);
            }
        }

        // ── 이벤트 구독 및 "최초 싱크 강제 맞춤" 로직 ──────────────────────
        private void OnEnable()
        {
            ChromaNetworkManager.OnTimeStateChanged += HandleTimeStateChanged;

            //  HUD가 켜지는 순간, 이미 NetworkManager에서 흐르고 있는 최신 시간 정보를 강제로 긁어옵니다.
            // 이렇게 하면 로딩이 늦어서 최초 전환 메시지를 놓친 클라이언트도 완벽하게 싱크가 맞춰집니다!
            var netManager = Mirror.NetworkManager.singleton as ChromaNetworkManager;
            if (netManager != null)
            {
                UpdateStateUI(netManager.CurrentTimeState, netManager.CurrentTimer);
            }
        }

        private void OnDisable()
        {
            ChromaNetworkManager.OnTimeStateChanged -= HandleTimeStateChanged;
        }
        // ─────────────────────────────────────────────────────────────────

        // 서버가 매 프레임 차감하고 있는 정밀한 남은 시간을 실시간으로 스크린에 반영합니다.
        private void Update()
        {
            var netManager = Mirror.NetworkManager.singleton as ChromaNetworkManager;
            if (netManager != null)
            {
                // 대기 중이거나 게임 종료가 아닐 때(시간이 흐르는 중일 때) 매 프레임 타이머 텍스트를 밀어줍니다.
                if (netManager.CurrentTimeState == TimeState.HideTime || netManager.CurrentTimeState == TimeState.SeekTime)
                {
                    UpdateTimerText(netManager.CurrentTimer);
                }
            }
        }

        public void TogglePaintMode()
        {
            if (paintModeRoot != null)
                paintModeRoot.SetActive(!paintModeRoot.activeSelf);
        }

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null)
                paintModeRoot.SetActive(active);
        }

        private void HandleTimeStateChanged(TimeState state, float remainingTime)
        {
            UpdateStateUI(state, remainingTime);
        }

        private void UpdateStateUI(TimeState state, float remainingTime)
        {
            // 상태 텍스트 변경
            if (stateText != null)
            {
                switch (state)
                {
                    case TimeState.Waiting:
                        stateText.text = "대기 중...";
                        break;
                    case TimeState.HideTime:
                        stateText.text = "숨는 시간!";
                        break;
                    case TimeState.SeekTime:
                        stateText.text = "찾는 시간!";
                        break;
                    case TimeState.GameEnd:
                        stateText.text = "게임 종료!";
                        break;
                }
            }

            // 타이머 텍스트 변경
            UpdateTimerText(remainingTime);
        }

        // 분리된 타이머 가독성 포맷팅 함수
        private void UpdateTimerText(float remainingTime)
        {
            if (timerText != null)
            {
                float displayTime = Mathf.Max(0, remainingTime);
                int minutes = Mathf.FloorToInt(displayTime / 60f);
                int seconds = Mathf.FloorToInt(displayTime % 60f);
                timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
        }
    }
}