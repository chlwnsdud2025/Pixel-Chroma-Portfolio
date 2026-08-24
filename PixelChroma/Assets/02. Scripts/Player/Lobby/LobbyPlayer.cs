using Mirror;
using PixelChroma.UI;
using UnityEngine;
using System;

namespace PixelChroma
{
    public class LobbyPlayer : PlayerBase
    {
        [Header("Lobby Ready UI")]
        [Tooltip("레디 시 켜질 체크마크 이미지 오브젝트를 연결해주세요.")]
        [SerializeField] private GameObject readyCheckObject;

        [Header("Crown")]
        [SerializeField] private GameObject crownObject;

        private LobbyPlayerState _lobbyState;

        private static event Action<uint> _onCrownReceived;

        protected override void Awake()
        {
            base.Awake();
            _lobbyState = GetComponent<LobbyPlayerState>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            LobbyPlayerState.OnReadyChanged += UpdateReadyImage;

            _onCrownReceived += HandleCrown;

            NetworkClient.RegisterHandler<CrownMessage>(OnCrownMessage);

            UpdateReadyImage();
            if (crownObject != null) crownObject.SetActive(false);
        }

        public override void OnStopClient()
        {
            LobbyPlayerState.OnReadyChanged -= UpdateReadyImage;
            _onCrownReceived -= HandleCrown;
            base.OnStopClient();
        }

        protected override void OnLocalPlayerStarted()
        {
            var lobby = UIManager.Instance.GetPanel<UILobby>();
            PaintController.BindHUD(lobby);
        }

        protected override void OnLocalPlayerStopped()
        {
            PaintController.UnbindHUD();
        }

        private void UpdateReadyImage()
        {
            if (readyCheckObject != null && _lobbyState != null)
            {
                readyCheckObject.SetActive(_lobbyState.isReady);
            }
        }

        private static void OnCrownMessage(CrownMessage msg)
        {
            _onCrownReceived?.Invoke(msg.topScorerNetId);
        }

        private void HandleCrown(uint topNetId)
        {
            if (crownObject == null) return;
            crownObject.SetActive(netId == topNetId);
        }
    }
}