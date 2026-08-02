using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace PixelChroma.UI
{
    public class UINickNameSetting : UIPopup
    {
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
        [SerializeField] private UIwarningTextText warningText;

        [Header("Localization")]
        [SerializeField] private NicknameSettingTextDataSO textData;

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
                nicknameInputField.characterLimit = maxNicknameLength;
                nicknameInputField.onValueChanged.AddListener(OnNicknameInputChanged);
            }
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            if (nicknameInputField != null)
                nicknameInputField.text = NicknameService.LoadOrDefault();

            SetwarningTextMessage(string.Empty);

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

        private void SetwarningTextMessage(string message)
        {
            if (warningText != null) warningText.Show(message);

            if (!string.IsNullOrEmpty(message) && nicknameInputField != null)
                nicknameInputField.ActivateInputField();
        }

        private void OnNicknameInputChanged(string value)
        {
            SetwarningTextMessage(string.Empty);
        }

        private void OnClickSave()
        {
            if (nicknameInputField == null) return;

            string enteredName = nicknameInputField.text.Trim();

            // 실제 렌더에 쓰이는 폰트를 넘겨, 이 폰트로 표시 불가능한(□) 문자를 저장 전에 막는다.
            TMP_FontAsset font = nicknameInputField.textComponent != null ? nicknameInputField.textComponent.font : null;

            switch (NicknameService.Validate(enteredName, bannedWords, maxNicknameLength, font))
            {
                case NameValidation.TooLong:
                    SetwarningTextMessage(textData.warningMaxLength.GetLocalizedString(maxNicknameLength));
                    return;
                case NameValidation.Banned:
                    SetwarningTextMessage(textData.warningBanned.GetLocalizedString());
                    return;
                case NameValidation.Unsupported:
                    if (!textData.warningUnsupported.IsEmpty)
                        SetwarningTextMessage(textData.warningUnsupported.GetLocalizedString());
                    return;
            }

            // 비워두면 스팀 이름(스팀 미사용 시 임의 이름)을 대신 저장한다.
            if (string.IsNullOrEmpty(enteredName))
                enteredName = NicknameService.GetDefaultSteamName();

            NicknameService.Save(enteredName);
            OnClickBack();
        }
    }
}