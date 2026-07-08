using UnityEngine;
using TMPro;
using Unity.VisualScripting;

namespace PixelChroma.UI
{
    public class UIInGameHUD : UIScene, IPaintHUD
    {
        [Header("Paint UI (Common)")]
        [SerializeField] protected GameObject paintModeRoot;
        [SerializeField] protected UIColorPicker colorPicker;

        [Header("Game State UI (Common)")]
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text timerText;

        [SerializeField] private TMP_Text gameResultOverlayText;

        public UIColorPicker ColorPicker => colorPicker;
        public bool IsPaintModeActive => paintModeRoot != null && paintModeRoot.activeSelf;

        public override void Initialize()
        {
            base.Initialize();
            if (paintModeRoot != null) paintModeRoot.SetActive(false);
            if (gameResultOverlayText != null) gameResultOverlayText.gameObject.SetActive(false);
        }

        protected virtual void Update()
        {
        }

        protected virtual void OnEnable()
        {
            GameRoundManager.OnTimeStateChanged += HandleTimeStateChanged;
            GameRoundManager.OnTimerTick += HandleTimerTick;
            GameRoundManager.OnWinnerDecided += HandleWinnerDecided;

            var round = GameRoundManager.Instance;
            if (round != null)
            {
                UpdateStateUI(round.CurrentTimeState, round.CurrentTimer);

                bool isResultState = round.CurrentTimeState == TimeState.CheckTime
                           || round.CurrentTimeState == TimeState.GameEnd;

                if (isResultState && round.CurrentWinner != PlayerRole.None)
                    ShowWinnerOverlay(round.CurrentWinner);
            }
        }

        protected virtual void OnDisable()
        {
            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChanged;
            GameRoundManager.OnTimerTick -= HandleTimerTick;
            GameRoundManager.OnWinnerDecided -= HandleWinnerDecided;
        }

        public void TogglePaintMode()
        {
            if (paintModeRoot != null) paintModeRoot.SetActive(!paintModeRoot.activeSelf);
        }

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null) paintModeRoot.SetActive(active);
        }

        private void HandleTimeStateChanged(TimeState state, int remainingSeconds)
        {
            UpdateStateUI(state, remainingSeconds);
        }

        private void HandleTimerTick(int remainingSeconds)
        {
            UpdateTimerText(remainingSeconds);
        }

        private void HandleWinnerDecided(PlayerRole winner)
        {
            ShowWinnerOverlay(winner);
        }

        private void UpdateStateUI(TimeState state, int remainingSeconds)
        {
            if (stateText != null)
            {
                switch (state)
                {
                    case TimeState.Waiting: stateText.text = "대기 중..."; break;
                    case TimeState.HideTime: stateText.text = "숨는 시간!"; break;
                    case TimeState.SeekTime: stateText.text = "찾는 시간!"; break;
                    case TimeState.CheckTime: stateText.text = "확인 시간!"; break;
                    case TimeState.GameEnd: stateText.text = "게임 종료!"; break;
                }
            }

            if (gameResultOverlayText != null && state != TimeState.CheckTime && state != TimeState.GameEnd)
                gameResultOverlayText.gameObject.SetActive(false);

            UpdateTimerText(remainingSeconds);
        }

        private void ShowWinnerOverlay(PlayerRole winner)
        {
            if (gameResultOverlayText == null) return;

            gameResultOverlayText.gameObject.SetActive(true);
            gameResultOverlayText.text = winner == PlayerRole.Seeker
                ? "<color=#FF2222>술래 승리!</color>"
                : "<color=#00FFFF>도망자 승리!</color>";
        }

        private void UpdateTimerText(int remainingSeconds)
        {
            if (timerText != null)
            {
                int display = Mathf.Max(0, remainingSeconds);
                int minutes = display / 60;
                int seconds = display % 60;
                timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
        }
    }
}
