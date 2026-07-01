using System.Collections.Generic;
using Mirror;
using PixelChroma.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // ScrollRect 사용을 위해 추가

namespace PixelChroma.UI
{
    public class UIChat : UIPanel
    {
        [Header("Chat Settings")]
        [SerializeField] private int maxMessages = 50; // 최대 유지할 채팅 개수

        [Header("Chat UI Elements")]
        [SerializeField] private TMP_InputField chatInputField;
        [SerializeField] private Transform chatContentParent;
        [SerializeField] private GameObject chatTextPrefab;
        [SerializeField] private ScrollRect scrollRect; // 채팅이 오면 맨 아래로 내리기 위함

        // 풀링을 위한 큐(Queue) 자료구조
        private Queue<TMP_Text> _chatMessagePool = new Queue<TMP_Text>();

        private void OnEnable()
        {
            PlayerChat.OnMessageReceived += HandleNewMessage;
            chatInputField.onSubmit.AddListener(OnChatSubmit);
        }

        private void OnDisable()
        {
            PlayerChat.OnMessageReceived -= HandleNewMessage;
            chatInputField.onSubmit.RemoveListener(OnChatSubmit);
        }

        private void HandleNewMessage(string sender, string message)
        {
            TMP_Text chatText;

            // 1. 최대 개수에 도달하지 않았다면 새로 생성
            if (_chatMessagePool.Count < maxMessages)
            {
                GameObject newChat = Instantiate(chatTextPrefab, chatContentParent);
                chatText = newChat.GetComponent<TMP_Text>();
            }
            // 2. 최대 개수에 도달했다면 가장 오래된 텍스트 재활용
            else
            {
                chatText = _chatMessagePool.Dequeue(); // 큐에서 가장 먼저 들어온(맨 위) 녀석을 뺌
                chatText.transform.SetAsLastSibling(); // 하이라키 상에서 Content의 가장 아래쪽으로 이동시킴
            }

            // 3. 텍스트 내용 갱신 및 큐에 다시 삽입
            chatText.text = $"<b>[{sender}]</b>: {message}";
            _chatMessagePool.Enqueue(chatText);

            // 4. (선택) 새 채팅이 왔을 때 스크롤 맨 아래로 자동 이동
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases(); // UI 레이아웃 크기를 강제로 즉시 갱신
                scrollRect.verticalNormalizedPosition = 0f; // 스크롤바를 0(맨 아래)으로 설정
            }
        }

        private void OnChatSubmit(string message)
        {
            PlayerChat localPlayerChat = NetworkClient.localPlayer?.GetComponent<PlayerChat>();

            if (localPlayerChat != null)
            {
                localPlayerChat.SendChat(message);
            }

            chatInputField.text = string.Empty;
            chatInputField.ActivateInputField();
        }
    }
}