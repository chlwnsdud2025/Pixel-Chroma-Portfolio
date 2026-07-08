using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIInGameHUD_Seeker : UIInGameHUD
    {
        [Header("Catch Icon")]
        [SerializeField] private Image catchIcon;
        [SerializeField] private Color catchActiveColor = Color.white;
        [SerializeField] private Color catchInactiveColor = new Color(0.7843f, 0.7843f, 0.7843f, 0.5f);

        [Header("Water Gun")]
        [SerializeField] private Image waterGunCooldownOverlay;

        public void SetCatchIconActive(bool isActive)
        {
            if (catchIcon == null) 
                return;

            Color target = isActive ? catchActiveColor : catchInactiveColor;
            if (catchIcon.color != target)
                catchIcon.color = target;
        }

        public void UpdateWaterGunCooldown(float fillAmount)
        {
            if (waterGunCooldownOverlay == null)
                return;

            if (!Mathf.Approximately(waterGunCooldownOverlay.fillAmount, fillAmount))
                waterGunCooldownOverlay.fillAmount = fillAmount;
        }
    }
}
