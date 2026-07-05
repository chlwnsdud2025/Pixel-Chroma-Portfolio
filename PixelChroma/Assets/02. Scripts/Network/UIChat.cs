using Mirror;
using PixelChroma.Network;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace PixelChroma.UI
{
    public class UIChat : UIPanel
    {
        [Header("Chat Settings")]
        [SerializeField] private int maxMessages = 50;

        [Header("Chat UI Elements")]
        [SerializeField] private GameObject chatPanel;
        [SerializeField] private Button openChatButton;
        [SerializeField] private Button closeChatButton;

        [SerializeField] private TMP_InputField chatInputField;
        [SerializeField] private Transform chatContentParent;
        [SerializeField] private GameObject chatTextPrefab;
        [SerializeField] private ScrollRect scrollRect;

        public static event Action<bool> OnChatToggled;

        private Queue<TMP_Text> _chatMessagePool = new Queue<TMP_Text>();

        private void OnEnable()
        {
            PlayerChat.OnMessageReceived += HandleNewMessage;
            chatInputField.onSubmit.AddListener(OnChatSubmit);

            if (openChatButton != null) openChatButton.onClick.AddListener(() => ToggleChat(!chatPanel.activeSelf));
            if (closeChatButton != null) closeChatButton.onClick.AddListener(() => ToggleChat(false));
        }

        private void OnDisable()
        {
            PlayerChat.OnMessageReceived -= HandleNewMessage;
            chatInputField.onSubmit.RemoveListener(OnChatSubmit);

            if (openChatButton != null) openChatButton.onClick.RemoveAllListeners();
            if (closeChatButton != null) closeChatButton.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            chatPanel.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // 1. T키로 채팅창 토글
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                // 텍스트를 입력 중일 때는 T를 눌러도 창이 꺼지지 않고 't'가 입력되도록 무시
                if (chatInputField.isFocused) return;

                ToggleChat(!chatPanel.activeSelf);
            }

            // 2. ESC키로 채팅창 닫기 (채팅 중단 및 빠져나가기)
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (chatPanel.activeSelf)
                {
                    ToggleChat(false);
                }
            }
        }

        public void ToggleChat(bool isOpen)
        {
            chatPanel.SetActive(isOpen);

            if (isOpen)
            {
                chatInputField.text = string.Empty;
                Canvas.ForceUpdateCanvases();
                chatInputField.Select();
                chatInputField.ActivateInputField();
            }
            else
            {
                chatInputField.DeactivateInputField();
            }

            OnChatToggled?.Invoke(isOpen);
        }

        private void HandleNewMessage(string sender, string message)
        {
            TMP_Text chatText;

            if (_chatMessagePool.Count < maxMessages)
            {
                GameObject newChat = Instantiate(chatTextPrefab, chatContentParent);
                chatText = newChat.GetComponent<TMP_Text>();
            }
            else
            {
                chatText = _chatMessagePool.Dequeue();
                chatText.transform.SetAsLastSibling();
            }

            chatText.text = $"[{sender}]:{message}";
            _chatMessagePool.Enqueue(chatText);

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void OnChatSubmit(string message)
        {
            PlayerChat localPlayerChat = NetworkClient.localPlayer?.GetComponent<PlayerChat>();

            if (localPlayerChat != null && !string.IsNullOrWhiteSpace(message))
            {
                localPlayerChat.SendChat(message);
            }

            // 채팅을 보내고 창을 닫지 않습니다. 
            // 대신 입력된 텍스트만 지우고 다시 입력할 수 있도록 포커스를 줍니다.
            chatInputField.text = string.Empty;
            chatInputField.ActivateInputField();
            chatInputField.Select();
        }
    }
}