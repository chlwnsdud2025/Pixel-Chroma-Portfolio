using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

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
        [SerializeField] private OneTextLabel stateText;
        [SerializeField] private UIPunchText timerText;
        [SerializeField] private OneTextLabel forcedExposeTimerText;

        [Header("Result UI")]
        [SerializeField] private CanvasGroup resultOverlayGroup;
        [SerializeField] private OneTextLabel gameResultOverlayText;
        [SerializeField] private float resultAppearDuration = 0.5f;
        [SerializeField] private float resultRiseOffset = 40f;
        [SerializeField] private float resultAppearStartScale = 0.8f;
        [SerializeField] private float resultHoldDuration = 2f;

        [Header("Score Board")]
        [SerializeField] private UIScoreBoard scoreBoard;

        [Header("Game Mode UI")]
        [SerializeField] private OneTextLabel modeText;
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

        [Header("Skip Vote")]
        [SerializeField] private GameObject skipVoteRoot;
        [SerializeField] private Button skipButton;
        [SerializeField] private Image skipHoldGauge;
        [SerializeField] private OneTextLabel skipButtonText;
        [SerializeField] private OneTextLabel skipVoteCountText;

        public UIColorPicker ColorPicker => colorPicker;
        public bool IsPaintModeActive => paintModeRoot != null && paintModeRoot.activeSelf;

        // 관전 모드 버튼 클릭. 로컬 플레이어가 구독해 실제 모드 전환을 수행합니다.
        public event Action<SpectateMode> OnSpectateModeSelected;

        private Vector2 resultBaseAnchoredPos;
        private PlayerBase _currentSpectateTarget; 
        private bool _currentSpectateIsSelf;

        public void Initialize()
        {
            if (colorPicker != null)
                colorPicker.Initialize();

            if (settingButton != null) settingButton.AddClickListener(OnClickSetting);

            if (resultOverlayGroup != null)
            {
                resultBaseAnchoredPos = ((RectTransform)resultOverlayGroup.transform).anchoredPosition;
                resultOverlayGroup.gameObject.SetActive(false);
            }
            if (scoreBoard != null) scoreBoard.Initialize();

            if (skipButton != null) skipButton.AddClickListener(OnClickSkipVote);
            if (skipVoteRoot != null) skipVoteRoot.SetActive(false);
            if (skipHoldGauge != null) skipHoldGauge.fillAmount = 0f;

            if (spectatorPanel != null)
            {
                // Initialize가 두 번 불려도 중복 구독되지 않도록 해제 후 구독합니다.
                spectatorPanel.OnModeSelected -= HandleSpectateModeSelected;
                spectatorPanel.OnModeSelected += HandleSpectateModeSelected;
            }
        }

        private void HandleSpectateModeSelected(SpectateMode mode)
        {
            OnSpectateModeSelected?.Invoke(mode);
        }

        public void SubscribeEvents()
        {
            GameRoundManager.OnTimeStateChanged += HandleTimeStateChanged;
            GameRoundManager.OnTimerTick += HandleTimerTick;
            GameRoundManager.OnWinnerDecided += HandleWinnerDecided;
            InGameManager.OnPlayerCountChanged += HandlePlayerCountChanged;
            GameSettings.Changed += OnSettingsChanged;
            GameRoundManager.OnSkipVoteUpdated += HandleSkipVoteUpdated;
            PlayerBase.OnLocalSkipHoldProgress += HandleSkipHoldProgress;
        }

        public void UnsubscribeEvents()
        {
            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChanged;
            GameRoundManager.OnTimerTick -= HandleTimerTick;
            GameRoundManager.OnWinnerDecided -= HandleWinnerDecided;
            InGameManager.OnPlayerCountChanged -= HandlePlayerCountChanged;
            GameSettings.Changed -= OnSettingsChanged;
            GameRoundManager.OnSkipVoteUpdated -= HandleSkipVoteUpdated;
            PlayerBase.OnLocalSkipHoldProgress -= HandleSkipHoldProgress;
        }

        public void SetPaintMode(bool active)
        {
            if (paintModeRoot != null) paintModeRoot.SetActive(active);

            if (scoreBoard != null) scoreBoard.SetVisible(!active);
        }

        public void ResetHUDState()
        {
            if (paintModeRoot != null) paintModeRoot.SetActive(false);
            if (resultOverlayGroup != null) resultOverlayGroup.gameObject.SetActive(false);
            if (spectatorPanel != null) spectatorPanel.Hide();
            if (catchLog != null) catchLog.ClearAll();
            if (skipVoteRoot != null) skipVoteRoot.SetActive(false);
            if (skipHoldGauge != null) skipHoldGauge.fillAmount = 0f;
            if (skipVoteCountText != null) skipVoteCountText.text = "";
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
            _currentSpectateTarget = target;
            _currentSpectateIsSelf = isSelf;

            if (spectatorPanel == null) return;

            // 대상이 없으면 자유 시점입니다. 표시 문구는 HUD 텍스트 데이터가 가지고 있습니다.
            string displayName = target != null
                ? target.playerName
                : (textData != null ? textData.spectateFreeView.GetLocalizedString() : string.Empty);

            byte[] pixelPng = PixelNicknameHelper.GetPixelData(target);
            spectatorPanel.SetTargetName(displayName, isSelf, pixelPng);
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

            // 스킵 투표 UI: HideTime, CheckTime에서만 표시
            if (skipVoteRoot != null)
            {
                bool showSkip = state == TimeState.HideTime || state == TimeState.CheckTime;
                skipVoteRoot.SetActive(showSkip);

                // 상태 전환 시 UI 초기화
                if (showSkip)
                {
                    if (skipButton != null) skipButton.interactable = true;
                    if (skipButtonText != null) skipButtonText.text = textData.skipVoteLabel.GetLocalizedString();
                    if (skipHoldGauge != null) skipHoldGauge.fillAmount = 0f;

                    var round = GameRoundManager.Instance;
                    if (skipVoteCountText != null && round != null)
                        skipVoteCountText.text = $"(0/{round.RequiredSkipVotes})";
                }
            }
        }

        private void ShowWinnerOverlay(PlayerRole winner)
        {
            if (resultOverlayGroup == null || gameResultOverlayText == null) return;

            gameResultOverlayText.text = winner == PlayerRole.Seeker
                ? textData.resultSeekerWin.GetLocalizedString()
                : textData.resultRunnerWin.GetLocalizedString();

            // 시작 상태: 투명 + 아래로 살짝 + 작은 스케일
            var rect = (RectTransform)resultOverlayGroup.transform;
            var startPos = resultBaseAnchoredPos + Vector2.down * resultRiseOffset;
            var startScale = Vector3.one * resultAppearStartScale;

            resultOverlayGroup.DOKill();
            rect.DOKill();

            resultOverlayGroup.gameObject.SetActive(true);
            resultOverlayGroup.alpha = 0f;
            rect.anchoredPosition = startPos;
            rect.localScale = startScale;

            // 등장(페이드/라이즈/스케일 업) → N초 유지 → 퇴장(반대로) → 비활성화
            DOTween.Sequence().SetTarget(resultOverlayGroup)
                .Append(resultOverlayGroup.DOFade(1f, resultAppearDuration).SetEase(Ease.OutCubic))
                .Join(rect.DOAnchorPos(resultBaseAnchoredPos, resultAppearDuration).SetEase(Ease.OutCubic))
                .Join(rect.DOScale(Vector3.one, resultAppearDuration).SetEase(Ease.OutCubic))
                .AppendInterval(resultHoldDuration)
                .Append(resultOverlayGroup.DOFade(0f, resultAppearDuration).SetEase(Ease.InCubic))
                .Join(rect.DOAnchorPos(startPos, resultAppearDuration).SetEase(Ease.InCubic))
                .Join(rect.DOScale(startScale, resultAppearDuration).SetEase(Ease.InCubic))
                .OnComplete(() => resultOverlayGroup.gameObject.SetActive(false));
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
        private void OnSettingsChanged()
        {
            if (spectatorPanel != null && spectatorPanel.gameObject.activeInHierarchy)
                SetSpectateTarget(_currentSpectateTarget, _currentSpectateIsSelf);
        }

        // --------------------- 클릭 이벤트-----------------------------------
        private void OnClickSetting()
        {
            UIManager.Instance.ShowPopup<UISetting>().Forget();
        }

        // --------------------- 스킵 투표 -----------------------------------

        private void HandleSkipVoteUpdated(int currentVotes, int requiredVotes)
        {
            if (skipVoteCountText != null)
                skipVoteCountText.text = $"({currentVotes}/{requiredVotes})";
        }

        private void HandleSkipHoldProgress(float progress)
        {
            if (skipHoldGauge != null)
                skipHoldGauge.fillAmount = progress;
        }

        public void SetSkipVoted()
        {
            if (skipButton != null) skipButton.interactable = false;
            if (skipButtonText != null) skipButtonText.text = textData.skipVoteVotedLabel.GetLocalizedString();
            if (skipHoldGauge != null) skipHoldGauge.fillAmount = 1f;
        }

        private void OnClickSkipVote()
        {
            NetworkClient.Send(new SkipVoteRequest());
            SetSkipVoted();  // 공용 메서드 호출
        }



    }
}
