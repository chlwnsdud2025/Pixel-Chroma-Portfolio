using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace PixelChroma.UI
{
    public class UIMainMenuNickname : UIPopup
    {
        public const string NicknamePrefsKey = "CustomPlayerNickname";

        [Header("Nickname Limits Settings")]
        [Tooltip("닉네임 최대 글자 수 제한 설정")]
        [SerializeField] private int maxNicknameLength = 10; 

        // [블랙리스트 명부] 여기에 필터링할 비속어 단어들을 적어두시면 됩니다.
        [Header("Banned Words (비속어 필터링)")]
        [SerializeField] private string[] bannedWords = new string[] { "바보", "멍청이"};

        [Header("Inputs & Buttons")]
        [SerializeField] private TMP_InputField nicknameInputField;
        [SerializeField] private Button confirmButton;     
        [SerializeField] private Button cancelButton;      

        [Header("Feedback Status UI")]
        [SerializeField] private TMP_Text warningText; 
        [SerializeField] private Color warningColor = Color.red;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;      
        [SerializeField] private float animationDuration = 0.25f;

        public override void Initialize()
        {
            base.Initialize();

            if (confirmButton != null) confirmButton.onClick.AddListener(SaveAndClose);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnClickBack);

            if (nicknameInputField != null)
            {
                // 인풋필드 자체 글자 제한 걸기
                nicknameInputField.characterLimit = maxNicknameLength;
                // 유저가 타이핑을 칠 때마다 실시간으로 글자 수 경고나 비속어 경고를 초기화/검사할 수 있게 이벤트 등록
                nicknameInputField.onValueChanged.AddListener(OnNicknameInputChanged);
            }
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            if (nicknameInputField != null)
            {
                string currentName = PlayerPrefs.GetString(NicknamePrefsKey, GetDefaultSteamName());
                nicknameInputField.text = currentName;
            }

            // 열릴 때 경고 텍스트 비우기
            SetWarningMessage(string.Empty);

            if (panel != null) panel.localScale = Vector3.zero;
        }

        protected override async UniTask OnShowAnimation()
        {
            if (panel != null)
            {
                if (nicknameInputField != null) nicknameInputField.ActivateInputField();
                await panel.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack).ToUniTask();
            }
        }

        protected override async UniTask OnHideAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.zero, animationDuration).SetEase(Ease.InBack).ToUniTask();
        }

        // 실시간 타이핑 감지 함수
        private void OnNicknameInputChanged(string value)
        {
            // 글자를 고치기 시작하면 경고 텍스트를 부드럽게 지워줍니다.
            SetWarningMessage(string.Empty);
        }

        private void SaveAndClose()
        {
            if (nicknameInputField == null) return;

            string enteredName = nicknameInputField.text.Trim();

            // 1. 공백 처리 예외 공정 검사
            if (string.IsNullOrEmpty(enteredName))
            {
                SetWarningMessage("닉네임은 공백일 수 없습니다.");
                return; 
            }

            // 2. 글자 수 검증 공정
            if (enteredName.Length > maxNicknameLength)
            {
                SetWarningMessage($"닉네임은 최대 {maxNicknameLength}글자까지 가능합니다.");
                return;
            }

            // 3. [핵심 추가] 비속어 블랙리스트 포함 여부 샌드박스 검사
            if (ContainsBannedWord(enteredName))
            {
                SetWarningMessage("사용할 수 없는 비속어가 포함되어 있습니다.");
                return; // 팝업이 닫히지 않고 경고만 유지됨
            }

            // 모든 철저한 검증 보안 통과 시 저장 후 팝업 Close
            PlayerPrefs.SetString(NicknamePrefsKey, enteredName);
            PlayerPrefs.Save();

            Debug.Log($"[UI] 커스텀 닉네임 저장 완료: {enteredName}");
            OnClickBack();
        }

        // 비속어가 닉네임에 한 단어라도 묻어있는지 체크하는 보안 캡슐화 함수
        private bool ContainsBannedWord(string text)
        {
            if (bannedWords == null || bannedWords.Length == 0) return false;

            // 유저가 대소문자 섞어 쓰는 트릭 방지하기 위해 소문자로 변환 검사
            string lowerText = text.ToLower();

            foreach (string word in bannedWords)
            {
                if (string.IsNullOrWhiteSpace(word)) continue;

                if (lowerText.Contains(word.ToLower()))
                {
                    return true; // 비속어 적발!
                }
            }

            return false;
        }

        // 경고창 메시지 주입 연출 함수
        private void SetWarningMessage(string message)
        {
            if (warningText == null) return;

            warningText.text = message;
            warningText.color = warningColor;

            // 글자가 비어있지 않다면 쫄깃하게 살짝 흔들리는 연출 추가 (선택사항)
            if (!string.IsNullOrEmpty(message))
            {
                warningText.transform.DOComplete();
                warningText.transform.DOShakePosition(0.2f, 5f, 10).SetUpdate(true);
            }
        }

        public static string GetDefaultSteamName()
        {
            try
            {
                if (Steamworks.SteamAPI.IsSteamRunning())
                {
                    return Steamworks.SteamFriends.GetPersonaName();
                }
            }
            catch (System.Exception) { }

            return "PixelPlayer_" + Random.Range(1000, 9999);
        }
    }
}