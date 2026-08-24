using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Mirror;
using Cysharp.Threading.Tasks;
using System.Linq;

namespace PixelChroma
{
    public class GameRoundManager : MonoSingleton<GameRoundManager>
    {
        private const float CountdownDuration = 10f;

        private const float LobbyReturnDelayOnCheckEnd = 0.5f;

        public static event Action<TimeState, int> OnTimeStateChanged;
        public static event Action<PlayerRole> OnWinnerDecided;
        public static event Action<int> OnTimerTick;

        private readonly Dictionary<int, PlayerRole> _assignedRoles = new();
        private bool _isLobbyReturningStarted = false;
        private CancellationTokenSource _timerCts;

        [Header("forced Expose")]
        [SerializeField] private float forcedExposeDuration;

        public RoomOption CurrentRoomOption { get; private set; }
        public TimeState CurrentTimeState { get; private set; } = TimeState.Waiting;
        public PlayerRole CurrentWinner { get; private set; } = PlayerRole.None;
        public int CurrentTimer { get; private set; }
        public GameModeRule CurrentRule
        {
            get
            {
                var mode = CurrentRoomOption?.gameMode ?? GameMode.Basic;
                return GameModeRuleFactory.Create(mode);
            }
        }

        private float OptionHideTime => CurrentRoomOption?.hideTime ?? RoomOption.DefaultHideTime;
        private float OptionSeekTime => CurrentRoomOption?.seekTime ?? RoomOption.DefaultSeekTime;
        private float OptionCheckTime => CurrentRoomOption?.checkTime ?? RoomOption.DefaultCheckTime;
        private int OptionSeekerCount => CurrentRoomOption?.seekerCount ?? RoomOption.DefaultSeekerCount;
        private float OptionForcedExposeInterval => CurrentRoomOption?.forcedExposeInterval ?? RoomOption.DefaultForcedExposeInterval;
        private bool OptionForcedExposeEnabled => CurrentRoomOption?.forcedExposeEnabled ?? true;

        public int SecondsUntilNextForcedExpose
        {
            get
            {
                if (!OptionForcedExposeEnabled) return 0;
                if (CurrentTimeState != TimeState.SeekTime || OptionForcedExposeInterval <= 0f) return 0;

                int elapsed = Mathf.CeilToInt(OptionSeekTime) - CurrentTimer;
                int intervalInt = Mathf.Max(1, Mathf.RoundToInt(OptionForcedExposeInterval));
                int remainder = elapsed % intervalInt;
                return remainder == 0 ? intervalInt : intervalInt - remainder;
            }
        }

        // ── Unity 생명주기 ──────────────────────────────────────────────

        private void OnDestroy()
        {
            CancelTimer();
        }

        // ── Public ──────────────────────────────────────────────────────

        public void Initialize()
        {
            ResetRoundState();

            NetworkClient.RegisterHandler<TimeStateMessage>(OnClientTimeStateChanged);
            NetworkClient.RegisterHandler<WinnerDecidedMessage>(OnClientWinnerDecided);
            NetworkClient.RegisterHandler<RoomOptionSyncMessage>(OnClientRoomOptionChanged);

            WorkshopMapChecker.RegisterHandlers();
            NetworkClient.RegisterHandler<WorkshopMapCheckResult>(WorkshopMapChecker.HandleCheckResult);

            if (NetworkServer.active)
                WorkshopMapChecker.RegisterServerHandlers();
        }

        public void Cleanup()
        {
            CancelTimer();
            ResetRoundState();

            NetworkClient.UnregisterHandler<TimeStateMessage>();
            NetworkClient.UnregisterHandler<WinnerDecidedMessage>();
            NetworkClient.UnregisterHandler<RoomOptionSyncMessage>();

            WorkshopMapChecker.UnregisterHandlers();
            NetworkClient.UnregisterHandler<WorkshopMapCheckResult>();

            if (NetworkServer.active)
                WorkshopMapChecker.UnregisterServerHandlers();
        }

        [Server]
        public void StartGameWithOption(RoomOption option)
        {
            if (CurrentTimeState == TimeState.Countdown) return;

            var resolved = option?.Clone() ?? new RoomOption();
            ResolveRandomMap(resolved);

            CurrentRoomOption = resolved;
            CurrentWinner = PlayerRole.None;

            NetworkServer.SendToAll(new RoomOptionSyncMessage { option = resolved.ToData() });

            SetTimeState(TimeState.Countdown, CountdownDuration);
        }

        [Server]
        private void ResolveRandomMap(RoomOption option)
        {
            var registry = LobbyManager.Instance?.MapRegistry;
            var selected = registry?.FindById(option.mapName);
            if (selected == null || selected.mapType != MapType.Random) return;

            var picked = registry.GetRandomConcrete();
            if (picked != null)
                option.mapName = picked.name;
            else
                Debug.LogWarning("[GameRound] 랜덤 추첨 실패: 유효한 Normal 맵이 없습니다.");
        }

