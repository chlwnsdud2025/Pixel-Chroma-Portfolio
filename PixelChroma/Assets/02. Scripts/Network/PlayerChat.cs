using Mirror;
using UnityEngine;
using System;

namespace PixelChroma.Network
{
    public class PlayerChat : NetworkBehaviour
    {
        public static event Action<string, string> OnMessageReceived;

        public void SendChat(string message)
        {
            if (!isLocalPlayer) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            // 스팀 API를 직접 매번 파는 대신, PlayerBase에 주입 완료된 
            // NetworkManager 공통 동기화 닉네임(playerName)을 꺼내서 서버로 보냅니다!
            string currentNickname = "Player";
            if (TryGetComponent<PlayerBase>(out var playerBase))
            {
                currentNickname = playerBase.playerName;
            }

            CmdSendMessage(currentNickname, message);
        }

        [Command]
        private void CmdSendMessage(string senderName, string message)
        {
            RpcReceiveMessage(senderName, message);
        }

        [ClientRpc]
        private void RpcReceiveMessage(string senderName, string message)
        {
            OnMessageReceived?.Invoke(senderName, message);
        }
    }
}