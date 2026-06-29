using Mirror;
using UnityEngine;
using System;

namespace PixelChroma.Network
{
    public class PlayerChat : NetworkBehaviour
    {
        // UI에서 이벤트(채팅 수신)를 구독할 수 있도록 Action 델리게이트 사용
        public static event Action<string, string> OnMessageReceived;

        // UI 창에서 사용자가 엔터를 치면 이 함수를 호출합니다.
        public void SendChat(string message)
        {
            if (!isLocalPlayer) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            // 스팀 닉네임을 가져와서 서버로 전송 (임시로 netId 사용 가능)
            string senderName = Steamworks.SteamFriends.GetPersonaName();

            CmdSendMessage(senderName, message);
        }

        // [클라이언트 -> 서버] "나 이 채팅 칠게요!"
        [Command]
        private void CmdSendMessage(string senderName, string message)
        {
            // (선택) 서버 단에서 욕설 필터링이나 도배 방지 로직을 여기에 넣을 수 있습니다.

            // [서버 -> 모든 클라이언트] "A가 이런 채팅을 쳤다, 다들 화면에 띄워라!"
            RpcReceiveMessage(senderName, message);
        }

        [ClientRpc]
        private void RpcReceiveMessage(string senderName, string message)
        {
            // 이벤트 발생 -> UI 스크립트가 이 소식을 듣고 화면에 텍스트를 그립니다.
            OnMessageReceived?.Invoke(senderName, message);
        }
    }
}