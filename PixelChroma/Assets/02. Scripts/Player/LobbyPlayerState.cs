using UnityEngine;
using Mirror;
using PixelChroma.UI; // 새로운 UI 네임스페이스 추가

namespace PixelChroma
{
    public class LobbyPlayerState : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnReadyStateChanged))]
        public bool isReady = false;

        public override void OnStartLocalPlayer()
        {
            // [수정됨] UIManager를 통해 현재 띄워진 UILobby 패널을 찾습니다.
            var uiLobby = UIManager.Instance.GetPanel<UILobby>();
            if (uiLobby != null)
            {
                uiLobby.localLobbyPlayer = this;
            }
        }

        [Command]
        public void CmdToggleReady()
        {
            isReady = !isReady;
        }

        private void OnReadyStateChanged(bool oldReady, bool newReady)
        {
            var uiLobby = UIManager.Instance.GetPanel<UILobby>();
            if (uiLobby != null)
            {
                uiLobby.CheckAllReadyStates();
            }
        }

        public override void OnStartClient()
        {
            var uiLobby = UIManager.Instance.GetPanel<UILobby>();
            if (uiLobby != null)
            {
                uiLobby.CheckAllReadyStates();
            }
        }

        public override void OnStopClient()
        {
            var uiLobby = UIManager.Instance.GetPanel<UILobby>();
            if (uiLobby != null)
            {
                // UILobby 클래스 내부에 있는 CheckAllReadyStates 함수를 0.1초 뒤에 실행
                uiLobby.Invoke(nameof(UILobby.CheckAllReadyStates), 0.1f);
            }
        }
    }
}