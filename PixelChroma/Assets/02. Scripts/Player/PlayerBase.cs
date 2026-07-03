using Mirror;
using PixelChroma.UI;
using System;
using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace PixelChroma
{
    public abstract class PlayerBase : NetworkBehaviour
    {
        protected const string ActionMapName = "PlayerControll";
        protected const string MoveActionName = "Move";
        protected const string TogglePaintActionName = "TogglePaint";
        protected const string EyedropperActionName = "Eyedropper";
        protected const string ZoomModifierActionName = "ZoomModifier";

        protected static readonly int MoveX = Animator.StringToHash("MoveX");
        protected static readonly int MoveY = Animator.StringToHash("MoveY");
        protected static readonly int IsMoving = Animator.StringToHash("IsMoving");

        private bool _isChatActive = false;

        [Header("Input")]
        public InputActionAsset inputActions;

        [Header("References")]
        public Animator animator;
        public PlayerPaintCanvas paintCanvas;

        [Header("Settings")]
        public float moveSpeed = 5f;

        [Header("Zoom (PPU)")]
        [SerializeField] private int maxPPU = 64;
        [SerializeField] private int zoomStepPPU = 2;

        [Header("Nickname UI")]
        [SerializeField] private TMP_Text nicknameText;

        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        public string playerName = "Player";

        [SyncVar]
        public PlayerRole role = PlayerRole.None;

        public event Action<bool> OnPaintModeChanged;
        public event Action<int> OnBrushSizeScrolled;
        public event Action<bool> OnEyedropperStateChanged;
        public event Action<Color> OnEyedropperPreview;
        public event Action<Color> OnEyedropperPicked;
        public Func<Color> PaintColorProvider;

        protected InputAction moveAction;
        protected InputAction togglePaintAction;
        protected InputAction eyedropperAction;
        protected InputAction zoomModifierAction;
        protected CinemachineCamera cinemachineCamera;
        protected bool isPaintMode;

        private Rigidbody2D _rb;
        private PixelPerfectCamera _pixelPerfectCamera;
        private int _originalPPU;
        private bool _isEyedropperActive;
        private bool _eyedropperLocked;
        private Color _eyedropperColor;
        private Texture2D _eyedropperCapture;
        private Coroutine _eyedropperCoroutine;

        // ── 💡 1. 로컬 플레이어 시작 시 닉네임 불러와서 전송 ──────────────────
        public override void OnStartLocalPlayer()
        {
            _rb = GetComponent<Rigidbody2D>();

            var map = inputActions.FindActionMap(ActionMapName);
            moveAction = map.FindAction(MoveActionName);
            togglePaintAction = map.FindAction(TogglePaintActionName);
            eyedropperAction = map.FindAction(EyedropperActionName);
            zoomModifierAction = map.FindAction(ZoomModifierActionName);
            moveAction.Enable();
            togglePaintAction.Enable();
            eyedropperAction.Enable();
            zoomModifierAction.Enable();

            UIChat.OnChatToggled += HandleChatToggled;

            SetupCinemachineFollow();
            OnLocalPlayerStarted();

            UpdateCursorIcon(); // 시작 시 커서 업데이트

            // 내 기기에 저장된 커스텀 닉네임이 있는지 확인 후 서버에 전달
            string mySavedName = PlayerPrefs.GetString(PixelChroma.UI.UIMainMenuNickname.NicknamePrefsKey,
                                 PixelChroma.UI.UIMainMenuNickname.GetDefaultSteamName());
            CmdSetNickname(mySavedName);
        }

        // 2. 닉네임 동기화 Command 함수
        [Command]
        private void CmdSetNickname(string nameToSet)
        {
            if (string.IsNullOrEmpty(nameToSet)) return;

            // 서버에서 닉네임 변수를 갱신하면 SyncVar에 의해 모든 클라이언트에게 뿌려짐
            playerName = nameToSet.Trim();
            Debug.Log($"[NetworkServer] {netId}번 플레이어 닉네임 세팅 완료: {playerName}");
        }
        // ─────────────────────────────────────────────────────────────

        protected virtual void OnLocalPlayerStarted() { }

        public override void OnStopLocalPlayer()
        {
            moveAction?.Disable();
            togglePaintAction?.Disable();
            eyedropperAction?.Disable();
            zoomModifierAction?.Disable();
            UIChat.OnChatToggled -= HandleChatToggled;
            StopEyedropperCapture();
            if (_eyedropperCapture != null)
            {
                Destroy(_eyedropperCapture);
                _eyedropperCapture = null;
            }

            OnLocalPlayerStopped();
        }

        protected virtual void OnLocalPlayerStopped() { }

        private void SetupCinemachineFollow()
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
            if (cinemachineCamera != null)
                cinemachineCamera.Follow = transform;

            var mainCam = Camera.main;
            if (mainCam != null)
            {
                _pixelPerfectCamera = mainCam.GetComponent<PixelPerfectCamera>();
                if (_pixelPerfectCamera != null)
                    _originalPPU = _pixelPerfectCamera.assetsPPU;
            }
        }

        protected virtual void Update()
        {
            if (!isLocalPlayer) return;

            if (_isChatActive) return;

            if (togglePaintAction.WasPressedThisFrame())
                TogglePaintMode();

            HandleScrollWheel();

            if (isPaintMode)
            {
                HandleEyedropper();
                HandlePainting();
            }

            if (isClient && nicknameText != null)
            {
                UpdateNicknameVisibility();
            }

            UpdateAnimation();
        }

        protected virtual void FixedUpdate()
        {
            if (!isLocalPlayer) return;
            if (_isChatActive) return;
            HandleMovement();
        }

        private void OnPlayerNameChanged(string oldName, string newName)
        {
            if (nicknameText != null)
                nicknameText.text = newName;
        }

        protected virtual void HandleMovement()
        {
            if (_rb == null) return;

            Vector2 input = moveAction.ReadValue<Vector2>();
            _rb.linearVelocity = input.sqrMagnitude > 0.01f
                ? input.normalized * moveSpeed
                : Vector2.zero;
        }

        protected virtual void UpdateAnimation()
        {
            if (animator == null) return;

            Vector2 input = moveAction.ReadValue<Vector2>();
            bool moving = input.sqrMagnitude > 0.01f;

            animator.SetBool(IsMoving, moving);
            if (moving)
            {
                animator.SetFloat(MoveX, input.x);
                animator.SetFloat(MoveY, input.y);
            }
        }

        protected void HandleZoom()
        {
            if (_pixelPerfectCamera == null || Mouse.current == null) return;

            float scroll = Mouse.current.scroll.y.ReadValue();
            if (Mathf.Approximately(scroll, 0f)) return;

            if (zoomModifierAction.IsPressed())
            {
                int current = _pixelPerfectCamera.assetsPPU;
                int delta = scroll > 0 ? zoomStepPPU : -zoomStepPPU;
                _pixelPerfectCamera.assetsPPU = Mathf.Clamp(current + delta, _originalPPU, maxPPU);
            }
        }

        protected float GetScrollValue()
        {
            if (Mouse.current == null) return 0f;
            return Mouse.current.scroll.y.ReadValue();
        }

        protected bool IsAltHeld()
        {
            return zoomModifierAction.IsPressed();
        }

        private void HandleScrollWheel()
        {
            float scroll = GetScrollValue();
            if (Mathf.Approximately(scroll, 0f)) return;

            if (IsAltHeld())
            {
                HandleZoom();
                return;
            }

            if (isPaintMode && paintCanvas != null && _boundPicker != null)
            {
                int max = _boundPicker.MaxBrushSize;
                int current = paintCanvas.BrushSize;
                int next = scroll > 0 ? current + 1 : current - 1;
                next = Mathf.Clamp(next, 1, max);
                paintCanvas.BrushSize = next;
                OnBrushSizeScrolled?.Invoke(next);
            }
        }

        private void TogglePaintMode()
        {
            isPaintMode = !isPaintMode;
            OnPaintModeChanged?.Invoke(isPaintMode);
            UpdateCursorIcon();
        }

        protected virtual CursorType GetDefaultCursorType()
        {
            return CursorType.Default;
        }

        protected void UpdateCursorIcon()
        {
            if (!isLocalPlayer || CursorManager.Instance == null) return;

            if (!isPaintMode)
            {
                CursorManager.Instance.SetCursorType(GetDefaultCursorType());
            }
            else
            {
                if (_isEyedropperActive || (_boundPicker != null && _boundPicker.IsEyedropperMode))
                {
                    CursorManager.Instance.SetCursorType(CursorType.Eyedropper);
                }
                else if (_boundPicker != null && _boundPicker.IsEraserMode)
                {
                    float scale = 1f + (_boundPicker.BrushSize * 0.1f);
                    CursorManager.Instance.SetCursorType(CursorType.Eraser, scale);
                }
                else
                {
                    float scale = 1f + ((_boundPicker != null ? _boundPicker.BrushSize : 1) * 0.1f);
                    CursorManager.Instance.SetCursorType(CursorType.Brush, scale);
                }
            }
        }

        private void HandleEyedropper()
        {
            bool spaceHeld = eyedropperAction.IsPressed();
            if (!spaceHeld) _eyedropperLocked = false;

            if (spaceHeld && !_isEyedropperActive && !_eyedropperLocked)
            {
                _boundPicker?.SetEyedropper(true);
            }
            else if (!spaceHeld && _isEyedropperActive && (_boundPicker == null || !_boundPicker.IsEyedropperMode))
            {
                _boundPicker?.SetEyedropper(false);
            }

            if (_isEyedropperActive && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

                _boundPicker?.SetEyedropper(false);
                _eyedropperLocked = true;
                OnEyedropperPicked?.Invoke(_eyedropperColor);
            }
        }

        private void HandleUIEyedropperToggle(bool isActive)
        {
            if (isActive && !_isEyedropperActive)
            {
                _isEyedropperActive = true;
                _eyedropperCoroutine = StartCoroutine(EyedropperCaptureLoop());
                OnEyedropperStateChanged?.Invoke(true);
            }
            else if (!isActive && _isEyedropperActive)
            {
                StopEyedropperCapture();
                _isEyedropperActive = false;
                OnEyedropperStateChanged?.Invoke(false);
            }
            UpdateCursorIcon();
        }

        private void HandleUIEraserToggle(bool isActive)
        {
            UpdateCursorIcon();
        }

        private IEnumerator EyedropperCaptureLoop()
        {
            if (_eyedropperCapture == null)
                _eyedropperCapture = new Texture2D(1, 1, TextureFormat.RGB24, false);

            while (true)
            {
                yield return new WaitForEndOfFrame();

                if (Mouse.current == null) continue;

                Vector2 screenPos = Mouse.current.position.ReadValue();
                int x = (int)screenPos.x;
                int y = (int)screenPos.y;

                if (x >= 0 && x < Screen.width && y >= 0 && y < Screen.height)
                {
                    _eyedropperCapture.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
                    _eyedropperCapture.Apply();
                    _eyedropperColor = _eyedropperCapture.GetPixel(0, 0);
                    OnEyedropperPreview?.Invoke(_eyedropperColor);
                }
            }
        }

        private void StopEyedropperCapture()
        {
            if (_eyedropperCoroutine != null)
            {
                StopCoroutine(_eyedropperCoroutine);
                _eyedropperCoroutine = null;
            }
        }

        private void HandlePainting()
        {
            if (_isEyedropperActive) return;
            if (paintCanvas == null || Mouse.current == null) return;
            if (!Mouse.current.leftButton.isPressed) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            worldPos.z = 0;

            Vector2Int pixel = paintCanvas.WorldToPixel(worldPos);
            if (paintCanvas.IsInBounds(pixel.x, pixel.y))
            {
                if (_boundPicker != null && _boundPicker.IsEraserMode)
                {
                    paintCanvas.EraseAt(pixel.x, pixel.y);
                }
                else
                {
                    Color color = PaintColorProvider?.Invoke() ?? Color.white;
                    paintCanvas.PaintAt(pixel.x, pixel.y, color);
                }
            }
        }

        private IPaintHUD _boundHUD;
        protected UIColorPicker _boundPicker;

        protected void BindPaintHUD(IPaintHUD hud)
        {
            if (hud == null) return;

            _boundHUD = hud;
            _boundPicker = hud.ColorPicker;

            OnPaintModeChanged += hud.SetPaintMode;

            if (_boundPicker != null)
            {
                PaintColorProvider = () => _boundPicker.SelectedColor;
                OnBrushSizeScrolled += _boundPicker.SetBrushSize;
                OnEyedropperPreview += _boundPicker.SetEyedropperPreview;
                OnEyedropperPicked += _boundPicker.SetColor;
                _boundPicker.OnBrushSizeChanged += OnUIBrushSizeChanged;

                _boundPicker.OnEyedropperModeChanged += HandleUIEyedropperToggle;
                _boundPicker.OnEraserModeChanged += HandleUIEraserToggle;

                if (paintCanvas != null)
                    paintCanvas.BrushSize = _boundPicker.BrushSize;
            }

            UpdateCursorIcon();
        }

        protected void UnbindPaintHUD()
        {
            if (_boundPicker != null)
            {
                _boundPicker.OnBrushSizeChanged -= OnUIBrushSizeChanged;
                _boundPicker.OnEyedropperModeChanged -= HandleUIEyedropperToggle;
                _boundPicker.OnEraserModeChanged -= HandleUIEraserToggle;
            }

            OnPaintModeChanged = null;
            OnBrushSizeScrolled = null;
            OnEyedropperStateChanged = null;
            OnEyedropperPreview = null;
            OnEyedropperPicked = null;
            PaintColorProvider = null;

            _boundHUD = null;
            _boundPicker = null;
        }

        private void OnUIBrushSizeChanged(int size)
        {
            if (paintCanvas != null)
                paintCanvas.BrushSize = size;

            UpdateCursorIcon();
        }

        private void HandleChatToggled(bool isActive)
        {
            _isChatActive = isActive;

            if (_isChatActive)
            {
                if (_rb != null) _rb.linearVelocity = Vector2.zero;
                if (animator != null) animator.SetBool(IsMoving, false);
            }
        }

        private void UpdateNicknameVisibility()
        {
            if (NetworkClient.localPlayer == null) return;

            PlayerBase localPlayer = NetworkClient.localPlayer.GetComponent<PlayerBase>();
            if (localPlayer == null) return;

            if (this == localPlayer)
            {
                nicknameText.enabled = true;
                return;
            }

            if (localPlayer.role == PlayerRole.None || this.role == PlayerRole.None)
            {
                nicknameText.enabled = true;
                return;
            }

            if (localPlayer is RunnerPlayer localRunner && localRunner.isDead)
            {
                nicknameText.enabled = true;
                return;
            }

            PlayerRole localRole = localPlayer.role;
            PlayerRole targetRole = this.role;

            bool shouldShow = false;

            if (localRole == PlayerRole.Seeker && targetRole == PlayerRole.Seeker)
            {
                shouldShow = true;
            }
            else if (localRole == PlayerRole.Runner && targetRole == PlayerRole.Runner)
            {
                bool isInfectionMode = false;
                shouldShow = !isInfectionMode;
            }
            else
            {
                shouldShow = false;
            }

            nicknameText.enabled = shouldShow;
        }
    }
}