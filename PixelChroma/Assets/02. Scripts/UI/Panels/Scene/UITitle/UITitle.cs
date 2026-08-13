using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UITitle : UIScene
    {
        [Header("Logo")]
        [SerializeField] private Transform logo;
        [SerializeField] private float logoAnimationDuration = 0.5f;

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

        [Header("External Links Button")]
        [SerializeField] private Button discordButton;
        [SerializeField] private Button snsButton;
        [SerializeField] private Button workshopButton;

        [Header("External Link URLs")]
        [SerializeField] private string discordUrl = "https://discord.gg/여기에초대코드";
        [SerializeField] private string snsUrl = "https://x.com/여기에계정";
        [SerializeField] private string workshopUrl = "https://steamcommunity.com/app/5023800/workshop/";

        public override void Initialize()
        {
            base.Initialize();

            if (quickJoinButton != null) quickJoinButton.AddClickListener(OnClickQuickJoin);
            if (createRoomButton != null) createRoomButton.AddClickListener(OnClickCreateRoom);
            if (findRoomButton != null) findRoomButton.AddClickListener(OnClickFindRoom);
            if (joinRoomButton != null) joinRoomButton.AddClickListener(OnClickJoinRoom);
            if (nicknameSettingButton != null) nicknameSettingButton.AddClickListener(OnClickNicknameSetting);
            if (languageSettingButton != null) languageSettingButton.AddClickListener(OnClickLanguageButton);
            if (settingButton != null) settingButton.AddClickListener(OnClickSetting);
            if (exitGameButton != null) exitGameButton.AddClickListener(OnClickExitGame);

            if (discordButton != null) discordButton.AddClickListener(() => OpenUrl(discordUrl));
            if (snsButton != null) snsButton.AddClickListener(() => OpenUrl(snsUrl));
            if (workshopButton != null) workshopButton.AddClickListener(() => OpenUrl(workshopUrl));
        }

        public override void OnCancel() => OnClickSetting();

        protected override async UniTask OnShowAnimation()
        {
            if (logo == null) return;

            logo.localScale = Vector3.zero;
            await logo.DOScale(Vector3.one, logoAnimationDuration)
                .SetEase(Ease.OutBack)
                .ToUniTask();
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

        private void OnClickSetting()
        {
            UIManager.Instance.ShowPopup<UISetting>().Forget();
        }

        private void OnClickExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void OpenUrl(string url)
        {
            // Steam 초기화 상태면 Overlay 브라우저 사용 (게임을 나가지 않음)
            if (GameManager.SteamInitialized)
            {
                Steamworks.SteamFriends.ActivateGameOverlayToWebPage(url);
                return;
            }

            // Steam 없으면 시스템 브라우저로 fallback
            Application.OpenURL(url);
        }


    }
}