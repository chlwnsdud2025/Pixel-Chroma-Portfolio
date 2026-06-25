using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class NetworkManager : FusionCallbackAdapter
{
    private NetworkRunner _runner;

    [Header("Prefabs")]
    [SerializeField] private NetworkObject _gamePlayerPrefab; // 실제 게임에서 쓸 캐릭터

    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private static NetworkManager _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    async void StartGame(GameMode mode)
    {
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (scene.IsValid) sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "TestRoom",
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        // 방 접속 성공 시 1번 씬(Lobby)으로 이동
        if (_runner.IsServer)
        {
            _runner.LoadScene(SceneRef.FromIndex(1));
        }
    }

    private void OnGUI()
    {
        // 🌟 수정됨: _runner가 null일 때(즉, 아직 아무것도 누르지 않았을 때)만 버튼을 표시합니다.
        // 버튼을 누르는 순간 _runner가 생성되므로 버튼이 즉시 사라져 연타 버그를 막습니다.
        if (_runner == null)
        {
            if (GUI.Button(new Rect(0, 0, 200, 40), "Host")) StartGame(GameMode.Host);
            if (GUI.Button(new Rect(0, 40, 200, 40), "Join")) StartGame(GameMode.Client);
        }
    }

    // 🌟 씬 로드가 완료되었을 때 (게임 씬에서만 캐릭터 스폰)
    public override void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!runner.IsServer) return;

        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

        // 게임 씬(2번)에 도착했을 때만 진짜 캐릭터들을 소환합니다.
        if (currentSceneIndex == 2)
        {
            _spawnedCharacters.Clear();

            foreach (var player in runner.ActivePlayers)
            {
                SpawnPlayerCharacter(runner, player);
            }
        }
    }

    // 이미 게임 씬인데 누군가 늦게 들어왔을 때
    public override void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        if (SceneManager.GetActiveScene().buildIndex == 2)
        {
            SpawnPlayerCharacter(runner, player);
        }
    }

    private void SpawnPlayerCharacter(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.ContainsKey(player)) return;

        Vector2 spawnPosition = new Vector2((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1);
        NetworkObject spawnedObj = runner.Spawn(_gamePlayerPrefab, spawnPosition, Quaternion.identity, player);

        if (spawnedObj != null)
        {
            _spawnedCharacters.Add(player, spawnedObj);
        }
    }

    public override void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            if (networkObject != null) runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }
}