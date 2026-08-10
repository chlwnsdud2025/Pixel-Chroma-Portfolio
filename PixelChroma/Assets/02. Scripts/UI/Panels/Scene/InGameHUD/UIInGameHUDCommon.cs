using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    /// <summary>
    /// Runner / Seeker HUD가 공통으로 사용하는 UI 참조와 로직을 담는 컴포넌트입니다.
    /// Base 프리팹에 부착해 한 번만 인스펙터 배선하면, 각 Variant(UIInGameHUD_Runner/_Seeker)가
    /// 이 컴포넌트를 그대로 상속받아 재사용합니다. 씬 컴포넌트(UIInGameHUD)가 라이프사이클을 위임합니다.
    /// </summary>
    public class UIInGameHUDCommon : MonoBehaviour
    {
        [Header("Paint UI (Common)")]
        [SerializeField] private GameObject paintModeRoot;
        [SerializeField] private UIColorPicker colorPicker;

        [Header("Game State UI (Common)")]
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private UIPunchText timerText;
        [SerializeField] private TMP_Text forcedExposeTimerText;

        [Header("Result UI")]
        [SerializeField] private TMP_Text gameResultOverlayText;
        [SerializeField] private float resultPunchScale = 0.3f;
        [SerializeField] private float resultPunchDuration = 0.3f;

        [Header("Game Mode UI")]
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private GameModeRegistrySO gameModeRegistry;

        [Header("Player Count UI")]
        [SerializeField] private UIPlayerCountIcons runnerCountIcons;
        [SerializeField] private UIPlayerCountIcons seekerCountIcons;

        [Header("Spectator Mode UI (Common)")]
        [SerializeField] private UISpectatorModePanel spectatorPanel;

        [Header("Catch Log")]
        [SerializeField] private UICatchLog catchLog;

        [Header("Localization")]
        [SerializeField] private HUDTextDataSO textData;

        [Header("Setting Button")]
        [SerializeField] private Button settingButton;

        public UIColorPicker ColorPicker => colorPicker;
        public bool IsPaintModeActive => paintModeRoot != null && paintModeRoot.activeSelf;

        public void Initialize()
        {
            if (colorPicker != null)
                colorPicker.Initialize();

            if (settingButton != null) settingButton.AddClickListener(OnClickSetting);
        }

        public void SubscribeEvents()
        {
            GameRoundManager.OnTimeStateChanged += HandleTimeStateChanged;
            GameRoundManager.OnTimerTick += HandleTimerTick;
            GameRoundManager.OnWinnerDecided += HandleWinnerDecided;
            InGameManager.OnPlayerCountChanged += HandlePlayerCountChanged;
        }

        public void UnsubscribeEvents()
        {
            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChanged;
            GameRoundManager.OnTimerTick -= HandleTimerTick;
            GameRoundManager.OnWinnerDecided -= HandleWinnerDecided;
            InGameManager.OnPlayerCountChanged -= HandlePlayerCountChanged;
        }

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null) paintModeRoot.SetActive(active);
        }

        public void ResetHUDState()
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

        public void SetSpectateTarget(PlayerBase target, bool isSelf)
        {
            if (spectatorPanel == null) return;

            // 대상이 없으면 자유 시점입니다. 표시 문구는 HUD 텍스트 데이터가 가지고 있습니다.
            string displayName = target != null
                ? target.playerName
                : (textData != null ? textData.spectateFreeView.GetLocalizedString() : string.Empty);

            spectatorPanel.SetTargetName(displayName, isSelf);
        }

        public void SetLikeGuide(bool canLike)
        {
            if (spectatorPanel != null)
                spectatorPanel.SetLikeGuide(canLike);
        }

        public void SyncToCurrentState()
        {
            var round = GameRoundManager.Instance;
            if (round == null) return;

            UpdateStateUI(round.CurrentTimeState, round.CurrentTimer);

            if (modeText != null)
            {
                var mode = round.CurrentRule?.Mode ?? GameMode.Basic;
                var data = gameModeRegistry != null ? gameModeRegistry.Find(mode) : null;
                modeText.text = data != null ? data.displayName.GetLocalizedString() : "";
            }

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
            {
                var round = GameRoundManager.Instance;
                bool exposeOn = round != null && round.CurrentRoomOption != null
                    && round.CurrentRoomOption.forcedExposeEnabled;
                forcedExposeTimerText.gameObject.SetActive(state == TimeState.SeekTime && exposeOn);
            }

            UpdateTimerText(remainingSeconds);
        }

        private void ShowWinnerOverlay(PlayerRole winner)
        {
            if (gameResultOverlayText == null) return;

            gameResultOverlayText.gameObject.SetActive(true);
            gameResultOverlayText.text = winner == PlayerRole.Seeker
                ? $"<color=#FF2222>{textData.resultSeekerWin.GetLocalizedString()}</color>"
                : $"<color=#00FFFF>{textData.resultRunnerWin.GetLocalizedString()}</color>";

            PunchResultText();
        }

        private void PunchResultText()
        {
            if (gameResultOverlayText == null) return;

            var t = gameResultOverlayText.transform;
            t.DOKill();
            t.localScale = Vector3.one;
            t.DOPunchScale(Vector3.one * resultPunchScale, resultPunchDuration);
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
            ShowTransitionSequenceAsync(winner).Forget();
        }

        private async UniTaskVoid ShowTransitionSequenceAsync(PlayerRole winner)
        {
            if (gameResultOverlayText == null) return;

            gameResultOverlayText.gameObject.SetActive(true);
            gameResultOverlayText.text = winner == PlayerRole.Seeker
                ? textData.transitionAllCaught.GetLocalizedString()
                : textData.transitionTimeUp.GetLocalizedString();

            PunchResultText();

            await UniTask.Delay(TimeSpan.FromSeconds(2.5f));

            ShowWinnerOverlay(winner);
        }

        // --------------------- 클릭 이벤트-----------------------------------
        private void OnClickSetting()
        {
            UIManager.Instance.ShowPopup<UISetting>().Forget();
        }
    }
}
