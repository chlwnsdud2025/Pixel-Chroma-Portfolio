using Cysharp.Threading.Tasks;
using PixelChroma.UI;
using UnityEngine;

namespace PixelChroma
{
    public class GameManager : MonoSingleton<GameManager>
    {
        [SerializeField] private UIInitialLoading initialLoading;

        public GameState CurrentState { get; private set; } = GameState.Boot;

        private async void Start()
        {
            await Boot();
        }

        private async UniTask Boot()
        {
            initialLoading.Show();

            if (!InitializeSteam())
            {
                Debug.LogError("[GameManager] Steam 초기화 실패");
                Application.Quit();
                return;
            }

            await UIManager.Instance.InitializeAsync();

            initialLoading.Hide();
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
