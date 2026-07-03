using Mirror;
using PixelChroma.UI;
using UnityEngine;

namespace PixelChroma
{
    public class LobbyPlayer : PlayerBase
    {
        // 만약 미러 로비 시스템의 ReadyState를 쓰고 계시다면 [SyncVar] 훅을 사용하시거나 
        // Ready가 바뀔 때 이 가상 함수(UpdateNicknameText)를 재호출해주시면 됩니다.
        [SyncVar(hook = nameof(OnReadyStateChanged))]
        public bool isReady = false;

        private void OnReadyStateChanged(bool oldState, bool newState)
        {
            UpdateNicknameText();
        }

        protected override void OnLocalPlayerStarted()
        {
            var lobby = UIManager.Instance.CurrentScene as UILobby;
            BindPaintHUD(lobby);
        }

        protected override void OnLocalPlayerStopped()
        {
            UnbindPaintHUD();
        }

        // 💡 부모의 가상 함수를 재정의하여 레디 상태에 따라 닉네임 뒤에 초록색 체크(✔) 문양을 더해줍니다!
        protected override void UpdateNicknameText()
        {
            if (nicknameText == null) return;

            string displayName = playerName;

            if (isReady)
            {
                displayName += " <color=#00FF00>✔</color>";
            }

            nicknameText.text = displayName;
        }
    }
}