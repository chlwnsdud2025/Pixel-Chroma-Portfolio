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

        [Header("Physics")]
        [Tooltip("러너 캐릭터가 속한 물리 레이어. Infection 모드에서 서로 몸이 안 보일 때 이 레이어끼리 충돌을 꺼줍니다.")]
        [SerializeField] private LayerMask runnerPhysicsLayer;

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

        public int SecondsUntilNextForcedExpose
        {
            get
            {
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
            NetworkClient.RegisterHandler<TimeStateMessage>(OnClientTimeStateChanged);
            NetworkClient.RegisterHandler<WinnerDecidedMessage>(OnClientWinnerDecided);
            NetworkClient.RegisterHandler<RoomOptionSyncMessage>(OnClientRoomOptionChanged);
        }

        public void Cleanup()
        {
            CancelTimer();
            NetworkClient.UnregisterHandler<TimeStateMessage>();
            NetworkClient.UnregisterHandler<WinnerDecidedMessage>();
            NetworkClient.UnregisterHandler<RoomOptionSyncMessage>();
        }

        [Server]
        public void StartGameWithOption(RoomOption option)
        {
            if (CurrentTimeState == TimeState.Countdown) return;

            CurrentRoomOption = option;
            CurrentWinner = PlayerRole.None;

            NetworkServer.SendToAll(new RoomOptionSyncMessage { option = option.ToData() });

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
            if (aliveRunners == 0)
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

            UpdateRunnerCollision();

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

        //-----------------------InfectionMode에서 HideTime에 Runner끼리 충돌 방지----------------
        private void UpdateRunnerCollision()
        {
            int layer = LayerMaskToLayer(runnerPhysicsLayer);
            if (layer < 0) return;

            bool canCollide = CurrentRule == null || CurrentRule.CanRunnersCollide(CurrentTimeState);
            Physics2D.IgnoreLayerCollision(layer, layer, !canCollide);
        }

        // LayerMask(비트마스크)를 Physics2D.IgnoreLayerCollision이 요구하는 단일 레이어 번호로 변환
        private static int LayerMaskToLayer(LayerMask mask)
        {
            int value = mask.value;
            return value == 0 ? -1 : (int)Mathf.Log(value, 2);
        }
    }
}
