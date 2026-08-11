using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

namespace PixelChroma.UI
{
    public class UIMemberSlot : MonoBehaviour
    {
        [SerializeField] private OneTextLabel nameText;
        [SerializeField] private GameObject readyIcon;
        [SerializeField] private GameObject hostIcon;
        [SerializeField] private Button kickButton;

        private LobbyPlayerState _player;

        private void Awake()
        {
            if (kickButton != null)
                kickButton.AddClickListener(OnClickKick);
        }

        public void Set(LobbyPlayerState player, bool isHost, bool isLocalHost)
        {
            _player = player;

            var playerBase = player.GetComponent<PlayerBase>();
            string playerName = playerBase != null ? playerBase.playerName : "Player";

            if (nameText != null)
                nameText.text = playerName;

            if (readyIcon != null)
                readyIcon.SetActive(player.isReady);

            if (hostIcon != null)
                hostIcon.SetActive(isHost);

            if (kickButton != null)
                kickButton.gameObject.SetActive(isLocalHost && !isHost);
        }

        private void OnClickKick()
        {
            if (_player == null) return;

            var localPlayer = LobbyPlayerState.AllPlayers[0];
            if (localPlayer != null && localPlayer.isLocalPlayer)
                localPlayer.CmdKickPlayer(_player.netId);
        }
    }
}
