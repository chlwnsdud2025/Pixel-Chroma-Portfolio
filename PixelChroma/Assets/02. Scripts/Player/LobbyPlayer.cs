using Mirror;
using PixelChroma.UI;
using UnityEngine;

namespace PixelChroma
{
    public class LobbyPlayer : PlayerBase
    {
        [Header("Lobby Ready UI")]
        [Tooltip("레디 시 켜질 체크마크 이미지 오브젝트를 연결해주세요.")]
        [SerializeField] private GameObject readyCheckObject;

        [SyncVar(hook = nameof(OnReadyStateChanged))]
        public bool isReady = false;

        private void OnReadyStateChanged(bool oldState, bool newState)
        {
            UpdateReadyImage();
            UpdateNicknameText();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            UpdateReadyImage(); // 처음 스폰되었을 때 상태 동기화
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

        // 텍스트는 이제 순수하게 닉네임만 출력합니다.
        protected override void UpdateNicknameText()
        {
            if (nicknameText == null) return;
            nicknameText.text = playerName;
        }

        // 레디 상태에 따라 체크마크 오브젝트를 켜고 끕니다.
        private void UpdateReadyImage()
        {
            if (readyCheckObject != null)
            {
                readyCheckObject.SetActive(isReady);
            }
        }
    }
}