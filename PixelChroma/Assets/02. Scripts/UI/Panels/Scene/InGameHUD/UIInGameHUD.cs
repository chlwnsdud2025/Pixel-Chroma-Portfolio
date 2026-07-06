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
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text timerText;

        [SerializeField] private TMP_Text gameResultOverlayText;

        public UIColorPicker ColorPicker => colorPicker;
        public bool IsPaintModeActive => paintModeRoot != null && paintModeRoot.activeSelf;

        public override void Initialize()
        {
            base.Initialize();
            if (paintModeRoot != null) paintModeRoot.SetActive(false);
            if (gameResultOverlayText != null) gameResultOverlayText.gameObject.SetActive(false); // 초기엔 숨김
        }

        protected virtual void OnEnable()
        {
            GameRoundManager.OnTimeStateChanged += HandleTimeStateChanged;

            var round = GameRoundManager.Instance;
            if (round != null)
            {
                UpdateStateUI(round.CurrentTimeState, round.CurrentTimer, round.CurrentWinner);
            }
        }

        protected virtual void OnDisable()
        {
            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChanged;
        }

        protected virtual void Update()
        {
            var round = GameRoundManager.Instance;
            if (round != null)
            {
                if (round.CurrentTimeState == TimeState.HideTime ||
                    round.CurrentTimeState == TimeState.SeekTime ||
                    round.CurrentTimeState == TimeState.CheckTime)
                {
                    UpdateTimerText(round.CurrentTimer);
                }
            }
        }

        public void TogglePaintMode()
        {
            if (paintModeRoot != null) paintModeRoot.SetActive(!paintModeRoot.activeSelf);
        }

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null) paintModeRoot.SetActive(active);
        }

        private void HandleTimeStateChanged(TimeState state, float remainingTime, WinFaction winner)
        {
            UpdateStateUI(state, remainingTime, winner);
        }

        private void UpdateStateUI(TimeState state, float remainingTime, WinFaction winner)
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

            // 💡 [승리 오버레이 연출 분기점]
            if (gameResultOverlayText != null)
            {
                if (state == TimeState.CheckTime)
                {
                    // 찾는 시간이 끝났을 때 도망자가 한 명이라도 살았다면 "도망자 승리!" 텍스트 가시화
                    gameResultOverlayText.gameObject.SetActive(true);
                    gameResultOverlayText.text = "<color=#00FFFF>도망자 승리!</color>";
                }
                else if (state == TimeState.GameEnd)
                {
                    gameResultOverlayText.gameObject.SetActive(true);
                    if (winner == WinFaction.Seeker)
                    {
                        gameResultOverlayText.text = "<color=#FF2222>술래 승리!</color>";
                    }
                    else if (winner == WinFaction.Runner)
                    {
                        gameResultOverlayText.text = "<color=#00FFFF>도망자 승리!</color>";
                    }
                }
                else
                {
                    // 평상시엔 가려두기
                    gameResultOverlayText.gameObject.SetActive(false);
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