        [Server]
        public void BeginRound()
        {
            SetTimeState(TimeState.HideTime, OptionHideTime);

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.ResetAll();

                // Runner만 점수 초기화
                foreach (var conn in NetworkServer.connections.Values)
                {
                    if (_assignedRoles.TryGetValue(conn.connectionId, out var role)
                        && role == PlayerRole.Runner)
                    {
                        ScoreManager.Instance.SetScore(conn.connectionId, 0);
                    }
                }

                ScoreManager.Instance.StartBroadcast();
                ScoreManager.Instance.StartVisionScoring();
            }

            Debug.Log($"[GameRound] 인게임 씬 진입 확인. 타이머 가동 시작: {OptionHideTime}초");
        }

        [Server]
        public void NotifyRunnerDead()
        {
            if (CurrentTimeState != TimeState.SeekTime) return;
            CheckRunnerWipeout();
        }

        public bool TryGetRole(int connectionId, out PlayerRole role)
        {
            return _assignedRoles.TryGetValue(connectionId, out role);
        }

        public void RemovePlayer(int connectionId)
        {
            _assignedRoles.Remove(connectionId);
        }

        // ── Private ─────────────────────────────────────────────────────

        [Server]
        private void SetTimeState(TimeState newState, float time)
        {
            CurrentTimeState = newState;
            CurrentTimer = Mathf.CeilToInt(time);

            NetworkServer.SendToAll(new TimeStateMessage { state = newState, timer = time });
            Debug.Log($"[GameRound] 상태 전환: {newState}, 시간: {time}초, 승자: {CurrentWinner}");

            CancelTimer();
            if (CurrentTimer > 0)
                RunTimerAsync().Forget();
        }

        [Server]
        private void SetWinner(PlayerRole winner)
        {
            CurrentWinner = winner;
            NetworkServer.SendToAll(new WinnerDecidedMessage { winner = winner });
        }

        [Server]
        private void CheckRunnerWipeout()
        {
            if (InGameManager.Instance == null) return;
            if (CurrentWinner != PlayerRole.None) return;
            int aliveRunners = InGameManager.Instance.GetAliveRunnerCount();
            if (aliveRunners == 0)
            {
                SetWinner(PlayerRole.Seeker);
                GrantSurvivalBonus();
                SetTimeState(TimeState.CheckTime, OptionCheckTime);
            }
        }
        [Server]
        private void GrantSurvivalBonus()
        {
            if (ScoreManager.Instance == null || InGameManager.Instance == null) return;

            foreach (var runner in InGameManager.Instance.Runners)
            {
                if (runner == null || runner.IsDead) continue;
                if (runner.connectionToClient == null) continue;

                int connId = runner.connectionToClient.connectionId;
                int currentScore = ScoreManager.Instance.GetScore(connId);
                int bonus = Mathf.RoundToInt(currentScore * ScoreManager.Instance.SurvivalBonusPercent);
                ScoreManager.Instance.AddScore(connId, bonus);
            }
        }
        [Server]
        private void HandleTimerExpired()
        {
            switch (CurrentTimeState)
            {
                case TimeState.Countdown:
                    OnCountdownExpired();
                    break;
                case TimeState.HideTime:
                    OnHideTimeExpired();
                    break;
                case TimeState.SeekTime:
                    OnSeekTimeExpired();
                    break;
                case TimeState.CheckTime:
                    OnCheckTimeExpired();
                    break;
            }
        }

        [Server]
        private void OnCountdownExpired()
        {
            AssignRoles();

            var networkManager = (ChromaNetworkManager)NetworkManager.singleton;
            networkManager.ServerChangeScene(networkManager.inGameScene);
        }

        [Server]
        private void OnHideTimeExpired()
        {
            SetTimeState(TimeState.SeekTime, OptionSeekTime);

            if (InGameManager.Instance != null)
            {
                Vector3 seekerMainSpawn = InGameManager.Instance.GetSpawnPosition();
                foreach (var seeker in InGameManager.Instance.Seekers)
                    seeker.RpcTeleport(seekerMainSpawn);
            }
        }

        [Server]
        private void OnSeekTimeExpired()
        {
            SetWinner(PlayerRole.Runner);
            GrantSurvivalBonus();
            SetTimeState(TimeState.CheckTime, OptionCheckTime);
        }

        [Server]
        private void OnCheckTimeExpired()
        {
            SetTimeState(TimeState.GameEnd, 0f);
            StartReturnToLobbyTimer(LobbyReturnDelayOnCheckEnd).Forget();
        }

