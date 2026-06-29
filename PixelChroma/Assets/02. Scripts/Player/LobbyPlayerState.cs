using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace PixelChroma
{
    public class LobbyPlayerState : NetworkBehaviour
    {
        public static readonly List<LobbyPlayerState> AllPlayers = new();
        public static event Action OnLobbyPlayersChanged;

        [SyncVar(hook = nameof(OnReadyStateChanged))]
        public bool isReady = false;

        public override void OnStartClient()
        {
            AllPlayers.Add(this);
            OnLobbyPlayersChanged?.Invoke();
        }

        public override void OnStopClient()
        {
            AllPlayers.Remove(this);
            OnLobbyPlayersChanged?.Invoke();
        }

        [Command]
        public void CmdToggleReady()
        {
            isReady = !isReady;
        }

        private void OnReadyStateChanged(bool oldReady, bool newReady)
        {
            OnLobbyPlayersChanged?.Invoke();
        }
    }
}
