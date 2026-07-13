using Cysharp.Threading.Tasks;
using PixelChroma.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Settings;

namespace PixelChroma
{
    public class GameManager : MonoSingleton<GameManager>
    {
        public GameState CurrentState { get; private set; } = GameState.Boot;

        [Header("Scene Essentials")]
        [SerializeField] private GameObject mainCameraPrefab;
        [SerializeField] private GameObject cinemachineCameraPrefab;

        private async void Start()
        {
            await Boot();
        }

        private async UniTask Boot()
        {
            // [변경] UIManager를 통해 초기 로딩 화면을 띄웁니다.
            if (UIManager.Instance != null) 
                UIManager.Instance.ShowInitialLoading().Forget();

            if (!InitializeSteam())
            {
                Debug.LogError("[GameManager] Steam 초기화 실패");
                Application.Quit();
                return;
            }
            // Localization대기 및 설정 복원
            var initOp = LocalizationSettings.InitializationOperation;
            await UniTask.WaitUntil(() => initOp.IsDone); LocalizationManager.ApplySavedLocale();
            LocalizationManager.ApplySavedLocale();
            await UIManager.Instance.InitializeAsync();

            // [변경] UIManager를 통해 초기 로딩 화면을 숨깁니다.
            if (UIManager.Instance != null) 
                UIManager.Instance.HideInitialLoading().Forget();

            await EnterMainMenu();
        }

        // 스팀을 사용하는(초기화된) 빌드인지 여부. 에디터는 localhost로 테스트하므로 스팀을 쓰지 않습니다.
        // 스팀 콜백 등록·로비 API 등 스팀 의존 로직을 한 곳에서 켜고 끄기 위한 단일 게이트입니다.
        public static bool SteamActive =>
#if UNITY_EDITOR
            false;
#else
            true;
#endif

        private bool InitializeSteam()
        {
            // 스팀 미사용(에디터/로컬)이면 초기화를 건너뛰고 성공으로 처리합니다.
            if (!SteamActive)
                return true;

            return Steamworks.SteamAPI.Init();
        }

        public void HandleDisconnect()
        {
            Debug.LogWarning("[GameManager] 서버와의 연결이 끊어졌습니다. 메인 메뉴로 복귀합니다.");

            if (InGameManager.Instance != null)
                InGameManager.Instance.ResetState();

            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobbyId.IsValid())
                LobbyManager.Instance.LeaveLobby();

            EnterMainMenu().Forget();
        }

        public async UniTask EnterMainMenu()
        {
            CurrentState = GameState.MainMenu;
            EnsureSceneEssentials();

            if (CursorManager.Instance != null)
                CursorManager.Instance.SetCursorType(CursorType.Default);

            await UIManager.Instance.ShowScene<UITitle>();
        }

        public async UniTask EnterLobby()
        {
            CurrentState = GameState.Lobby;
            EnsureSceneEssentials();

            if (CursorManager.Instance != null)
                CursorManager.Instance.SetCursorType(CursorType.Default);

            await UIManager.Instance.HideAllPopups();
            await UIManager.Instance.ShowScene<UILobby>();
        }

        public async UniTask EnterInGame()
        {
            CurrentState = GameState.InGame;
            EnsureSceneEssentials();
            await UIManager.Instance.HideAllPopups();
            //await UIManager.Instance.ShowScene<UIInGameHUD>();
        }

        // 씬 전환 시 필수 오브젝트를 보장한다. 없으면 프리팹으로 생성한다.
        private void EnsureSceneEssentials()
        {
            EnsureEventSystem();
            EnsureMainCamera();

            // vcam은 Title(메인 메뉴)을 제외한 모든 씬(로비·인게임)에서 필요하다.
            // 스폰된 로컬 플레이어가 PlayerBase.SetupCinemachineFollow에서 이 vcam을 찾아 Follow를 건다.
            if (CurrentState != GameState.MainMenu)
                EnsureCinemachineCamera();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) 
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // Main Camera(Brain·Camera·AudioListener)는 맵마다 동일한 제네릭 오브젝트이므로
        // 씬에 없으면 프리팹으로 생성한다. Camera.main 가드로 중복 AudioListener를 막는다.
        private void EnsureMainCamera()
        {
            if (Camera.main != null) return;

            if (mainCameraPrefab == null)
            {
                Debug.LogError("[GameManager] mainCameraPrefab이 할당되지 않았습니다!");
                return;
            }

            Instantiate(mainCameraPrefab);
        }

        // vcam은 Confiner 같은 맵 고유 씬 참조가 없어 제네릭하게 생성할 수 있다.
        // Follow 타겟은 프리팹에서 비어 있고, 로컬 플레이어 스폰 시 런타임에 바인딩된다.
        private void EnsureCinemachineCamera()
        {
            if (FindAnyObjectByType<CinemachineCamera>() != null) return;

            if (cinemachineCameraPrefab == null)
            {
                Debug.LogError("[GameManager] cinemachineCameraPrefab이 할당되지 않았습니다!");
                return;
            }

            Instantiate(cinemachineCameraPrefab);
        }
    }
}