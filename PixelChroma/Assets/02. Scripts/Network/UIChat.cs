using Mirror;
using PixelChroma.Network;
using TMPro;
using UnityEngine;

namespace PixelChroma.UI
{
    public class UIChat : UIPanel
    {
        [Header("Chat UI Elements")]
        [SerializeField] private TMP_InputField chatInputField;
        [SerializeField] private Transform chatContentParent; // ScrollView의 Content
        [SerializeField] private GameObject chatTextPrefab;   // 개별 채팅 메시지 프리팹

        // override와 base 호출을 제거한 순수 유니티 이벤트 함수
        private void OnEnable()
        {
            // 네트워크 객체에서 채팅이 오면 처리하도록 이벤트 구독
            PlayerChat.OnMessageReceived += HandleNewMessage;

            // 인풋 필드에서 엔터를 쳤을 때 발송되도록 리스너 등록
            chatInputField.onSubmit.AddListener(OnChatSubmit);
        }

        // override와 base 호출을 제거한 순수 유니티 이벤트 함수
        private void OnDisable()
        {
            PlayerChat.OnMessageReceived -= HandleNewMessage;
            chatInputField.onSubmit.RemoveListener(OnChatSubmit);
        }

        private void HandleNewMessage(string sender, string message)
        {
            // 채팅 프리팹을 생성하고 텍스트를 세팅합니다.
            GameObject newChat = Instantiate(chatTextPrefab, chatContentParent);
            TMP_Text chatText = newChat.GetComponent<TMP_Text>();
            chatText.text = $"<b>[{sender}]</b>: {message}";
        }

        private void OnChatSubmit(string message)
        {
            // 현재 내 권한(LocalPlayer)을 가진 PlayerChat 컴포넌트를 찾습니다.
            PlayerChat localPlayerChat = NetworkClient.localPlayer?.GetComponent<PlayerChat>();

            if (localPlayerChat != null)
            {
                localPlayerChat.SendChat(message);
            }

            // 전송 후 입력창 비우고 포커스 유지
            chatInputField.text = string.Empty;
            chatInputField.ActivateInputField();
        }
    }
}