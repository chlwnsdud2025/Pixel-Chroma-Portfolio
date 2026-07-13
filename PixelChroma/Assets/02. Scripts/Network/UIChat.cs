using Mirror;
using PixelChroma.Network;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        // 풀링 시스템 세분화 (화면에 떠있는 큐, 렌더링이 꺼진 대기 큐)
        private Queue<TMP_Text> _activeMessages = new Queue<TMP_Text>();
        private Queue<TMP_Text> _inactivePool = new Queue<TMP_Text>();

        private void OnEnable()
        {
            PlayerChat.OnMessageReceived += HandleNewMessage;
            chatInputField.onSubmit.AddListener(OnChatSubmit);

            if (openChatButton != null) openChatButton.onClick.AddListener(() => ToggleChat(!chatPanel.activeSelf));
            if (closeChatButton != null) closeChatButton.onClick.AddListener(() => ToggleChat(false));

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            PlayerChat.OnMessageReceived -= HandleNewMessage;
            chatInputField.onSubmit.RemoveListener(OnChatSubmit);

            if (openChatButton != null) openChatButton.onClick.RemoveAllListeners();
            if (closeChatButton != null) closeChatButton.onClick.RemoveAllListeners();

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            chatPanel.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                if (chatInputField.isFocused) return;
                ToggleChat(!chatPanel.activeSelf);
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (chatPanel.activeSelf)
                {
                    ToggleChat(false);
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearChat();
        }

        // Destroy 없이 순수 풀링으로만 관리하는 클린업 로직
        public void ClearChat()
        {
            // 화면에 떠있는 메시지들을 모두 대기열(Pool)로 반환합니다.
            while (_activeMessages.Count > 0)
            {
                TMP_Text chat = _activeMessages.Dequeue();
                chat.gameObject.SetActive(false); // 렌더링 끄기
                _inactivePool.Enqueue(chat);      // 재사용 대기열에 삽입
            }

            if (chatInputField != null) chatInputField.text = string.Empty;
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

            // 1. 활성 메시지가 최대치(50개)에 도달했으면, 가장 오래된 것을 재사용
            if (_activeMessages.Count >= maxMessages)
            {
                chatText = _activeMessages.Dequeue();
            }
            // 2. 대기열(Pool)에 쉬고 있는 오브젝트가 있으면 꺼내 쓰기
            else if (_inactivePool.Count > 0)
            {
                chatText = _inactivePool.Dequeue();
                chatText.gameObject.SetActive(true); // 다시 켜기
            }
            // 3. 둘 다 아니면 (최초 생성 시기) 새로 Instantiate
            else
            {
                GameObject newChat = Instantiate(chatTextPrefab, chatContentParent);
                chatText = newChat.GetComponent<TMP_Text>();
            }

            // UI 맨 아래로 보내기
            chatText.transform.SetAsLastSibling();
            chatText.text = $"[{sender}]: {message}";

            // 활성 큐에 등록
            _activeMessages.Enqueue(chatText);

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

            chatInputField.text = string.Empty;
            chatInputField.ActivateInputField();
            chatInputField.Select();
        }
    }
}