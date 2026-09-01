using System;
using UnityEngine;
using UnityEngine.UI;
using OneText.UGUI;

namespace PixelChroma.UI
{
    public class UISpectatorModePanel : MonoBehaviour
    {
        [SerializeField] private UIFocusButton seekerButton;
        [SerializeField] private UIFocusButton runnerButton;
        [SerializeField] private UIFocusButton freeButton;
        [SerializeField] private OneTextLabel targetNameText;
        [SerializeField] private RawImage targetNameImage;
        [SerializeField] private Color selfTargetNameColor = Color.yellow;

        // 관전 모드 버튼을 눌렀을 때 발생합니다. 실제 전환은 로컬 플레이어가 처리합니다.
        public event Action<SpectateMode> OnModeSelected;

        private Color _defaultTargetNameColor = Color.white;
        private Texture2D _cachedTexture;

        private void Awake()
        {
            if (targetNameText != null)
                _defaultTargetNameColor = targetNameText.color;

            BindModeButton(seekerButton, SpectateMode.Seeker);
            BindModeButton(runnerButton, SpectateMode.Runner);
            BindModeButton(freeButton, SpectateMode.Free);
        }

        private void BindModeButton(UIFocusButton button, SpectateMode mode)
        {
            if (button == null) return;

            button.OnClicked += () => OnModeSelected?.Invoke(mode);
        }

        private void OnDestroy()                               
        {
            if (_cachedTexture != null)
                Destroy(_cachedTexture);
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
            if (seekerButton != null) seekerButton.SetSelected(mode == SpectateMode.Seeker);
            if (runnerButton != null) runnerButton.SetSelected(mode == SpectateMode.Runner);
            if (freeButton != null) freeButton.SetSelected(mode == SpectateMode.Free);
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

    }
}
