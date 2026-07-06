using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 💡 부모 HUD를 상속받습니다.
    public class UIInGameHUD_Runner : UIInGameHUD
    {
        [Header("Runner Expose Skill UI Only")]
        [SerializeField] private Image exposeSkillCooldownImage;
        [SerializeField] private TMP_Text exposeSkillCooldownText;

        private bool _isExposeCounting;
        private float _exposeCooldownDuration;
        private float _exposeTimer;

        public override void Initialize()
        {
            base.Initialize();

            // 시작할 때 기본적으로 Cooldown 레이어들은 가려둡니다.
            if (exposeSkillCooldownImage != null) exposeSkillCooldownImage.gameObject.SetActive(false);
            if (exposeSkillCooldownText != null) exposeSkillCooldownText.gameObject.SetActive(false);
        }

        // 부모의 Update(전역 타이머) 기능에 더해, 내 스킬 쿨타임 차감 연산만 덧붙입니다.
        protected override void Update()
        {
            base.Update(); // 부모의 룸 타이머 연산 실행

            if (_isExposeCounting)
            {
                _exposeTimer -= Time.deltaTime;

                if (_exposeTimer <= 0f)
                {
                    _isExposeCounting = false;
                    if (exposeSkillCooldownImage != null) exposeSkillCooldownImage.gameObject.SetActive(false);
                    if (exposeSkillCooldownText != null) exposeSkillCooldownText.gameObject.SetActive(false);
                }
                else
                {
                    if (exposeSkillCooldownImage != null)
                        exposeSkillCooldownImage.fillAmount = _exposeTimer / _exposeCooldownDuration;

                    if (exposeSkillCooldownText != null)
                        exposeSkillCooldownText.text = _exposeTimer.ToString("F1");
                }
            }
        }

        // RunnerPlayer가 스킬을 썼을 때 시동을 걸어주는 퍼블릭 함수
        public void StartExposeSkillCooldown(float duration)
        {
            _exposeCooldownDuration = duration;
            _exposeTimer = duration;
            _isExposeCounting = true;

            if (exposeSkillCooldownImage != null)
            {
                exposeSkillCooldownImage.gameObject.SetActive(true);
                exposeSkillCooldownImage.fillAmount = 1f;
            }
            if (exposeSkillCooldownText != null)
            {
                exposeSkillCooldownText.gameObject.SetActive(true);
                exposeSkillCooldownText.text = duration.ToString("F1");
            }
        }
    }
}