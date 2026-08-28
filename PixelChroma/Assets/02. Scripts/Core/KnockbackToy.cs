using System.Collections;
using Mirror;
using UnityEngine;

namespace PixelChroma
{
    // HideTime 대기실용 장난감. 물총에 맞으면 넉백되고 벽에 튕긴다(사망 개념 없음).
    // 물리는 서버에서만 시뮬레이션되고, NetworkRigidbodyUnreliable2D가 결과를 클라에 뿌린다.
    [RequireComponent(typeof(Rigidbody2D))]
    public class KnockbackToy : NetworkBehaviour
    {
        [Header("Knockback")]
        [Tooltip("맞았을 때 밀려나는 세기. 물총의 러너용 값과 무관하게 공만 따로 조절한다")]
        [SerializeField] private float knockbackImpulse = 12f;
        [Tooltip("맞았을 때 회전 속도(도/초). 러너는 10000이라 공엔 과함")]
        [SerializeField] private float knockbackRotSpeed = 540f;
        [Tooltip("클수록 빨리 멈춘다. 러너는 2.5. 피격 시 Rigidbody2D의 Damping을 이 값으로 덮어쓴다")]
        [SerializeField] private float knockbackDrag = 1.5f;

        [Header("Rest Detection")]
        [Tooltip("이 속도 아래로 떨어지면 멈춘 것으로 보고 Idle로 되돌린다")]
        [SerializeField] private float restSpeed = 0.15f;
        [Tooltip("이 회전 속도(도/초) 아래여야 완전히 멈춘 것으로 본다")]
        [SerializeField] private float restAngularSpeed = 20f;

        [Header("Hit Reaction")]
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Animator animator;
        [SerializeField] private float flashDuration = 0.1f;
        [SerializeField] private Color flashColor = Color.white;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        // 클라의 Rigidbody2D는 kinematic이라 속도를 읽을 수 없다 → 정지 판정은 서버 전용.
        // SyncVar로 두면 늦게 접속한 클라도 훅에서 현재 상태를 그대로 맞출 수 있다.
        [SyncVar(hook = nameof(OnMovingStateChanged))]
        private bool _isMoving;

        private Rigidbody2D _rb;
        private Color _baseColor;
        private Coroutine _flashRoutine;
        private Vector3 _homePosition;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _homePosition = transform.position;
            if (visual != null) _baseColor = visual.color;

            // 피격 전(술래가 몸으로 밀 때)에도 같은 감쇠가 걸리도록 처음부터 맞춰둔다.
            _rb.linearDamping = knockbackDrag;
            _rb.angularDamping = knockbackDrag;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyMovingToAnimator(_isMoving);   // 늦참 클라 초기 상태 반영
        }

        // 물총 명중 시 서버에서 호출. 세기는 공이 스스로 정하고 방향만 받는다.
        // 적용 공식은 RunnerDeathEffects.Knockback과 동일하다.
        [Server]
        public void ServerApplyKnockback(Vector2 dir)
        {
            _rb.linearDamping = knockbackDrag;
            _rb.angularDamping = knockbackDrag;
            _rb.angularVelocity = knockbackRotSpeed * (dir.x >= 0 ? -1f : 1f);
            _rb.linearVelocity = dir * (knockbackImpulse / _rb.mass);

            _isMoving = true;
            RpcPlayHitFlash();
        }

        // 구석에 끼었을 때 되돌리는 용도. 라운드 시작 등에서 호출.
        [Server]
        public void ServerResetToHome()
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            transform.position = _homePosition;
            transform.rotation = Quaternion.identity;
            _isMoving = false;
        }

        // 서버만 실제 물리를 돌리므로 정지 판정도 서버에서 한다.
        // 총알뿐 아니라 술래가 몸으로 밀어 굴러가는 경우도 같이 잡힌다.
        [ServerCallback]
        private void FixedUpdate()
        {
            bool moving =
                _rb.linearVelocity.sqrMagnitude > restSpeed * restSpeed
                || Mathf.Abs(_rb.angularVelocity) > restAngularSpeed;

            if (moving == _isMoving) return;

            if (!moving)
            {
                // 미세하게 떠다니며 Idle 복귀가 지연되지 않도록 완전히 정지시킨다.
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }

            _isMoving = moving;
        }

        private void OnMovingStateChanged(bool _, bool moving) => ApplyMovingToAnimator(moving);

        private void ApplyMovingToAnimator(bool moving)
        {
            if (animator != null) animator.SetBool(IsMovingHash, moving);
        }

        [ClientRpc]
        private void RpcPlayHitFlash()
        {
            if (visual == null) return;
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            visual.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            visual.color = _baseColor;
            _flashRoutine = null;
        }
    }
}