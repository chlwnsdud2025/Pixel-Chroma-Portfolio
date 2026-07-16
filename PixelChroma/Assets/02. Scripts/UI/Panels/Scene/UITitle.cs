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

        public override void Initialize()
        {
            base.Initialize();

            if (quickJoinButton != null) quickJoinButton.onClick.AddListener(OnClickQuickJoin);
            if (createRoomButton != null) createRoomButton.onClick.AddListener(OnClickCreateRoom);
            if (findRoomButton != null) findRoomButton.onClick.AddListener(OnClickFindRoom);
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnClickJoinRoom);
            if (nicknameSettingButton != null) nicknameSettingButton.onClick.AddListener(OnClickNicknameSetting);
            if (languageSettingButton != null) languageSettingButton.onClick.AddListener(OnClickLanguageButton);
        }

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
    }
}