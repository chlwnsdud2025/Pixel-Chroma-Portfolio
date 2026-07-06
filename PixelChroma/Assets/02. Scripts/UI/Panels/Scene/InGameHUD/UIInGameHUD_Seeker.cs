using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 💡 부모 HUD를 상속받습니다.
    public class UIInGameHUD_Seeker : UIInGameHUD
    {
        [Header("Seeker Interaction UI Only")]
        [SerializeField] private Button killButton;

        [Header("Seeker Water Gun UI Only")]
        [SerializeField] private Image waterGunCooldownOverlay;

        public override void Initialize()
        {
            base.Initialize();

            if (killButton != null)
            {
                // 술래 전용 HUD이므로 버튼을 아예 숨기지 않고 항상 켜둡니다!
                killButton.gameObject.SetActive(true);

                // 처음엔 사거리에 아무도 없으니 클릭만 안 되게(회색으로) 막아둡니다.
                killButton.interactable = false;
            }
        }

        public void UpdateWaterGunCooldown(float fillAmount)
        {
            if (waterGunCooldownOverlay != null)
            {
                waterGunCooldownOverlay.fillAmount = fillAmount;
            }
        }

        public void SetKillButtonVisible(bool isVisible)
        {
            if (killButton != null)
                killButton.gameObject.SetActive(isVisible);
        }

        public void SetKillButtonInteractable(bool isInteractable, UnityEngine.Events.UnityAction onClickAction = null)
        {
            if (killButton == null) return;

            killButton.interactable = isInteractable;
            killButton.onClick.RemoveAllListeners();

            if (isInteractable && onClickAction != null)
            {
                killButton.onClick.AddListener(onClickAction);
            }
        }
    }
}