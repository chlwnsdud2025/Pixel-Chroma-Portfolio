using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace PixelChroma
{
    public class RunnerDeathEffects : MonoBehaviour
    {
        [Header("피격 카메라 쉐이크")]
        [SerializeField] private float hitImpulseStrength = 1f;

        [Header("넉백")]
        [SerializeField] private float knockbackDuration = 0.4f;

        [Header("사망 깜빡임")]
        [SerializeField] private float deathBlinkDuration = 1.5f;
        [SerializeField] private int deathBlinkCount = 3;

        [Header("피격 점멸")]
        [SerializeField] private float flashDuration = 0.1f;

        private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");

        private Rigidbody2D _rb;
        private SpriteRenderer _visualRenderer;
        private CinemachineImpulseSource _impulseSource;
        private Animator _animator;

        private bool _isKnockedBack;
        public bool IsKnockedBack => _isKnockedBack;
        public float KnockbackDuration => knockbackDuration;

        public event Action<GameMode> OnDeathSequenceFinished;

        public void Init(Rigidbody2D rb, SpriteRenderer visualRenderer, Animator animator)
        {
            _rb = rb;
            _visualRenderer = visualRenderer;
            _animator = animator;
            _impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        public void PlayKnockbackVisuals(PlayerPaintCanvas paintCanvas)
        {
            if (paintCanvas != null)
                paintCanvas.FadeAllPaint(knockbackDuration);

            StartCoroutine(HitFlash());
        }

        public void PlayKnockbackPhysics(Vector2 dir, float impulse, float drag, float rotSpeed)
        {
            StartCoroutine(Knockback(dir, impulse, drag, rotSpeed));
        }

        public void PlayDeathSequence(GameMode mode, bool isOwned)
        {
            StartCoroutine(DeathSequenceRoutine(mode, isOwned));
        }

        private IEnumerator Knockback(Vector2 dir, float impulse, float drag, float rotSpeed)
        {
            _isKnockedBack = true;

            _rb.constraints = RigidbodyConstraints2D.None;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 넉백 중 벽 레이어 충돌 무시
            int runnerLayer = gameObject.layer;
            int wallLayer = LayerMask.NameToLayer("Wall");
            Physics2D.IgnoreLayerCollision(runnerLayer, wallLayer, true);

            // 카메라 쉐이크는 설정에서 끌 수 있다(모션 감수성 접근성). 넉백 물리 자체는 그대로 유지.
            if (_impulseSource != null && GameSettings.ScreenShakeEnabled)
                _impulseSource.GenerateImpulse(new Vector3(dir.x, dir.y, 0f) * hitImpulseStrength);

            _rb.linearVelocity = Vector2.zero;
            _rb.linearDamping = drag;
            _rb.angularDamping = drag;
            _rb.angularVelocity = rotSpeed * (dir.x >= 0 ? -1f : 1f);
            _rb.AddForce(dir * impulse, ForceMode2D.Impulse);

            yield return new WaitForSeconds(knockbackDuration);

            // 충돌 복원
            Physics2D.IgnoreLayerCollision(runnerLayer, wallLayer, false);

            _isKnockedBack = false;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        private IEnumerator HitFlash()
        {
            if (_visualRenderer == null) yield break;

            _visualRenderer.material.SetFloat(FlashAmountID, 1f);
            yield return new WaitForSeconds(flashDuration);
            _visualRenderer.material.SetFloat(FlashAmountID, 0f);
        }

        private IEnumerator DeathSequenceRoutine(GameMode mode, bool isOwned)
        {
            if (isOwned && _rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }

            //if (_animator != null) _animator.SetTrigger("Death");

            if (_visualRenderer != null)
            {
                float blinkInterval = deathBlinkDuration / (deathBlinkCount * 2);
                for (int i = 0; i < deathBlinkCount; i++)
                {
                    _visualRenderer.color = new Color(1f, 1f, 1f, 0.2f);
                    yield return new WaitForSeconds(blinkInterval);
                    _visualRenderer.color = new Color(1f, 1f, 1f, 1f);
                    yield return new WaitForSeconds(blinkInterval);
                }
                _visualRenderer.color = Color.white;
            }

            if (isOwned)
                OnDeathSequenceFinished?.Invoke(mode);
        }
    }
}