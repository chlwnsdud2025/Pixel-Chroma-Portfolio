using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 💡 부모 HUD를 상속받습니다.
    public class UIInGameHUD_Runner : UIInGameHUD
    {
        [Header("Spectator Mode UI")]
        [SerializeField] private UISpectatorModePanel spectatorPanel;

        [Header("Runner Expose Skill UI Only")]
        [SerializeField] private Image exposeSkillCooldownOverlay;
        [SerializeField] private TMP_Text exposeSkillCooldownText;

        private bool _isExposeCounting;
        private float _exposeCooldownDuration;
        private float _exposeTimer;

        public override void Initialize()
        {
            base.Initialize();

            if (spectatorPanel != null) spectatorPanel.Hide();

            if (exposeSkillCooldownOverlay != null) exposeSkillCooldownOverlay.gameObject.SetActive(false);
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
                    if (exposeSkillCooldownOverlay != null) exposeSkillCooldownOverlay.gameObject.SetActive(false);
                    if (exposeSkillCooldownText != null) exposeSkillCooldownText.gameObject.SetActive(false);
                }
                else
                {
                    if (exposeSkillCooldownOverlay != null)
                        exposeSkillCooldownOverlay.fillAmount = _exposeTimer / _exposeCooldownDuration;

                    if (exposeSkillCooldownText != null)
                        exposeSkillCooldownText.text = _exposeTimer.ToString("F1");
                }
            }
        }

        public void SetSpectatorMode(bool active)
        {
            if (spectatorPanel == null) return;

            if (active) spectatorPanel.Show();
            else spectatorPanel.Hide();
        }

        public void SetActiveSpectateMode(SpectateMode mode)
        {
            if (spectatorPanel != null)
                spectatorPanel.SetActiveMode(mode);
        }

        public void SetSpectateTargetName(string name, bool isSelf)
        {
            if (spectatorPanel != null)
                spectatorPanel.SetTargetName(name, isSelf);
        }


        public void StartExposeSkillCooldown(float duration)
        {
            _exposeCooldownDuration = duration;
            _exposeTimer = duration;
            _isExposeCounting = true;

            if (exposeSkillCooldownOverlay != null)
            {
                exposeSkillCooldownOverlay.gameObject.SetActive(true);
                exposeSkillCooldownOverlay.fillAmount = 1f;
            }
            if (exposeSkillCooldownText != null)
            {
                exposeSkillCooldownText.gameObject.SetActive(true);
                exposeSkillCooldownText.text = duration.ToString("F1");
            }
        }
    }
}