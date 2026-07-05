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

        [Header("Inputs & Buttons")]
        [SerializeField] private TMP_InputField nicknameInputField;
        [SerializeField] private Button confirmButton;     // 팝업 안의 '확인' 버튼
        [SerializeField] private Button cancelButton;      // 팝업 안의 '취소' 버튼

        [Header("Animation")]
        [SerializeField] private RectTransform panel;      // 팝업의 메인 패널 (애니메이션 타겟)
        [SerializeField] private float animationDuration = 0.25f;

        public override void Initialize()
        {
            base.Initialize();

            // 이벤트 리스너 연결
            if (confirmButton != null) confirmButton.onClick.AddListener(SaveAndClose);

            // 💡 취소 버튼은 UIPopup에 이미 구현된 OnClickBack을 그대로 재활용하여 창을 닫습니다.
            if (cancelButton != null) cancelButton.onClick.AddListener(OnClickBack);
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            // 1. 기존에 저장된 커스텀 닉네임 또는 스팀 닉네임을 인풋필드에 세팅
            if (nicknameInputField != null)
            {
                string currentName = PlayerPrefs.GetString(NicknamePrefsKey, GetDefaultSteamName());
                nicknameInputField.text = currentName;
            }

            // 2. DOTween 팝업 애니메이션을 위해 크기를 0으로 초기화
            if (panel != null) panel.localScale = Vector3.zero;
        }

        protected override async UniTask OnShowAnimation()
        {
            if (panel != null)
            {
                // 애니메이션 시작과 동시에 인풋필드 타이핑 준비 완료
                if (nicknameInputField != null) nicknameInputField.ActivateInputField();

                await panel.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack).ToUniTask();
            }
        }

        protected override async UniTask OnHideAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.zero, animationDuration).SetEase(Ease.InBack).ToUniTask();
        }

        // 💡 확인 버튼: 저장하고 닫기
        private void SaveAndClose()
        {
            if (nicknameInputField == null) return;

            string enteredName = nicknameInputField.text.Trim();

            if (string.IsNullOrEmpty(enteredName))
            {
                Debug.LogWarning("[UI] 닉네임은 공백일 수 없습니다.");
                // TODO: 필요하다면 상태 텍스트(StatusText)에 에러 메시지를 띄울 수 있습니다.
                return;
            }

            // 로컬 기기에 저장
            PlayerPrefs.SetString(NicknamePrefsKey, enteredName);
            PlayerPrefs.Save();

            Debug.Log($"[UI] 커스텀 닉네임 저장 완료: {enteredName}");

            // 저장 완료 후, UIPopup 부모 클래스의 메서드를 호출하여 부드럽게 닫습니다.
            OnClickBack();
        }

        // 기본 닉네임 (스팀 연동) 가져오기
        public static string GetDefaultSteamName()
        {
            try
            {
                if (Steamworks.SteamAPI.IsSteamRunning())
                {
                    return Steamworks.SteamFriends.GetPersonaName();
                }
            }
            catch (System.Exception)
            {
                // 에디터 환경이나 스팀 오프라인 시 예외 방어
            }

            return "PixelPlayer_" + Random.Range(1000, 9999);
        }
    }
}