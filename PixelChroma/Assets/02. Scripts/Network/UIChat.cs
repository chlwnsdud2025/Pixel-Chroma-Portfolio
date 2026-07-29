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
    public class UIChat : UIPanel, IEscapeHandler
    {
        private const string ActionMapName = "PlayerControll";
        private const string ActionChatToggle = "ChatToggle";

        [Header("Chat Settings")]
        [SerializeField] private int maxMessages = 50;

        private InputAction _chatToggleAction;

        // 각 메시지가 표시된 뒤 사라지기 시작하는 시간(초)
        [SerializeField] private float messageDisplayDuration = 5f;
        // 페이드아웃에 걸리는 시간(초)
        [SerializeField] private float messageFadeDuration = 1f;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Chat Colors")]
        // 본인 채팅 색상 (주황)
        [SerializeField] private Color myMessageColor = new Color(1f, 0.6f, 0.2f);
        // 타인 채팅 색상 (파랑)
        [SerializeField] private Color otherMessageColor = new Color(0.4f, 0.7f, 1f);

        [Header("Chat UI Elements")]
        [SerializeField] private CanvasGroup chatCanvasGroup;    // 채팅창 전체 투명도 제어용
        [SerializeField] private Button openChatButton;          // InputField 활성화 버튼
        [SerializeField] private TMP_InputField chatInputField;
        [SerializeField] private Transform chatContentParent;
        [SerializeField] private GameObject chatTextPrefab;
        [SerializeField] private ScrollRect scrollRect;

        // InputField 활성/비활성 이벤트 (PlayerBase의 입력 차단용)
        public static event Action<bool> OnChatToggled;

        // 활성 메시지 큐 (화면에 표시 중)
        private readonly Queue<ChatEntry> _activeMessages = new();
        // 비활성 풀 (재사용 대기)
        private readonly Queue<TMP_Text> _inactivePool = new();

        // InputField가 현재 포커스 중인지
        private bool _isInputActive;
        // ActivateInputField 후 실제 포커스가 잡힐 때까지 대기 중인지
        private bool _waitingForFocus;

        // ── 메시지 항목: TMP_Text + 타이머 정보를 묶어서 관리 ──
        private struct ChatEntry
        {
            public TMP_Text text;
            public CanvasGroup canvasGroup;  // 개별 메시지 페이드용
            public float spawnTime;          // 생성 시각
            public bool isFading;            // 페이드 진행 중 여부
        }

        private void OnEnable()
        {
            // 변경: 시그니처에 netId 추가
            PlayerChat.OnMessageReceived += HandleNewMessage;
            chatInputField.onSubmit.AddListener(OnChatSubmit);

            if (openChatButton != null)
                openChatButton.onClick.AddListener(() => ToggleInput(!_isInputActive));

            SceneManager.sceneLoaded += OnSceneLoaded;

            if (inputActions != null)
            {
                var map = inputActions.FindActionMap(ActionMapName);
                _chatToggleAction = map?.FindAction(ActionChatToggle);
                _chatToggleAction?.Enable();
            }

            // ESC는 UIManager가 유일 리더로서 최우선 처리하도록 위임한다.
            if (UIManager.Instance != null)
                UIManager.Instance.ChatEscapeHandler = this;
        }

        private void OnDisable()
        {
            PlayerChat.OnMessageReceived -= HandleNewMessage;
            chatInputField.onSubmit.RemoveListener(OnChatSubmit);

            if (openChatButton != null)
                openChatButton.onClick.RemoveAllListeners();

            _chatToggleAction?.Disable();

            if (UIManager.Instance != null && ReferenceEquals(UIManager.Instance.ChatEscapeHandler, this))
                UIManager.Instance.ChatEscapeHandler = null;

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            // 채팅창은 항상 활성, InputField만 비활성 상태로 시작
            chatInputField.gameObject.SetActive(false);
            _isInputActive = false;

            // 초기에는 메시지가 없으므로 채팅창 투명
            if (chatCanvasGroup != null)
                chatCanvasGroup.alpha = 0f;
        }

        private void Update()
        {
            HandleInputToggle();
            HandleFocusLost();
            UpdateMessageFade();
            UpdateChatVisibility();
        }

        // ── T키 처리: InputField 포커스만 제어
        private void HandleInputToggle()
        {
            if (_chatToggleAction != null && _chatToggleAction.WasPressedThisFrame())
            {
                if (chatInputField.isFocused) return;
                ToggleInput(!_isInputActive);
            }
        }

        // ── ESC 처리: 입력창이 열려 있으면 닫고 소비.
        public bool HandleEscape()
        {
            if (!_isInputActive) return false;

            ToggleInput(false);
            return true;
        }

        // ── 마우스 클릭 등으로 InputField 포커스를 잃었을 때 비활성화 ──
        private void HandleFocusLost()
        {
            if (!_isInputActive) return;

            // 아직 포커스가 잡히기 전이면 잡힐 때까지 판정 보류
            if (_waitingForFocus)
            {
                if (chatInputField.isFocused)
                    _waitingForFocus = false;
                return;
            }

            if (!chatInputField.isFocused)
                ToggleInput(false);
        }

        // ── 각 메시지의 페이드아웃 처리 ──
        private void UpdateMessageFade()
        {
            float now = Time.time;
            var tempList = new List<ChatEntry>();

            // 큐를 순회하면서 시간이 지난 메시지를 페이드 처리
            while (_activeMessages.Count > 0)
            {
                var entry = _activeMessages.Peek();

                float elapsed = now - entry.spawnTime;
                float fadeStart = messageDisplayDuration;
                float fadeEnd = messageDisplayDuration + messageFadeDuration;

                if (elapsed >= fadeEnd)
                {
                    // 페이드 완료 → 풀로 반환
                    _activeMessages.Dequeue();
                    entry.text.gameObject.SetActive(false);
                    if (entry.canvasGroup != null) entry.canvasGroup.alpha = 1f;
                    _inactivePool.Enqueue(entry.text);
                }
                else
                {
                    // 아직 사라질 시간이 안 됨 → 나머지도 더 이전이므로 중단
                    // (큐는 시간순이라 앞이 안 됐으면 뒤도 안 됨)
                    if (elapsed >= fadeStart && entry.canvasGroup != null)
                    {
                        float t = (elapsed - fadeStart) / messageFadeDuration;
                        entry.canvasGroup.alpha = 1f - t;
                    }
                    break;
                }
            }

            // 큐 중간 항목들도 페이드 체크 (Peek/Dequeue만으론 중간 접근 불가하므로 배열 변환)
            var array = _activeMessages.ToArray();
            for (int i = 0; i < array.Length; i++)
            {
                float elapsed = now - array[i].spawnTime;
                float fadeStart = messageDisplayDuration;

                if (elapsed >= fadeStart && array[i].canvasGroup != null)
                {
                    float t = Mathf.Clamp01((elapsed - fadeStart) / messageFadeDuration);
                    array[i].canvasGroup.alpha = 1f - t;
                }
            }
        }

        // ── 채팅창 전체 투명도 제어 ──
        private void UpdateChatVisibility()
        {
            if (chatCanvasGroup == null) return;

            // InputField가 열려있으면 항상 보이게
            bool shouldShow = _isInputActive || _activeMessages.Count > 0;

            float targetAlpha = shouldShow ? 1f : 0f;
            chatCanvasGroup.alpha = Mathf.MoveTowards(chatCanvasGroup.alpha, targetAlpha, Time.deltaTime * 3f);
        }

        // ── InputField 토글 (기존 ToggleChat 대체) ──
        public void ToggleInput(bool open)
        {
            _isInputActive = open;
            chatInputField.gameObject.SetActive(open);

            if (open)
            {
                chatInputField.text = string.Empty;
                chatInputField.Select();
                chatInputField.ActivateInputField();
                _waitingForFocus = true;

                // InputField 열릴 때 기존 메시지들을 다시 보이게 (페이드 리셋)
                RefreshAllMessageAlpha();

                // 채팅창 전체도 즉시 보이게
                if (chatCanvasGroup != null)
                    chatCanvasGroup.alpha = 1f;
            }
            else
            {
                chatInputField.DeactivateInputField();
            }

            // PlayerBase에 입력 차단 통지 (기존 연동 유지)
            OnChatToggled?.Invoke(open);
        }

        // InputField 열릴 때 모든 메시지를 불투명하게 복원
        private void RefreshAllMessageAlpha()
        {
            foreach (var entry in _activeMessages)
            {
                if (entry.canvasGroup != null)
                    entry.canvasGroup.alpha = 1f;
            }
        }

        // ── 새 메시지 수신 ──
        private void HandleNewMessage(uint senderNetId, string sender, string message)
        {
            TMP_Text chatText;
            CanvasGroup cg;

            // 풀에서 가져오거나 새로 생성
            if (_activeMessages.Count >= maxMessages)
            {
                var oldest = _activeMessages.Dequeue();
                chatText = oldest.text;
                cg = oldest.canvasGroup;
            }
            else if (_inactivePool.Count > 0)
            {
                chatText = _inactivePool.Dequeue();
                cg = chatText.GetComponent<CanvasGroup>();
            }
            else
            {
                GameObject newChat = Instantiate(chatTextPrefab, chatContentParent);
                chatText = newChat.GetComponent<TMP_Text>();
                // CanvasGroup이 없으면 추가 (개별 메시지 페이드용)
                cg = newChat.GetComponent<CanvasGroup>();
                if (cg == null) cg = newChat.AddComponent<CanvasGroup>();
            }

            chatText.gameObject.SetActive(true);
            chatText.transform.SetAsLastSibling();

            // 본인 여부 판별 → 색상 적용
            bool isMine = NetworkClient.localPlayer != null
                          && NetworkClient.localPlayer.netId == senderNetId;

            string colorHex = ColorUtility.ToHtmlStringRGB(isMine ? myMessageColor : otherMessageColor);
            chatText.text = $"<color=#{colorHex}>[{sender}]: {message}</color>";

            // CanvasGroup 초기화 (불투명)
            if (cg != null) cg.alpha = 1f;

            // 활성 큐에 등록
            _activeMessages.Enqueue(new ChatEntry
            {
                text = chatText,
                canvasGroup = cg,
                spawnTime = Time.time,
                isFading = false,
            });

            // 채팅창 즉시 보이게
            if (chatCanvasGroup != null)
                chatCanvasGroup.alpha = 1f;

            // 스크롤 맨 아래로
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        // ── 메시지 전송 ──
        private void OnChatSubmit(string message)
        {
            PlayerChat localPlayerChat = NetworkClient.localPlayer?.GetComponent<PlayerChat>();

            if (localPlayerChat != null && !string.IsNullOrWhiteSpace(message))
                localPlayerChat.SendChat(message);

            chatInputField.text = string.Empty;
            chatInputField.ActivateInputField();
            chatInputField.Select();

            // 제출 후 재활성화도 포커스 반영에 시간이 걸리므로 유예
            _waitingForFocus = true;
        }

        // ── 씬 전환 시 정리 ──
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearChat();
        }

        public void ClearChat()
        {
            while (_activeMessages.Count > 0)
            {
                var entry = _activeMessages.Dequeue();
                entry.text.gameObject.SetActive(false);
                if (entry.canvasGroup != null) entry.canvasGroup.alpha = 1f;
                _inactivePool.Enqueue(entry.text);
            }

            if (chatInputField != null) chatInputField.text = string.Empty;
        }
    }
}