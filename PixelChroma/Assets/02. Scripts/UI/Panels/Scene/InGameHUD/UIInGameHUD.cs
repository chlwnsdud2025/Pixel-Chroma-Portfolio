using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace PixelChroma.UI
{
    public class UIInGameHUD : UIScene, IPaintHUD
    {
        [Header("Paint UI (Common)")]
        [SerializeField] protected GameObject paintModeRoot;
        [SerializeField] protected UIColorPicker colorPicker;

        [Header("Game State UI (Common)")]
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private UIPunchText timerText;
        [SerializeField] private TMP_Text forcedExposeTimerText;

        [SerializeField] private TMP_Text gameResultOverlayText;

        [Header("Game Mode UI")]
        [SerializeField] private TMP_Text modeText;

        [Header("Player Count UI")]
        [SerializeField] private UIPlayerCountIcons runnerCountIcons;
        [SerializeField] private UIPlayerCountIcons seekerCountIcons;

        [Header("Spectator Mode UI (Common)")]
        [SerializeField] private UISpectatorModePanel spectatorPanel;

        [Header("Catch Log")]
        [SerializeField] private UICatchLog catchLog;

        [Header("Localization")]
        [SerializeField] private HUDTextDataSO textData;

        public UIColorPicker ColorPicker => colorPicker;
        public bool IsPaintModeActive => paintModeRoot != null && paintModeRoot.activeSelf;

        protected virtual void Update()
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            ResetHUDState();
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            GameRoundManager.OnTimeStateChanged += HandleTimeStateChanged;
            GameRoundManager.OnTimerTick += HandleTimerTick;
            GameRoundManager.OnWinnerDecided += HandleWinnerDecided;
            InGameManager.OnPlayerCountChanged += HandlePlayerCountChanged;
        }

        public override void OnAfterHide()
        {
            base.OnAfterHide();

            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChanged;
            GameRoundManager.OnTimerTick -= HandleTimerTick;
            GameRoundManager.OnWinnerDecided -= HandleWinnerDecided;
            InGameManager.OnPlayerCountChanged -= HandlePlayerCountChanged;
        }

        protected override UniTask OnShow()
        {
            ResetHUDState();
            SyncToCurrentState();
            return base.OnShow();
        }

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null) paintModeRoot.SetActive(active);
        }

        protected virtual void ResetHUDState()
        {
            if (paintModeRoot != null) paintModeRoot.SetActive(false);
            if (gameResultOverlayText != null) gameResultOverlayText.gameObject.SetActive(false);
            if (spectatorPanel != null) spectatorPanel.Hide();
            if (catchLog != null) catchLog.ClearAll();
        }

        public void SetSpectatorMode(bool active)
        {
            if (spectatorPanel == null) return;

            if (active) spectatorPanel.Show();
            else spectatorPanel.Hide();
        }

        public void SetActiveSpectateMode(SpectateMode mode)
        {
            if (spectatorPanel != null)
                spectatorPanel.SetActiveMode(mode);
        }

        public void SetSpectateTargetName(string name, bool isSelf)
        {
            if (spectatorPanel != null)
                spectatorPanel.SetTargetName(name, isSelf);
        }

        private void SyncToCurrentState()
        {
            var round = GameRoundManager.Instance;
            if (round == null) return;

            UpdateStateUI(round.CurrentTimeState, round.CurrentTimer);

            if (modeText != null)
                modeText.text = round.CurrentRule?.DisplayName ?? "";

            var inGame = InGameManager.Instance;
            if (inGame != null)
                HandlePlayerCountChanged(inGame.GetAliveRunnerCount(), inGame.GetAliveSeekerCount());
        }

        private void HandlePlayerCountChanged(int aliveRunners, int aliveSeekers)
        {
            if (runnerCountIcons != null) runnerCountIcons.SetCount(aliveRunners);
            if (seekerCountIcons != null) seekerCountIcons.SetCount(aliveSeekers);
        }

        private void UpdateStateUI(TimeState state, int remainingSeconds)
        {
            if (stateText != null)
            {
                stateText.text = state switch
                {
                    TimeState.Waiting => textData.stateWaiting.GetLocalizedString(),
                    TimeState.HideTime => textData.stateHideTime.GetLocalizedString(),
                    TimeState.SeekTime => textData.stateSeekTime.GetLocalizedString(),
                    TimeState.CheckTime => textData.stateCheckTime.GetLocalizedString(),
                    TimeState.GameEnd => textData.stateGameEnd.GetLocalizedString(),
                    _ => ""
                };
            }

            if (forcedExposeTimerText != null)
                forcedExposeTimerText.gameObject.SetActive(state == TimeState.SeekTime);

            UpdateTimerText(remainingSeconds);
        }

        private void ShowWinnerOverlay(PlayerRole winner)
        {
            if (gameResultOverlayText == null) return;

            gameResultOverlayText.gameObject.SetActive(true);
            gameResultOverlayText.text = winner == PlayerRole.Seeker
                ? $"<color=#FF2222>{textData.resultSeekerWin.GetLocalizedString()}</color>"
                : $"<color=#00FFFF>{textData.resultRunnerWin.GetLocalizedString()}</color>";
        }

        private void UpdateTimerText(int remainingSeconds)
        {
            if (timerText != null)
                timerText.SetText(Mathf.Max(0, remainingSeconds).ToString());
        }

        private void HandleTimeStateChanged(TimeState state, int remainingSeconds)
        {
            UpdateStateUI(state, remainingSeconds);
        }

        private void HandleTimerTick(int remainingSeconds)
        {
            UpdateTimerText(remainingSeconds);

            if (forcedExposeTimerText != null)
            {
                var round = GameRoundManager.Instance;
                forcedExposeTimerText.text = round != null ? round.SecondsUntilNextForcedExpose.ToString() : "";
            }
        }

        private void HandleWinnerDecided(PlayerRole winner)
        {
            ShowWinnerOverlay(winner);
        }
    }
}