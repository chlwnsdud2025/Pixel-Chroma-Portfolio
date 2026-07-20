using UnityEngine;
using UnityEngine.Localization;
using TMPro;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace PixelChroma.UI
{
    public class UINickNameSetting : UIPopup
    {
        public const string NicknamePrefsKey = NicknameService.PrefsKey;

        [Header("Nickname Limits Settings")]
        [Tooltip("닉네임 최대 글자 수 제한 설정")]
        [SerializeField] private int maxNicknameLength = 10; 

        [Header("Banned Words (비속어 필터링)")]
        [SerializeField] private BannedWordsSO bannedWords;

        [Header("Inputs & Buttons")]
        [SerializeField] private TMP_InputField nicknameInputField;
        [SerializeField] private Button saveButton;     
        [SerializeField] private Button closeButton;      

        [Header("Feedback Status UI")]
        [SerializeField] private TMP_Text warningText;

        [Header("Localization")]
        [SerializeField] private LocalizedString warningMaxLengthLocalized;
        [SerializeField] private LocalizedString warningBannedLocalized;

        [Header("Animation")]
        [SerializeField] private RectTransform panel;      
        [SerializeField] private float animationDuration = 0.25f;

        public override void Initialize()
        {
            base.Initialize();

            if (saveButton != null) saveButton.onClick.AddListener(OnClickSave);
            if (closeButton != null) closeButton.onClick.AddListener(OnClickBack);

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
                nicknameInputField.text = NicknameService.LoadOrDefault();

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

        // 경고창 메시지 주입 연출 함수
        private void SetWarningMessage(string message)
        {
            if (warningText == null) return;

            warningText.text = message;

            if (!string.IsNullOrEmpty(message))
            {
                warningText.transform.DOComplete();
                warningText.transform.DOShakePosition(0.2f, 5f, 10).SetUpdate(true);

                if (nicknameInputField != null)
                    nicknameInputField.ActivateInputField();
            }
        }

        public string GetDefaultSteamName() => NicknameService.GetDefaultSteamName();

        // 실시간 타이핑 감지 함수
        private void OnNicknameInputChanged(string value)
        {
            // 글자를 고치기 시작하면 경고 텍스트를 부드럽게 지워줍니다.
            SetWarningMessage(string.Empty);
        }

        private void OnClickSave()
        {
            if (nicknameInputField == null) return;

            string enteredName = nicknameInputField.text.Trim();

            switch (NameValidator.Validate(enteredName, bannedWords, maxNicknameLength))
            {
                case NameValidation.TooLong:
                    SetWarningMessage(warningMaxLengthLocalized.GetLocalizedString(maxNicknameLength));
                    return;
                case NameValidation.Banned:
                    SetWarningMessage(warningBannedLocalized.GetLocalizedString());
                    return;
            }

            // 비워두면 스팀 이름(스팀 미사용 시 임의 이름)을 대신 저장한다.
            if (string.IsNullOrEmpty(enteredName))
                enteredName = NicknameService.GetDefaultSteamName();

            // 모든 철저한 검증 보안 통과 시 저장 후 팝업 Close
            NicknameService.Save(enteredName);

            Debug.Log($"[UI] 커스텀 닉네임 저장 완료: {enteredName}");
            OnClickBack();
        }
    }
}