using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Mirror;
using Cysharp.Threading.Tasks;

namespace PixelChroma
{
    public class GameRoundManager : MonoSingleton<GameRoundManager>
    {
        [Header("Countdown")]
        public float countdownDuration = 5f;

        public static event Action<int> OnCountdownTick;
        public static event Action OnCountdownEnd;
        public static event Action<TimeState, float> OnTimeStateChanged;

        // 게임 시작 시점에 LobbyManager.CurrentRoomOption의 참조를 확정하여 이 라운드의 규칙으로 사용한다.
        // 확정 후에는 LobbyManager와 독립적으로 사용하며, 서버에서만 읽는다(클라이언트에서는 null).
        public RoomOption CurrentRoomOption { get; private set; }
        public TimeState CurrentTimeState { get; private set; } = TimeState.Waiting;
        public float CurrentTimer { get; private set; }

        private readonly Dictionary<int, PlayerRole> _assignedRoles = new();
        private bool _isCountingDown;
        private CancellationTokenSource _countdownCts;

        public void Initialize()
        {
            NetworkClient.RegisterHandler<CountdownTickMessage>(OnClientCountdownTick);
            NetworkClient.RegisterHandler<CountdownEndMessage>(OnClientCountdownEnd);
            NetworkClient.RegisterHandler<TimeStateMessage>(OnClientTimeStateChanged);
        }

        public void Cleanup()
        {
            CancelCountdown();
            NetworkClient.UnregisterHandler<CountdownTickMessage>();
            NetworkClient.UnregisterHandler<CountdownEndMessage>();
            NetworkClient.UnregisterHandler<TimeStateMessage>();
        }

        private void OnDestroy()
        {
            CancelCountdown();
        }

        // ── 타이머 ──────────────────────────────────────────────────────

        private void Update()
        {
            if (CurrentTimeState != TimeState.HideTime && CurrentTimeState != TimeState.SeekTime)
                return;

            if (CurrentTimer > 0)
                CurrentTimer = Mathf.Max(0f, CurrentTimer - Time.deltaTime);

            if (!NetworkServer.active || CurrentTimer > 0)
                return;

            if (CurrentTimeState == TimeState.HideTime)
            {
                float seekTime = CurrentRoomOption != null ? CurrentRoomOption.seekTime : RoomOption.DefaultSeekTime;
                SetTimeState(TimeState.SeekTime, seekTime);

                if (InGameManager.Instance != null)
                {
                    Vector3 seekerMainSpawn = InGameManager.Instance.GetSpawnPosition(PlayerRole.Seeker, false);
                    foreach (var seeker in SeekerPlayer.AllSeekers)
                        seeker.RpcTeleport(seekerMainSpawn);
                }
            }
            else if (CurrentTimeState == TimeState.SeekTime)
            {
                SetTimeState(TimeState.GameEnd, 0f);
            }
        }

        [Server]
        private void SetTimeState(TimeState newState, float time)
        {
            CurrentTimeState = newState;
            CurrentTimer = time;
            NetworkServer.SendToAll(new TimeStateMessage { state = newState, timer = time });
            Debug.Log($"[GameRound] 상태 전환: {newState}, 시간: {time}초");
        }

        private void OnClientTimeStateChanged(TimeStateMessage msg)
        {
            CurrentTimeState = msg.state;
            CurrentTimer = msg.timer;
            OnTimeStateChanged?.Invoke(msg.state, msg.timer);
        }

        // ── 게임 시작 / 카운트다운 ─────────────────────────────────────

        [Server]
        public void StartGameWithOption(RoomOption option)
        {
            if (!NetworkServer.active || _isCountingDown) return;

            CurrentRoomOption = option;
            StartCountdownAsync().Forget();
        }

        [Server]
        public void BeginRound()
        {
            float hideTime = CurrentRoomOption != null ? CurrentRoomOption.hideTime : RoomOption.DefaultHideTime;
            SetTimeState(TimeState.HideTime, hideTime);
            Debug.Log($"[GameRound] 인게임 씬 진입 확인. 타이머 가동 시작: {hideTime}초");
        }

        private async UniTaskVoid StartCountdownAsync()
        {
            _isCountingDown = true;
            _countdownCts?.Cancel();
            _countdownCts?.Dispose();
            _countdownCts = new CancellationTokenSource();

            try
            {
                int remaining = Mathf.CeilToInt(countdownDuration);

                while (remaining > 0)
                {
                    NetworkServer.SendToAll(new CountdownTickMessage { remainingSeconds = remaining });
                    await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: _countdownCts.Token);
                    remaining--;
                }

                NetworkServer.SendToAll(new CountdownEndMessage());
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[GameRound] 카운트다운이 취소되었습니다.");
                return;
            }
            finally
            {
                _isCountingDown = false;
                _countdownCts?.Dispose();
                _countdownCts = null;
            }

            AssignRoles();
            ApplyRolesToLobbyPlayers();

            var networkManager = (ChromaNetworkManager)NetworkManager.singleton;
            string targetScene = networkManager.ResolveMapScene();
            networkManager.ServerChangeScene(targetScene);
        }

        private void CancelCountdown()
        {
            _countdownCts?.Cancel();
            _countdownCts?.Dispose();
            _countdownCts = null;
            _isCountingDown = false;
        }

        // ── 역할 배정 ──────────────────────────────────────────────────

        public bool TryGetRole(int connectionId, out PlayerRole role)
        {
            return _assignedRoles.TryGetValue(connectionId, out role);
        }

        public void RemovePlayer(int connectionId)
        {
            _assignedRoles.Remove(connectionId);
        }

        [Server]
        private void AssignRoles()
        {
            _assignedRoles.Clear();

            var conns = NetworkServer.connections.Values.ToList();
            if (conns.Count == 0) return;

            int seekerCount = CurrentRoomOption != null ? CurrentRoomOption.seekerCount : RoomOption.DefaultSeekerCount;
            var shuffled = conns.OrderBy(_ => UnityEngine.Random.value).ToList();

            for (int i = 0; i < shuffled.Count; i++)
            {
                PlayerRole role = (i < seekerCount) ? PlayerRole.Seeker : PlayerRole.Runner;
                _assignedRoles[shuffled[i].connectionId] = role;
            }
        }

        [Server]
        private void ApplyRolesToLobbyPlayers()
        {
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn.identity == null) continue;

                var playerBase = conn.identity.GetComponent<PlayerBase>();
                if (playerBase != null && _assignedRoles.TryGetValue(conn.connectionId, out var role))
                    playerBase.role = role;
            }
        }

        // ── 카운트다운 클라이언트 핸들러 ────────────────────────────────

        private static void OnClientCountdownTick(CountdownTickMessage msg)
        {
            OnCountdownTick?.Invoke(msg.remainingSeconds);
        }

        private static void OnClientCountdownEnd(CountdownEndMessage msg)
        {
            OnCountdownEnd?.Invoke();
        }
    }
}