        private async UniTaskVoid RunTimerAsync()
        {
            _timerCts = new CancellationTokenSource();
            var token = _timerCts.Token;

            try
            {
                OnTimerTick?.Invoke(CurrentTimer);

                while (CurrentTimer > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
                    CurrentTimer--;
                    SoundManager.Instance?.PlaySfx(SfxType.Clock);
                    OnTimerTick?.Invoke(CurrentTimer);

                    if (NetworkServer.active)
                        CheckForcedExpose();
                }

                if (NetworkServer.active)
                    HandleTimerExpired();
            }
            catch (OperationCanceledException) { }
        }

        private void CancelTimer()
        {
            _timerCts?.Cancel();
            _timerCts?.Dispose();
            _timerCts = null;
        }

        private async UniTaskVoid StartReturnToLobbyTimer(float delaySeconds)
        {
            if (_isLobbyReturningStarted) return;
            _isLobbyReturningStarted = true;

            await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds));

            Debug.Log("[GameRound] 인게임 공식 종료, 로비 대기방 씬으로 강제 전환합니다.");
            _isLobbyReturningStarted = false;

            var netManager = NetworkManager.singleton as ChromaNetworkManager;
            if (netManager != null)
            {
                netManager.ServerChangeScene(netManager.onlineScene);
            }
        }

        private void ResetRoundState()
        {
            CurrentTimeState = TimeState.Waiting;
            CurrentWinner = PlayerRole.None;
            CurrentTimer = 0;
        }


        //--------------------------------역할정하기!-----------------------------------
        [Server]
        private void AssignRoles()
        {
            _assignedRoles.Clear();

            var conns = NetworkServer.connections.Values.ToList();
            if (conns.Count == 0) return;

            var seekerConnIds = DetermineSeekerConnectionIds(conns, OptionSeekerCount);

            foreach (var conn in conns)
            {
                _assignedRoles[conn.connectionId] = seekerConnIds.Contains(conn.connectionId)
                    ? PlayerRole.Seeker
                    : PlayerRole.Runner;
            }
        }

        // 술래 지원자를 우선 배정하고, 부족하거나 넘치는 인원은 규칙에 따라 랜덤 처리합니다.
        [Server]
        private HashSet<int> DetermineSeekerConnectionIds(List<NetworkConnectionToClient> conns, int seekerCount)
        {
            var connIdSet = new HashSet<int>(conns.Select(c => c.connectionId));

            var volunteerZone = FindAnyObjectByType<SeekerVolunteerZone>();
            var volunteers = volunteerZone != null
                ? volunteerZone.VolunteerConnectionIds.Where(connIdSet.Contains).ToList()
                : new List<int>();

            var shuffledVolunteers = volunteers.OrderBy(_ => UnityEngine.Random.value).ToList();

            if (shuffledVolunteers.Count >= seekerCount)
            {
                return new HashSet<int>(shuffledVolunteers.Take(seekerCount));
            }

            var result = new HashSet<int>(shuffledVolunteers);
            int remaining = seekerCount - result.Count;

            var nonVolunteers = connIdSet.Except(result).OrderBy(_ => UnityEngine.Random.value).ToList();
            foreach (var connId in nonVolunteers.Take(remaining))
                result.Add(connId);

            return result;
        }


        // ── 이벤트 핸들러 ───────────────────────────────────────────────

        private void OnClientTimeStateChanged(TimeStateMessage msg)
        {
            CurrentTimeState = msg.state;
            CurrentTimer = Mathf.CeilToInt(msg.timer);

            CancelTimer();
            if (CurrentTimer > 0)
                RunTimerAsync().Forget();

            OnTimeStateChanged?.Invoke(msg.state, CurrentTimer);
        }

        private void OnClientWinnerDecided(WinnerDecidedMessage msg)
        {
            CurrentWinner = msg.winner;
            OnWinnerDecided?.Invoke(msg.winner);
        }

        [Server]
        private void CheckForcedExpose()
        {
            if (!OptionForcedExposeEnabled) return;
            if (CurrentTimeState != TimeState.SeekTime) return;
            if (OptionForcedExposeInterval <= 0f) return;

            int elapsed = Mathf.CeilToInt(OptionSeekTime) - CurrentTimer;
            int intervalInt = Mathf.Max(1, Mathf.RoundToInt(OptionForcedExposeInterval));

            if (elapsed > 0 && elapsed % intervalInt == 0)
                TriggerForcedExposeForAllRunners();
        }

        [Server]
        private void TriggerForcedExposeForAllRunners()
        {
            if (InGameManager.Instance == null) return;

            foreach (var runner in InGameManager.Instance.Runners)
            {
                if (runner != null && !runner.IsDead)
                    runner.ServerForceExpose(forcedExposeDuration);
            }

            Debug.Log($"[GameRound] 강제 도발 발동! 생존 러너 전원 위치 노출 (지속시간: {forcedExposeDuration}초)");
        }

        private void OnClientRoomOptionChanged(RoomOptionSyncMessage msg)
        {
            CurrentRoomOption = RoomOption.FromData(msg.option);
        }
    }
}
