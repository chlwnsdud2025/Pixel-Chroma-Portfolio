using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PixelChroma.UI
{
    public class UISpectatorModePanel : MonoBehaviour
    {
        [SerializeField] private Image seekerButton;
        [SerializeField] private Image runnerButton;
        [SerializeField] private Image freeButton;
        [SerializeField] private Sprite buttonNormal;
        [SerializeField] private Sprite buttonSelected;
        [SerializeField] private TMP_Text targetNameText;
        [SerializeField] private Color selfTargetNameColor = Color.yellow;

        [Header("Like Guide")]
        [SerializeField] private GameObject likeGuideRoot;

        private Color _defaultTargetNameColor = Color.white;

        private void Awake()
        {
            if (targetNameText != null)
                _defaultTargetNameColor = targetNameText.color;

            if (likeGuideRoot != null)
                likeGuideRoot.SetActive(false);
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

        public void SetTargetName(string displayName, bool isSelf)
        {
            if (targetNameText == null) return;

            targetNameText.text = displayName;
            targetNameText.color = isSelf ? selfTargetNameColor : _defaultTargetNameColor;
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