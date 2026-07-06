using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using Mirror.FizzySteam;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

namespace PixelChroma
{
    public enum TimeState { Waiting, HideTime, SeekTime, CheckTime, GameEnd }

    public enum WinFaction { None, Seeker, Runner }
    public struct TimeStateMessage : NetworkMessage
    {
        public TimeState state;
        public float timer;
        public WinFaction winner;
    }

    public class ChromaNetworkManager : NetworkManager
    {
        [Header("Chroma Scene Settings")]
        [Scene] public string inGameScene;

        [Header("Chroma Prefabs")]
        public GameObject lobbyPlayerPrefab;
        public GameObject runnerPrefab;
        public GameObject seekerPrefab;

        [Header("Countdown")]
        public float countdownDuration = 5f;

        public static event Action<int> OnCountdownTick;
        public static event Action OnCountdownEnd;


        // 이벤트 이름도 OnTimeStateChanged 로 변경
        public static event Action<TimeState, float, WinFaction> OnTimeStateChanged;

        private readonly Dictionary<int, PlayerRole> _assignedRoles = new();
        private readonly Dictionary<int, string> _playerNicknames = new(); // 닉네임 명부 추가

        private bool _isCountingDown;
        private CancellationTokenSource _countdownCts;
        private bool _isLobbyReturningStarted = false;// 중복 로비 이동 방지

        // 방장이 설정한 룰과 현재 진행 상태 관리
        private MapRegistrySO MapRegistry => LobbyManager.Instance != null ? LobbyManager.Instance.MapRegistry : null;

        public RoomOption CurrentRoomOption { get; private set; }
        public TimeState CurrentTimeState { get; private set; } = TimeState.Waiting;
        public float CurrentTimer { get; private set; } = 0f;
        public WinFaction CurrentWinner { get; private set; } = WinFaction.None;



        public override void Awake()
        {
            if (singleton != null && singleton != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            base.Awake();
#if UNITY_EDITOR
            var fizzy = GetComponent<FizzySteamworks>();
            var kcp = GetComponent<kcp2k.KcpTransport>();
            if (fizzy != null) fizzy.enabled = false;
            if (kcp != null) { kcp.enabled = true; Transport.active = kcp; }
#endif
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();

            if (LobbyManager.Instance != null && !NetworkServer.active)
                LobbyManager.Instance.NotifyClientConnected();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<CountdownTickMessage>(OnClientCountdownTick);
            NetworkClient.RegisterHandler<CountdownEndMessage>(OnClientCountdownEnd);

            // 클라이언트가 게임 상태 변경 메시지를 받을 수 있도록 등록
            NetworkClient.RegisterHandler<TimeStateMessage>(OnClientTimeStateChanged);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            NetworkClient.UnregisterHandler<CountdownTickMessage>();
            NetworkClient.UnregisterHandler<CountdownEndMessage>();
            NetworkClient.UnregisterHandler<TimeStateMessage>();
        }

        public void RegisterNickname(int connId, string nickname)
        {
            _playerNicknames[connId] = nickname;
            Debug.Log($"[NetworkManager] 명부에 닉네임 등록 완료 - ConnID: {connId}, Name: {nickname}");
        }

        // ── 중앙 통제 게임 루프 및 타이머 ────────────────────────────────

        private void Update()
        {
            if (CurrentTimeState == TimeState.HideTime ||
                CurrentTimeState == TimeState.SeekTime ||
                CurrentTimeState == TimeState.CheckTime)
            {
                if (CurrentTimer > 0)
                    CurrentTimer -= Time.deltaTime;

                // ── 오직 서버(방장)만 승리 판정 및 페이즈 전환을 감시합니다 ──
                if (NetworkServer.active)
                {
                    // 술래 시간(SeekTime) 도중 실시간 생존 인원 검사
                    if (CurrentTimeState == TimeState.SeekTime)
                    {
                        // 관전 중이 아니고(alive), 아직 살아있는 도망자 컴포넌트 추출
                        int aliveRunners = RunnerPlayer.AllRunners.Count(r => r != null && !r.isDead && !r.IsSpectating);

                        // 🚨 도망자가 전멸했다면? 즉시 술래(Seeker) 조기 승리 확정!
                        if (aliveRunners == 0 && RunnerPlayer.AllRunners.Count > 0)
                        {
                            SetTimeState(TimeState.GameEnd, 0f, WinFaction.Seeker);
                            StartReturnToLobbyTimer(5f).Forget(); // 5초 뒤 로비 복귀 공정 가동
                            return;
                        }
                    }

                    // 시간 만료에 따른 정규 타임라인 통제
                    if (CurrentTimer <= 0)
                    {
                        if (CurrentTimeState == TimeState.HideTime)
                        {
                            float seekTime = CurrentRoomOption != null ? CurrentRoomOption.seekTime : 180f;
                            SetTimeState(TimeState.SeekTime, seekTime);

                            if (InGameManager.Instance != null)
                            {
                                Vector3 seekerMainSpawn = InGameManager.Instance.GetSpawnPosition(PlayerRole.Seeker, false);
                                foreach (var seeker in SeekerPlayer.AllSeekers)
                                {
                                    seeker.RpcTeleport(seekerMainSpawn);
                                }
                            }
                        }
                        else if (CurrentTimeState == TimeState.SeekTime)
                        {
                            // 시간 끝날 때까지 도망자가 1명이라도 살아남았다면? 도망자(Runner) 승리!
                            float checkTime = CurrentRoomOption != null ? CurrentRoomOption.checkTime : 15f;
                            SetTimeState(TimeState.CheckTime, checkTime, WinFaction.Runner);
                        }
                        else if (CurrentTimeState == TimeState.CheckTime)
                        {
                            // 숨어있던 자리 확인 시간(CheckTime)마저 끝나면 로비로 안전 복귀 가동
                            SetTimeState(TimeState.GameEnd, 0f, WinFaction.Runner);
                            StartReturnToLobbyTimer(0.5f).Forget();
                        }
                    }
                }
            }
        }

        [Server]
        private void SetTimeState(TimeState newState, float time, WinFaction winner = WinFaction.None)
        {
            CurrentTimeState = newState;
            CurrentTimer = time;
            CurrentWinner = winner;

            NetworkServer.SendToAll(new TimeStateMessage { state = newState, timer = time, winner = winner });
            Debug.Log($"[Server 루프] 상태: {newState}, 남은시간: {time}초, 승리진영: {winner}");
        }

        private void OnClientTimeStateChanged(TimeStateMessage msg)
        {
            CurrentTimeState = msg.state;
            CurrentTimer = msg.timer;
            CurrentWinner = msg.winner;
            OnTimeStateChanged?.Invoke(msg.state, msg.timer, msg.winner);
        }

        private async UniTaskVoid StartReturnToLobbyTimer(float delaySeconds)
        {
            if (_isLobbyReturningStarted) return;
            _isLobbyReturningStarted = true;

            await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds));

