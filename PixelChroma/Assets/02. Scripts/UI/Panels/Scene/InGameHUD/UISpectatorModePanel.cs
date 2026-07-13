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

        private Color _defaultTargetNameColor = Color.white;

        private void Awake()
        {
            if (targetNameText != null)
                _defaultTargetNameColor = targetNameText.color;
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

        public void SetTargetName(string name, bool isSelf)
        {
            if (targetNameText == null) return;

            targetNameText.text = name;
            targetNameText.color = isSelf ? selfTargetNameColor : _defaultTargetNameColor;
        }
    }
}