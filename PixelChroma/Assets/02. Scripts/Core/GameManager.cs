using Cysharp.Threading.Tasks;
using PixelChroma.UI;
using UnityEngine;

namespace PixelChroma
{
    public class GameManager : MonoSingleton<GameManager>
    {
        // ── [수정] UIInitialLoading 직속 변수 제거 완료 ──

        public GameState CurrentState { get; private set; } = GameState.Boot;

        private async void Start()
        {
            await Boot();
        }

        private async UniTask Boot()
        {
            // [변경] UIManager를 통해 초기 로딩 화면을 띄웁니다.
            if (UIManager.Instance != null) UIManager.Instance.ShowInitialLoading();

            if (!InitializeSteam())
            {
                Debug.LogError("[GameManager] Steam 초기화 실패");
                Application.Quit();
                return;
            }

            await UIManager.Instance.InitializeAsync();

            // [변경] UIManager를 통해 초기 로딩 화면을 숨깁니다.
            if (UIManager.Instance != null) UIManager.Instance.HideInitialLoading();

            await EnterMainMenu();
        }

        private bool InitializeSteam()
        {
#if UNITY_EDITOR
            return true;
#else
            return Steamworks.SteamAPI.Init();
#endif
        }

        public async UniTask EnterMainMenu()
        {
            CurrentState = GameState.MainMenu;
            await UIManager.Instance.ShowScene<UITitle>();
        }

        public async UniTask EnterLobby()
        {
            CurrentState = GameState.Lobby;
            await UIManager.Instance.ShowScene<UILobby>();
        }

        public async UniTask EnterInGame()
        {
            CurrentState = GameState.InGame;
            await UIManager.Instance.HideAllPopups();
            await UIManager.Instance.ShowScene<UIHUD>();
        }
    }
}