            Debug.Log("[Server 루프] 인게임이 공식 종료되어 다시 로비 대기방 씬으로 전환합니다.");
            _isLobbyReturningStarted = false;

            // Mirror 엔진 명령어: 방에 묶인 모든 클라이언트를 데리고 로비(onlineScene)로 강제 이동합니다.
            ServerChangeScene(onlineScene);
        }

        // ────────────────────────────────────────────────────────────────

        public void StartGame()
        {
            StartGameWithOption(CurrentRoomOption ?? new RoomOption());
        }

        public void StartGameWithOption(RoomOption option)
        {
            if (!NetworkServer.active || _isCountingDown) return;

            CurrentRoomOption = option;
            AssignRoles();
            ApplyRolesToLobbyPlayers();
            StartCountdownAsync().Forget();
        }

        private async UniTaskVoid StartCountdownAsync()
        {
            _isCountingDown = true;
            _countdownCts = new CancellationTokenSource();

            int remaining = Mathf.CeilToInt(countdownDuration);

            while (remaining > 0)
            {
                NetworkServer.SendToAll(new CountdownTickMessage { remainingSeconds = remaining });
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: _countdownCts.Token);
                remaining--;
            }

            NetworkServer.SendToAll(new CountdownEndMessage());

            _isCountingDown = false;
            _countdownCts = null;

