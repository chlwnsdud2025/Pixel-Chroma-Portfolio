using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PixelChroma.UI
{
    public class UIInGameHUD_Seeker : UIInGameHUD
    {
        [Header("Catch Icon")]
        [SerializeField] private GameObject catchInactiveOverlay;

        [Header("Water Gun")]
        [SerializeField] private Image waterGunCooldownOverlay;
        [SerializeField] private TMP_Text waterGunCooldownText;

        private float _cooldownDuration;
        private bool _wasCooling;

        protected override void ResetHUDState()
        {
            base.ResetHUDState();

            _wasCooling = false;
            _cooldownDuration = 0f;
            if (waterGunCooldownOverlay != null) waterGunCooldownOverlay.fillAmount = 0f;
            if (waterGunCooldownText != null) waterGunCooldownText.gameObject.SetActive(false);
        }

        public void SetCatchIconActive(bool isActive)
        {
            if (catchInactiveOverlay != null)
                catchInactiveOverlay.SetActive(!isActive);
        }

        public void SetWaterGunCooldownDuration(float duration)
        {
            _cooldownDuration = duration;
        }

        public void UpdateWaterGunCooldown(float fillAmount)
        {
            if (waterGunCooldownOverlay != null &&
                !Mathf.Approximately(waterGunCooldownOverlay.fillAmount, fillAmount))
                waterGunCooldownOverlay.fillAmount = fillAmount;

            if (waterGunCooldownText == null)
                return;

            bool isCooling = fillAmount > 0f;

            if (isCooling)
            {
                if (!_wasCooling)
                    waterGunCooldownText.gameObject.SetActive(true);

                float remaining = fillAmount * _cooldownDuration;
                waterGunCooldownText.text = remaining.ToString("F1");
            }
            else if (_wasCooling)
            {
                waterGunCooldownText.gameObject.SetActive(false);
            }

            _wasCooling = isCooling;
        }
    }
}
