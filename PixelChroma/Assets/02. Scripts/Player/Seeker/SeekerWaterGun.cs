using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;
using PixelChroma.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelChroma
{
    // 술래의 물총(조준 + 발사 + 쿨타임)을 담당하는 NetworkBehaviour입니다.
    // SeekerPlayer와 같은 NetworkIdentity에 붙는 형제 컴포넌트로, 자체 SyncVar/Command/Rpc를 가집니다.
    // 로컬 입력 진입은 SeekerPlayer.Update가 LocalTick()으로 게이팅해서 호출합니다.
    [RequireComponent(typeof(WaterGunEffects))]
    public class SeekerWaterGun : NetworkBehaviour
    {
        // 이 각도(도)보다 크게 변할 때만 동기화 전송. ±180° 경계에서 매 프레임 전송되는 것을 막습니다.
        private const float AimDeadzoneDeg = 5f;
        // 서버 쿨타임 검증 시 빼주는 핑 지터 여유분(초).
        private const float ServerCooldownGrace = 0.1f;

        [Header("References")]
        [SerializeField] private SpriteRenderer weaponRenderer;
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private Transform firePoint;

        [Header("Settings")]
        [SerializeField] private float fireCooldown = 1.5f;
        [SerializeField] private float fireRange = 8f;
        [SerializeField] private float aoeRadius = 2f;
        [SerializeField] private LayerMask runnerLayer;
        [SerializeField] private LayerMask obstacleLayer;

        [Tooltip("피격된 도망자가 젖음 상태로 유지되는 시간(초)")]
        [SerializeField] private float wetDuration = 5f;

        [Header("Debug")]
        [Tooltip("발사 시 실제 피격 범위(aoeRadius)를 씬 뷰에 잠시 표시합니다. (에디터 전용)")]
        [SerializeField] private bool drawAoeGizmo = true;
        [Tooltip("피격 범위 기즈모가 표시되는 시간(초)")]
        [SerializeField] private float gizmoDisplayDuration = 1f;

        // 마지막 발사의 착탄 지점과 시각. 기즈모를 발사 직후 잠깐만 그리기 위한 값입니다.
        private Vector2 _lastImpactPos;
        private float _lastImpactGizmoTime = -100f;

        [SyncVar(hook = nameof(OnAimAngleChanged))]
        private float _syncAimAngle;

        private float _lastFireTime;
        private float _lastServerFireTime = -100f;
        private Camera _mainCam;
        private WaterGunEffects _effects;

        // HUD 쿨타임 오버레이용: 발사 직후 1 → 쿨타임이 끝나면 0.
        public float CooldownFill01 =>
            fireCooldown <= 0f ? 0f : 1f - Mathf.Clamp01((Time.time - _lastFireTime) / fireCooldown);

        private bool CanFireLocally => Time.time - _lastFireTime >= fireCooldown;

        // 발사·판정 기준점(총구). 미할당 시 회전 중심 → 몸통 순으로 폴백합니다.
        private Vector2 FireOrigin =>
            firePoint != null ? (Vector2)firePoint.position :
            (weaponPivot != null ? (Vector2)weaponPivot.position : (Vector2)transform.position);

        private void Awake()
        {
            _effects = GetComponent<WaterGunEffects>();
        }

        public override void OnStartLocalPlayer()
        {
            _mainCam = Camera.main;
        }

        public void LocalTick(bool isPaintMode)
        {
            HandleAiming(isPaintMode);
            HandleShooting(isPaintMode);
        }

        public void SetWeaponVisible(bool visible)
        {
            if (weaponRenderer != null)
                weaponRenderer.enabled = visible;
        }

        private void HandleAiming(bool isPaintMode)
        {
            if (_mainCam == null || Mouse.current == null || weaponPivot == null) return;

            // 페인트 모드에서는 무기가 숨겨지므로 조준/동기화를 건너뜁니다.
            if (isPaintMode)
            {
                if (CursorManager.Instance != null)
                    CursorManager.Instance.overrideScreenPosition = null;
                return;
            }

            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = _mainCam.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0;

            Vector2 currentPos = weaponPivot.position;
            Vector2 targetWorldPos = mouseWorldPos;
            Vector2 aimDirection = (targetWorldPos - currentPos).normalized;
            float distanceToTarget = Vector2.Distance(currentPos, targetWorldPos);

            // 사거리를 넘으면 커서를 사거리 끝에 고정해 조준 떨림을 없앱니다.
            if (distanceToTarget > fireRange)
            {
                Vector2 clampedWorldPos = currentPos + aimDirection * fireRange;
                Vector2 clampedScreenPos = _mainCam.WorldToScreenPoint(clampedWorldPos);

                if (CursorManager.Instance != null)
                    CursorManager.Instance.overrideScreenPosition = clampedScreenPos;

                Mouse.current.WarpCursorPosition(clampedScreenPos);
            }
            else
            {
                if (CursorManager.Instance != null)
                    CursorManager.Instance.overrideScreenPosition = null;
            }

            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            ApplyAimRotation(angle);

            // 각도는 순환값이므로 DeltaAngle로 비교해야 ±180° 경계에서 매 프레임 전송되는 것을 막을 수 있습니다.
            if (Mathf.Abs(Mathf.DeltaAngle(_syncAimAngle, angle)) > AimDeadzoneDeg)
            {
                CmdUpdateAimAngle(angle);
            }
        }

        private void ApplyAimRotation(float angle)
        {
            if (weaponPivot == null) return;

            weaponPivot.rotation = Quaternion.Euler(0, 0, angle);

            Vector3 localScale = Vector3.one;
            if (angle > 90 || angle < -90)
                localScale.y = -1f;
            else
                localScale.y = 1f;

            weaponPivot.localScale = localScale;
        }

        [Command]
        private void CmdUpdateAimAngle(float angle)
        {
            _syncAimAngle = angle;
        }

        private void OnAimAngleChanged(float oldAngle, float newAngle)
        {
            if (isLocalPlayer) return;
            ApplyAimRotation(newAngle);
        }

        private void HandleShooting(bool isPaintMode)
        {
            if (_mainCam == null || Mouse.current == null) return;
            if (isPaintMode || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (!CanFireLocally) return;

            // 실제 마우스가 아니라 사거리 끝에 고정된 커서 위치를 향해 쏩니다.
            Vector2? overridePos = CursorManager.Instance != null ? CursorManager.Instance.overrideScreenPosition : null;
            Vector2 mouseScreenPos = overridePos ?? Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = _mainCam.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0;

            // 클라 예측·서버 판정·이펙트가 모두 총구(FireOrigin) 기준으로 일치하도록 통일합니다.
            Vector2 targetPos = ResolveImpactPoint(FireOrigin, mouseWorldPos);

            _lastFireTime = Time.time;
            CmdFireWaterGun(targetPos);
        }

        [Command]
        private void CmdFireWaterGun(Vector2 targetPos)
        {
            // 서버에서도 연사 속도를 재검증합니다. (쿨타임을 무시하는 조작된 클라이언트 방지)
            if (Time.time - _lastServerFireTime < fireCooldown - ServerCooldownGrace) return;
            _lastServerFireTime = Time.time;

            Vector2 impactPos = ResolveImpactPoint(FireOrigin, targetPos);

            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(impactPos, aoeRadius, runnerLayer);

            foreach (var col in hitColliders)
            {
                var runner = col.GetComponent<RunnerPlayer>();
                if (runner != null)
                {
                    runner.ServerTakeEraseDamage(impactPos, aoeRadius);
                    runner.ApplyWetState(wetDuration);
                }
            }

            RpcPlayFireEffect(impactPos);
        }

        // origin→target 방향으로 사거리·장애물을 반영한 착탄점을 반환합니다. (클라/서버 공용)
        private Vector2 ResolveImpactPoint(Vector2 origin, Vector2 target)
        {
            Vector2 dir = (target - origin).normalized;
            float dist = Mathf.Min(Vector2.Distance(origin, target), fireRange);

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, dist, obstacleLayer);
            return hit.collider != null ? hit.point : origin + dir * dist;
        }

        [ClientRpc]
        private void RpcPlayFireEffect(Vector2 targetPos)
        {
            // targetPos는 서버가 OverlapCircle 판정에 쓴 impactPos와 동일하므로, 기즈모 표시 기준으로 재사용합니다.
            _lastImpactPos = targetPos;
            _lastImpactGizmoTime = Time.time;

            // 스플래시가 실제 피격 범위(aoeRadius)와 정확히 일치하도록 반지름을 함께 넘깁니다.
            if (_effects != null)
                _effects.PlayFireEffect(firePoint, targetPos, aoeRadius);
        }

#if UNITY_EDITOR
        // 발사 직후 gizmoDisplayDuration 동안 실제 피격 판정 원을 씬 뷰에 그립니다.
        // 물 스플래시 이펙트 크기와 이 원이 겹치면 시각/판정이 일치한다는 뜻입니다.
        private void OnDrawGizmos()
        {
            if (!drawAoeGizmo) return;
            if (Time.time - _lastImpactGizmoTime > gizmoDisplayDuration) return;

            Color baseColor = Color.cyan;
            Handles.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.12f);
            Handles.DrawSolidDisc(_lastImpactPos, Vector3.forward, aoeRadius);
            Handles.color = baseColor;
            Handles.DrawWireDisc(_lastImpactPos, Vector3.forward, aoeRadius);
        }
#endif
    }
}