            string targetScene = ResolveMapScene();
            ServerChangeScene(targetScene);
        }

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

        private static void OnClientCountdownTick(CountdownTickMessage msg)
        {
            OnCountdownTick?.Invoke(msg.remainingSeconds);
        }

        private static void OnClientCountdownEnd(CountdownEndMessage msg)
        {
            OnCountdownEnd?.Invoke();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            GameObject playerInstance = null;

            if (currentScene == GetSceneName(onlineScene))
            {
                // 1. 로비 씬 스폰
                playerInstance = Instantiate(lobbyPlayerPrefab);
            }
            else if (IsInGameScene(currentScene))
            {
                // 2. 인게임 씬 스폰

                // 💡 변수명이 겹치지 않게 'assignedRole'로 명확하게 변경했습니다.
                PlayerRole assignedRole = _assignedRoles.TryGetValue(conn.connectionId, out var r)
                    ? r
                    : PlayerRole.Runner;

                GameObject prefab = assignedRole == PlayerRole.Seeker ? seekerPrefab : runnerPrefab;

                Vector3 spawnPos = Vector3.zero;
                Quaternion spawnRot = Quaternion.identity;

                if (InGameManager.Instance != null)
                {
                    bool isWaiting = (assignedRole == PlayerRole.Seeker);
                    spawnPos = InGameManager.Instance.GetSpawnPosition(assignedRole, isWaiting);
                }
                else
                {
                    Transform startPos = GetStartPosition();
                    if (startPos != null)
                    {
                        spawnPos = startPos.position;
                        spawnRot = startPos.rotation;
                    }
                }

                playerInstance = Instantiate(prefab, spawnPos, spawnRot);

                // 인게임 캐릭터에 역할 주입
                var inGamePlayerBase = playerInstance.GetComponent<PlayerBase>();
                if (inGamePlayerBase != null)
                {
                    inGamePlayerBase.role = assignedRole;
                }

                _assignedRoles.Remove(conn.connectionId);
            }

            // 3. 공통 처리: 스폰된 아바타(로비든 인게임이든)에 명부 닉네임 주입 후 네트워크 연결
            if (playerInstance != null)
            {
                var playerBase = playerInstance.GetComponent<PlayerBase>();
                if (playerBase != null)
                {
                    if (_playerNicknames.TryGetValue(conn.connectionId, out var savedName))
                    {
                        playerBase.playerName = savedName;
                        // 서버에서 여기서 playerName을 미리 세팅해주면, 
                        // AddPlayerForConnection 호출 시 모든 유저에게 이 닉네임이 SyncVar로 완벽하게 뿌려집니다!
                    }
                }

                NetworkServer.AddPlayerForConnection(conn, playerInstance);
            }
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();

            string currentScene = SceneManager.GetActiveScene().name;
            if (IsInGameScene(currentScene) && GameManager.Instance != null)
            {
                GameManager.Instance.EnterInGame().Forget();
            }
            else if (currentScene == GetSceneName(onlineScene) && GameManager.Instance != null)
            {
                GameManager.Instance.EnterLobby().Forget();
            }
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);

            if (IsInGameScene(GetSceneName(sceneName)))
            {
                // 방장이 설정한 옵션이 있다면 그 시간을, 없다면 기본값(45초)을 적용
                float hideTime = CurrentRoomOption != null ? CurrentRoomOption.hideTime : 45f;

                // 타이머 및 상태 가동!
                SetTimeState(TimeState.HideTime, hideTime);

                Debug.Log($"[NetworkManager] 인게임 씬 진입 확인. 타이머 가동 시작: {hideTime}초");
            }
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.NotifyClientDisconnected();

            if (InGameManager.Instance != null)
                InGameManager.Instance.ClearSpawnPoints();

            if (GameManager.Instance == null) return;

            Debug.LogWarning("[Network] 서버와의 연결이 끊어졌습니다. 메인 메뉴로 복귀합니다.");

            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyId.IsValid())
                LobbyManager.Instance.LeaveLobby();

            GameManager.Instance.EnterMainMenu().Forget();
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            _assignedRoles.Remove(conn.connectionId);
            _playerNicknames.Remove(conn.connectionId); //명부에서 삭제
        }

        private void AssignRoles()
        {
            _assignedRoles.Clear();

            var conns = NetworkServer.connections.Values.ToList();
            if (conns.Count == 0) return;

            int seekerCount = CurrentRoomOption != null ? CurrentRoomOption.seekerCount : 1;
            var shuffled = conns.OrderBy(_ => UnityEngine.Random.value).ToList();

            for (int i = 0; i < shuffled.Count; i++)
            {
                PlayerRole role = (i < seekerCount) ? PlayerRole.Seeker : PlayerRole.Runner;
                _assignedRoles[shuffled[i].connectionId] = role;
            }
        }

        private bool IsInGameScene(string sceneName)
        {
            if (sceneName == GetSceneName(inGameScene))
                return true;

            return MapRegistry != null && MapRegistry.ContainsScene(sceneName);
        }

        private string ResolveMapScene()
        {
            if (MapRegistry != null && CurrentRoomOption != null)
            {
                var mapData = MapRegistry.FindById(CurrentRoomOption.mapName);
                if (mapData != null && !string.IsNullOrEmpty(mapData.sceneName))
                    return mapData.sceneName;
            }

            return inGameScene;
        }

        private string GetSceneName(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return string.Empty;
            return System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }
    }
}