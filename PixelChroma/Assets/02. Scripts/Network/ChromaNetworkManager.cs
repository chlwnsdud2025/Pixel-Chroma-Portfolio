using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using Mirror.FizzySteam;
using Cysharp.Threading.Tasks;

namespace PixelChroma
{
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

        private readonly Dictionary<int, PlayerRole> _assignedRoles = new();
        private bool _isCountingDown;
        private CancellationTokenSource _countdownCts;

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
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            NetworkClient.UnregisterHandler<CountdownTickMessage>();
            NetworkClient.UnregisterHandler<CountdownEndMessage>();
        }

        public void StartGame()
        {
            if (!NetworkServer.active || _isCountingDown) return;

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

            ServerChangeScene(inGameScene);
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
            else if (currentScene == GetSceneName(inGameScene))
            {
                PlayerRole role = _assignedRoles.TryGetValue(conn.connectionId, out var assigned)
                    ? assigned
                    : PlayerRole.Runner;

                GameObject prefab = role == PlayerRole.Seeker ? seekerPrefab : runnerPrefab;

                Vector3 spawnPos = Vector3.zero;
                Quaternion spawnRot = Quaternion.identity;

                if (InGameManager.Instance != null)
                {
                    // InGameManager가 살아있다면 맵에 등록된 스폰 포인트를 가져옵니다.
                    spawnPos = InGameManager.Instance.GetSpawnPosition(role);
                }
                else
                {
                    // 만약 매니저를 찾지 못하는 예외 상황이면 기존 Mirror 방식을 백업으로 씁니다.
                    Transform startPos = GetStartPosition();
                    if (startPos != null)
                    {
                        spawnPos = startPos.position;
                        spawnRot = startPos.rotation;
                    }
                }

                // 받아온 좌표를 기반으로 플레이어 생성
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

        public override void OnServerSceneChanged(string sceneName)
        {
            // 역할 배정은 StartGame() 카운트다운 시점에 이미 완료됨.
            // 씬별 추가 처리가 필요하면 여기에 분기 추가.
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();

            string currentScene = SceneManager.GetActiveScene().name;

            if (currentScene == GetSceneName(inGameScene) && GameManager.Instance != null)
            {
                GameManager.Instance.EnterInGame().Forget();
            }
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.NotifyClientDisconnected();

            if (InGameManager.Instance != null)
            {
                InGameManager.Instance.ClearSpawnPoints();
            }

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

            // TODO: 현재는 랜덤 1명을 술래로 지정하지만,
            //       추후 방 생성 시 seekerCount를 설정하거나 로비에서 변경할 수 있도록 확장 예정.
            //       - 방 생성 옵션(SyncVar 또는 Lobby 메타데이터)으로 seekerCount를 받아오기
            //       - 로비 UI에서 호스트가 seekerCount를 조절할 수 있는 기능 추가
            //       - seekerCount에 따라 아래 배정 로직을 N명 랜덤 선택으로 변경
            int seekerCount = 1;

            var shuffled = conns.OrderBy(_ => UnityEngine.Random.value).ToList();

            for (int i = 0; i < shuffled.Count; i++)
            {
                PlayerRole role = (i < seekerCount) ? PlayerRole.Seeker : PlayerRole.Runner;
                _assignedRoles[shuffled[i].connectionId] = role;
            }
        }

        private string GetSceneName(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return string.Empty;
            return System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }
    }
}
