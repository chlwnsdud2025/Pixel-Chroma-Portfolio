using UnityEngine;
using UnityEngine.UI;
using OneText.UGUI;

namespace PixelChroma.UI
{
    public class UISpectatorModePanel : MonoBehaviour
    {
        [SerializeField] private Image seekerButton;
        [SerializeField] private Image runnerButton;
        [SerializeField] private Image freeButton;
        [SerializeField] private Sprite buttonNormal;
        [SerializeField] private Sprite buttonSelected;
        [SerializeField] private OneTextLabel targetNameText;
        [SerializeField] private RawImage targetNameImage;
        [SerializeField] private Color selfTargetNameColor = Color.yellow;

        [Header("Like Guide")]
        [SerializeField] private GameObject likeGuideRoot;

        private Color _defaultTargetNameColor = Color.white;
        private Texture2D _cachedTexture;

        private void Awake()
        {
            if (targetNameText != null)
                _defaultTargetNameColor = targetNameText.color;

            if (likeGuideRoot != null)
                likeGuideRoot.SetActive(false);
        }

        private void OnDestroy()                               
        {
            if (_cachedTexture != null)
                Object.Destroy(_cachedTexture);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetActiveMode(SpectateMode mode)
        {
            if (seekerButton != null)
                seekerButton.sprite = mode == SpectateMode.Seeker ? buttonSelected : buttonNormal;
            if (runnerButton != null)
                runnerButton.sprite = mode == SpectateMode.Runner ? buttonSelected : buttonNormal;
            if (freeButton != null)
                freeButton.sprite = mode == SpectateMode.Free ? buttonSelected : buttonNormal;
        }

        public void SetTargetName(string displayName, bool isSelf, byte[] pixelPng = null)
        {
            bool showPixel = PixelNicknameHelper.ShouldShowPixel();

            if (showPixel && targetNameImage != null)
            {
                // Todo: 본인일 경우에 닉네임에 Outline등 추가? 필요하면 하기
                _cachedTexture = PixelNicknameHelper.ApplyPng(targetNameImage, pixelPng, _cachedTexture);
                targetNameImage.gameObject.SetActive(true);
                if (targetNameText != null) targetNameText.gameObject.SetActive(false);
            }
            else
            {
                if (targetNameImage != null) targetNameImage.gameObject.SetActive(false);
                if (targetNameText != null)
                {
                    targetNameText.gameObject.SetActive(true);
                    targetNameText.text = displayName;
                    targetNameText.color = isSelf ? selfTargetNameColor : _defaultTargetNameColor;
                }
            }
        }

        /// <summary>
        /// 지금 보고 있는 대상에게 좋아요를 줄 수 있는지 여부를 받아 안내를 표시합니다.
        /// </summary>
        public void SetLikeGuide(bool canLike)
        {
            if (likeGuideRoot != null)
                likeGuideRoot.SetActive(canLike);
        }
    }
}