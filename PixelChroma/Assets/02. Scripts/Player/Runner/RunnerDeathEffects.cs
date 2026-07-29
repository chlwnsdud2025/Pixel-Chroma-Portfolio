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

        [Header("히트스톱")]
        [SerializeField] private float hitstopDuration = 0.05f;
        [SerializeField, Range(0f, 1f)] private float hitstopSlowRatio = 0.1f;

        [Header("넉백")]
        [SerializeField] private float knockbackDuration = 0.4f;
        [SerializeField] private PhysicsMaterial2D bouncyMaterial;

        [Header("사망 깜빡임")]
        [SerializeField] private float deathBlinkDuration = 1.5f;
        [SerializeField] private int deathBlinkCount = 3;

        [Header("피격 점멸")]
        [SerializeField] private int hitstopFlashCount = 1;

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

            StartCoroutine(HitstopFlash());
        }

        public void PlayKnockbackPhysics(Vector2 dir, float impulse, float drag, float rotSpeed)
        {
            StartCoroutine(HitstopThenKnockback(dir, impulse, drag, rotSpeed));
        }

        public void PlayDeathSequence(GameMode mode, bool isOwned)
        {
            StartCoroutine(DeathSequenceRoutine(mode, isOwned));
        }

        private IEnumerator HitstopThenKnockback(Vector2 dir, float impulse, float drag, float rotSpeed)
        {
            _isKnockedBack = true;

            _rb.linearVelocity = Vector2.zero;
            _rb.linearDamping = 50f;
            _rb.sharedMaterial = bouncyMaterial;
            _rb.constraints = RigidbodyConstraints2D.None;
            _rb.AddForce(dir * impulse * hitstopSlowRatio, ForceMode2D.Impulse);

            if (_impulseSource != null)
                _impulseSource.GenerateImpulse(new Vector3(dir.x, dir.y, 0f) * hitImpulseStrength);

            yield return new WaitForSeconds(hitstopDuration);

            _rb.linearVelocity = Vector2.zero;
            _rb.linearDamping = drag;
            _rb.angularDamping = drag;
            _rb.angularVelocity = rotSpeed * (dir.x >= 0 ? -1f : 1f);
            _rb.AddForce(dir * impulse, ForceMode2D.Impulse);

            yield return new WaitForSeconds(knockbackDuration);

            _isKnockedBack = false;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        private IEnumerator HitstopFlash()
        {
            if (_visualRenderer == null) yield break;

            float blinkInterval = hitstopDuration;

            for (int i = 0; i < hitstopFlashCount; i++)
            {
                _visualRenderer.material.SetFloat(FlashAmountID, 1f);
                yield return new WaitForSeconds(blinkInterval);
                _visualRenderer.material.SetFloat(FlashAmountID, 0f);
                yield return new WaitForSeconds(blinkInterval);
            }

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