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
    public enum TimeState { Waiting, HideTime, SeekTime, GameEnd }

    public struct TimeStateMessage : NetworkMessage
    {
        public TimeState state;
        public float timer;
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
        public static event Action<TimeState, float> OnTimeStateChanged;

        private readonly Dictionary<int, PlayerRole> _assignedRoles = new();
        private bool _isCountingDown;
        private CancellationTokenSource _countdownCts;

        // 방장이 설정한 룰과 현재 진행 상태 관리
        private MapRegistrySO MapRegistry => LobbyManager.Instance != null ? LobbyManager.Instance.MapRegistry : null;

        public RoomOption CurrentRoomOption { get; private set; }
        public TimeState CurrentTimeState { get; private set; } = TimeState.Waiting;
        public float CurrentTimer { get; private set; } = 0f;

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

        // ── 중앙 통제 게임 루프 및 타이머 ────────────────────────────────

        private void Update()
        {
            if (CurrentTimeState == TimeState.HideTime || CurrentTimeState == TimeState.SeekTime)
            {
                // 서버와 클라이언트 모두 각자 컴퓨터에서 타이머를 깎습니다
                if (CurrentTimer > 0)
                    CurrentTimer -= Time.deltaTime;

                // 타이머가 0이 되면 다음 상태로 넘어가는 통제는 "오직 서버"만 합니다.
                if (NetworkServer.active && CurrentTimer <= 0)
                {
                    if (CurrentTimeState == TimeState.HideTime)
                    {
                        float seekTime = CurrentRoomOption != null ? CurrentRoomOption.seekTime : 180f;
                        SetTimeState(TimeState.SeekTime, seekTime);

                        // 찾는 시간이 시작되었으므로 술래들을 대기실에서 본진으로 텔레포트!
                        if (InGameManager.Instance != null)
                        {
                            Vector3 seekerMainSpawn = InGameManager.Instance.GetSpawnPosition(PlayerRole.Seeker, false);

                            // SeekerPlayer에 만들어둔 전역 리스트(AllSeekers)를 활용해 모두 텔레포트
                            foreach (var seeker in SeekerPlayer.AllSeekers)
                            {
                                seeker.RpcTeleport(seekerMainSpawn);
                            }
                        }
                    }
                    else if (CurrentTimeState == TimeState.SeekTime)
                    {
                        SetTimeState(TimeState.GameEnd, 0f);
                    }
                }
            }
        }

        [Server]
        private void SetTimeState(TimeState newState, float time)
        {
            CurrentTimeState = newState;
            CurrentTimer = time;

            // 모든 클라이언트에게 바뀐 상태와 시간을 쏴줍니다.
            NetworkServer.SendToAll(new TimeStateMessage { state = newState, timer = time });
            Debug.Log($"[NetworkManager] 상태 전환: {newState}, 시간: {time}초");
        }

        private void OnClientTimeStateChanged(TimeStateMessage msg)
        {
            CurrentTimeState = msg.state;
            CurrentTimer = msg.timer;
            OnTimeStateChanged?.Invoke(msg.state, msg.timer);
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
                playerInstance = Instantiate(lobbyPlayerPrefab);
            }
            else if (IsInGameScene(currentScene))
            {
                PlayerRole role = _assignedRoles.TryGetValue(conn.connectionId, out var assigned)
                    ? assigned
                    : PlayerRole.Runner;

                GameObject prefab = role == PlayerRole.Seeker ? seekerPrefab : runnerPrefab;

                Vector3 spawnPos = Vector3.zero;
                Quaternion spawnRot = Quaternion.identity;

                if (InGameManager.Instance != null)
                {
                    bool isWaiting = (role == PlayerRole.Seeker);
                    spawnPos = InGameManager.Instance.GetSpawnPosition(role, isWaiting);
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

                var playerBase = playerInstance.GetComponent<PlayerBase>();
                if (playerBase != null)
                    playerBase.role = role;

                _assignedRoles.Remove(conn.connectionId);
            }

            if (playerInstance != null)
            {
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