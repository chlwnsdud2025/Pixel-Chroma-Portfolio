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
        [Header("Countdown")]
        public float countdownDuration = 5f;

        private const float LobbyReturnDelayOnWipe = 5f;
        private const float LobbyReturnDelayOnCheckEnd = 0.5f;

        public static event Action<TimeState, int> OnTimeStateChanged;
        public static event Action<PlayerRole> OnWinnerDecided;
        public static event Action<int> OnTimerTick;

        private readonly Dictionary<int, PlayerRole> _assignedRoles = new();
        private bool _isLobbyReturningStarted = false;
        private CancellationTokenSource _timerCts;

        public RoomOption CurrentRoomOption { get; private set; }
        public TimeState CurrentTimeState { get; private set; } = TimeState.Waiting;
        public PlayerRole CurrentWinner { get; private set; } = PlayerRole.None;
        public int CurrentTimer { get; private set; }

        private float OptionHideTime => CurrentRoomOption?.hideTime ?? RoomOption.DefaultHideTime;
        private float OptionSeekTime => CurrentRoomOption?.seekTime ?? RoomOption.DefaultSeekTime;
        private float OptionCheckTime => CurrentRoomOption?.checkTime ?? RoomOption.DefaultCheckTime;
        private int OptionSeekerCount => CurrentRoomOption?.seekerCount ?? RoomOption.DefaultSeekerCount;

        // ── Unity 생명주기 ──────────────────────────────────────────────

        private void OnDestroy()
        {
            CancelTimer();
        }

        // ── Public ──────────────────────────────────────────────────────

        public void Initialize()
        {
            NetworkClient.RegisterHandler<TimeStateMessage>(OnClientTimeStateChanged);
            NetworkClient.RegisterHandler<WinnerDecidedMessage>(OnClientWinnerDecided);
        }

        public void Cleanup()
        {
            CancelTimer();
            NetworkClient.UnregisterHandler<TimeStateMessage>();
            NetworkClient.UnregisterHandler<WinnerDecidedMessage>();
        }

        [Server]
        public void StartGameWithOption(RoomOption option)
        {
            if (CurrentTimeState == TimeState.Countdown) return;

            CurrentRoomOption = option;
            CurrentWinner = PlayerRole.None;

            SetTimeState(TimeState.Countdown, countdownDuration);
        }

        [Server]
        public void BeginRound()
        {
            SetTimeState(TimeState.HideTime, OptionHideTime);
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
            int aliveRunners = InGameManager.Instance.GetAliveRunnerCount();
            if (aliveRunners == 0 && InGameManager.Instance.HasAnyRunner())
            {
                SetWinner(PlayerRole.Seeker);
                SetTimeState(TimeState.GameEnd, 0f);
                StartReturnToLobbyTimer(LobbyReturnDelayOnWipe).Forget();
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
            string targetScene = networkManager.ResolveMapScene();
            networkManager.ServerChangeScene(targetScene);
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
                    OnTimerTick?.Invoke(CurrentTimer);
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

        [Server]
        private void AssignRoles()
        {
            _assignedRoles.Clear();

            var conns = NetworkServer.connections.Values.ToList();
            if (conns.Count == 0) return;

            int seekerCount = OptionSeekerCount;
            var shuffled = conns.OrderBy(_ => UnityEngine.Random.value).ToList();

            for (int i = 0; i < shuffled.Count; i++)
            {
                PlayerRole role = (i < seekerCount) ? PlayerRole.Seeker : PlayerRole.Runner;
                _assignedRoles[shuffled[i].connectionId] = role;
            }
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
    }
}
