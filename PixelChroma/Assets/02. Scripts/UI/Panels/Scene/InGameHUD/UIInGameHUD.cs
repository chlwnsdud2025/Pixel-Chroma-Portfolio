using UnityEngine;
using TMPro;

namespace PixelChroma.UI
{
    public class UIInGameHUD : UIScene, IPaintHUD
    {
        [Header("Paint UI (Common)")]
        [SerializeField] protected GameObject paintModeRoot;
        [SerializeField] protected UIColorPicker colorPicker;

        [Header("Game State UI (Common)")]
        [SerializeField] protected TMP_Text stateText;
        [SerializeField] protected TMP_Text timerText;

        public UIColorPicker ColorPicker => colorPicker;
        public bool IsPaintModeActive => paintModeRoot != null && paintModeRoot.activeSelf;

        public override void Initialize()
        {
            base.Initialize();
            if (paintModeRoot != null)
                paintModeRoot.SetActive(false);
        }

        protected virtual void OnEnable()
        {
            ChromaNetworkManager.OnTimeStateChanged += HandleTimeStateChanged;

            // 최초 씬 진입 시 시간 싱크 맞춤
            var netManager = Mirror.NetworkManager.singleton as ChromaNetworkManager;
            if (netManager != null)
            {
                UpdateStateUI(netManager.CurrentTimeState, netManager.CurrentTimer);
            }
        }

        protected virtual void OnDisable()
        {
            ChromaNetworkManager.OnTimeStateChanged -= HandleTimeStateChanged;
        }

        // 💡 자식 클래스들이 Update를 오버라이드해서 각자의 쿨타임을 깎을 수 있도록 virtual 선언
        protected virtual void Update()
        {
            var netManager = Mirror.NetworkManager.singleton as ChromaNetworkManager;
            if (netManager != null)
            {
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
            if (stateText != null)
            {
                switch (state)
                {
                    case TimeState.Waiting: stateText.text = "대기 중..."; break;
                    case TimeState.HideTime: stateText.text = "숨는 시간!"; break;
                    case TimeState.SeekTime: stateText.text = "찾는 시간!"; break;
                    case TimeState.GameEnd: stateText.text = "게임 종료!"; break;
                }
            }
            UpdateTimerText(remainingTime);
        }

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