using Mirror;
using PixelChroma.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelChroma
{
    // 이제 LobbyPlayer는 독자적인 isReady 변수를 가지지 않고, 
    // 같은 프리팹에 붙어있는 LobbyPlayerState의 데이터를 가져다 씁니다.
    public class LobbyPlayer : PlayerBase
    {
        [Header("Lobby Ready UI")]
        [Tooltip("레디 시 켜질 체크마크 이미지 오브젝트를 연결해주세요.")]
        [SerializeField] private GameObject readyCheckObject;

        private LobbyPlayerState _lobbyState;

        protected override void Awake()
        {
            base.Awake();
            // 같은 프리팹에 있는 상태 관리 스크립트를 찾아옵니다.
            _lobbyState = GetComponent<LobbyPlayerState>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            LobbyPlayerState.OnReadyChanged += UpdateReadyImage;
            UpdateReadyImage();
        }

        public override void OnStopClient()
        {
            LobbyPlayerState.OnReadyChanged -= UpdateReadyImage;
            base.OnStopClient();
        }

        protected override void OnLocalPlayerStarted()
        {
            var lobby = UIManager.Instance.CurrentScene as UILobby;
            PaintController.BindHUD(lobby);
        }

        protected override void OnLocalPlayerStopped()
        {
            PaintController.UnbindHUD();
        }

        protected override void Update()
        {
            base.Update();
            if (!isLocalPlayer) return;

            // TODO: 테스트용 폼 전환 — 나중에 제거
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) paintCanvas.SwitchForm(PlayerPaintCanvas.PaintFormType.Default);
                if (kb.f2Key.wasPressedThisFrame) paintCanvas.SwitchForm(PlayerPaintCanvas.PaintFormType.Test1);
            }
        }

        private void UpdateReadyImage()
        {
            if (readyCheckObject != null && _lobbyState != null)
            {
                readyCheckObject.SetActive(_lobbyState.isReady);
            }
        }
    }
}