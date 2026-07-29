using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UITitle : UIScene
    {
        [Header("Main Menu Buttons")]
        [SerializeField] private Button quickJoinButton;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button findRoomButton;
        [SerializeField] private Button joinRoomButton;

        [Header("Setting Buttons")]
        [SerializeField] private Button nicknameSettingButton;
        [SerializeField] private Button languageSettingButton;
        [SerializeField] private Button settingButton;

        [Header("System Buttons")]
        [SerializeField] private Button exitGameButton;

        public override void Initialize()
        {
            base.Initialize();

            if (quickJoinButton != null) quickJoinButton.onClick.AddListener(OnClickQuickJoin);
            if (createRoomButton != null) createRoomButton.onClick.AddListener(OnClickCreateRoom);
            if (findRoomButton != null) findRoomButton.onClick.AddListener(OnClickFindRoom);
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnClickJoinRoom);
            if (nicknameSettingButton != null) nicknameSettingButton.onClick.AddListener(OnClickNicknameSetting);
            if (languageSettingButton != null) languageSettingButton.onClick.AddListener(OnClickLanguageButton);
            if (settingButton != null) settingButton.onClick.AddListener(OnClickSetting);
            if (exitGameButton != null) exitGameButton.onClick.AddListener(OnClickExitGame);
        }

        public override void OnCancel() => OnClickSetting();

        private void OnClickQuickJoin()
        {
            UIManager.Instance.ShowPopup<UIQuickMatch>().Forget();
        }

        private void OnClickCreateRoom()
        {
            UIManager.Instance.ShowPopup<UIRoomSetting>().Forget();
        }

        private void OnClickFindRoom()
        {
            UIManager.Instance.ShowPopup<UIRoomList>().Forget();
        }

        private void OnClickJoinRoom()
        {
            UIManager.Instance.ShowPopup<UIRoomCodeEntry>().Forget();
        }
        private void OnClickNicknameSetting()
        {
            UIManager.Instance.ShowPopup<UINickNameSetting>().Forget();
        }

        public void OnClickLanguageButton()
        {
            UIManager.Instance.ShowPopup<UILanguageSelect>().Forget();
        }

        private void OnClickSetting()
        {
            // TODO: 설정 팝업(UISetting) 구현 후 아래 호출로 교체
            // UIManager.Instance.ShowPopup<UISetting>().Forget();
            Debug.Log("[UITitle] 설정 팝업이 아직 구현되지 않았습니다.");
        }

        private void OnClickExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}