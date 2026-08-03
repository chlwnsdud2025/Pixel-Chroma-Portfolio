using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;
using PixelChroma.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using Unity.Cinemachine;
using static UnityEngine.LowLevelPhysics2D.PhysicsLayers;

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


        [Header("Recoil (발사 반동)")]
        [Tooltip("발사 시 총이 총구 반대방향으로 밀리는 거리(월드 유닛). 0이면 반동 없음")]
        [SerializeField] private float recoilDistance = 0.15f;
        [Tooltip("발사 시 카메라 쉐이크 강도")]
        [SerializeField] private float impulseStrength = 1f;

        [Tooltip("발사 시 총구가 들리는 각도(도). 0이면 회전 킥 없음")]
        [SerializeField] private float recoilKickAngle = 8f;

        [Tooltip("밀린 총이 원위치로 복귀하는 시간(초). 짧을수록 탄력적")]
        [SerializeField] private float recoilReturnTime = 0.12f;

        [Header("Knockback Settings")]
        [SerializeField] private float knockbackImpulse = 15f;
        [SerializeField] private float knockbackDrag = 5f;
        [SerializeField] private float knockbackRotSpeed = 720f;

        [Tooltip("넉백 방향에서 총알 진행방향이 차지하는 비중 (0 = 착탄점 방사형만, 1 = 총알 진행방향만)")]
        [Range(0f, 1f)]
        [SerializeField] private float knockbackForwardBias = 0.5f;

        private SeekerPlayer _seeker;

        // 마지막 발사의 착탄 지점과 시각. 기즈모를 발사 직후 잠깐만 그리기 위한 값입니다.
        private Vector2 _lastImpactPos;
        private float _lastImpactGizmoTime = -100f;

        [SyncVar(hook = nameof(OnAimAngleChanged))]
        private float _syncAimAngle;

        private float _lastFireTime;
        private float _lastServerFireTime = -100f;
        private Vector2 _aimDirection = Vector2.right;
        private Camera _mainCam;
        private WaterGunEffects _effects;

        // 반동 연출용: 총 스프라이트의 원위치와 진행 중인 반동 코루틴
        private Vector3 _weaponRestLocalPos;
        private Coroutine _recoilRoutine;
        private Quaternion _weaponRestLocalRot;
        private CinemachineImpulseSource _impulseSource;

        public float FireCooldownDuration => fireCooldown;

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
            _impulseSource = GetComponent<CinemachineImpulseSource>();
            _seeker = GetComponent<SeekerPlayer>();
            if (weaponRenderer != null)
            {
                _weaponRestLocalPos = weaponRenderer.transform.localPosition;
                _weaponRestLocalRot = weaponRenderer.transform.localRotation;
                
            }
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

            if (isPaintMode)
            {
                if (CursorManager.Instance != null)
                    CursorManager.Instance.overrideScreenPosition = null;
                return;
            }

            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = _mainCam.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0;

            // 방향은 회전 중심(pivot) 기준 — firePoint를 쓰면 회전 피드백 루프 발생
            Vector2 pivotPos = weaponPivot.position;
            Vector2 aimDirection = ((Vector2)mouseWorldPos - pivotPos).normalized;
            _aimDirection = aimDirection;

            // firePoint는 회전해도 pivot으로부터 항상 이 거리만큼 떨어져 있음 (로컬 좌표라 회전/반전에 영향 안 받음)
            float muzzleOffset = firePoint != null ? firePoint.localPosition.magnitude : 0f;
            float effectiveRange = fireRange + muzzleOffset;   // pivot 기준으로 봤을 때의 실제 최대 사거리

            float distanceToTarget = Vector2.Distance(pivotPos, mouseWorldPos);

            if (distanceToTarget > effectiveRange)
            {
                Vector2 clampedWorldPos = pivotPos + aimDirection * effectiveRange;
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

        private void PlayRecoil()
        {
            if (weaponRenderer == null) return;
            if (recoilDistance <= 0f && recoilKickAngle <= 0f) return;

            if (_recoilRoutine != null) StopCoroutine(_recoilRoutine);
            _recoilRoutine = StartCoroutine(RecoilRoutine());
        }

        private IEnumerator RecoilRoutine()
        {
            Transform t = weaponRenderer.transform;

            Vector3 kickedPos = _weaponRestLocalPos - Vector3.right * recoilDistance;

            // 회전: 총구가 위로 들리도록 로컬 Z를 +방향으로 킥.
            // 반동은 weaponRenderer 로컬 공간에 적용되고, 뒤집힘(S(1,-1))+조준회전(~180°)이
            // 합쳐지면 Y축 반사가 되어 상하 방향이 보존됨 → 좌우 어느 쪽이든 +kick이 위로 향함.
            Quaternion kickedRot =
                _weaponRestLocalRot * Quaternion.Euler(0f, 0f, recoilKickAngle);

            // 즉발 킥
            t.localPosition = kickedPos;
            t.localRotation = kickedRot;

            float elapsed = 0f;
            while (elapsed < recoilReturnTime)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / recoilReturnTime);

                // ease-out: 처음 빠르게 돌아오다 끝에서 부드럽게 안착
                float eased = 1f - (1f - k) * (1f - k);

                t.localPosition = Vector3.Lerp(kickedPos, _weaponRestLocalPos, eased);
                t.localRotation = Quaternion.Slerp(kickedRot, _weaponRestLocalRot, eased);

                yield return null;
            }

            // 오차 없이 정확히 복귀
            t.localPosition = _weaponRestLocalPos;
            t.localRotation = _weaponRestLocalRot;
            _recoilRoutine = null;
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

            _lastFireTime = Time.time;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector2 mouseWorld = _mainCam.ScreenToWorldPoint(mouseScreen);   // 조준 시 이미 사거리로 클램프된 위치
            CmdFireWaterGun(mouseWorld);   // 방향이 아니라 '목표점'을 전달

            PlayRecoil();

            if (_impulseSource != null)
            {
                Vector2 recoilDir = -_aimDirection.normalized * impulseStrength;
                _impulseSource.GenerateImpulse(new Vector3(recoilDir.x, recoilDir.y, 0f));
            }
        }

        [Command]
        private void CmdFireWaterGun(Vector2 targetPoint)
        {
            if (Time.time - _lastServerFireTime < fireCooldown - ServerCooldownGrace) return;
            _lastServerFireTime = Time.time;

            Vector2 origin = FireOrigin;                    // 서버 기준 총구 위치
            Vector2 direction = targetPoint - origin;       // 총구 → 마우스 지점
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            direction.Normalize();

            RaycastHit2D hit = Physics2D.Raycast(origin, direction, fireRange, obstacleLayer | runnerLayer);
            bool didHit = hit.collider != null;

            Vector2 laserEndPos = didHit ? hit.point : origin + direction * fireRange;
            Vector2 hitNormal = didHit ? hit.normal : -direction;   // 안 맞았을 땐 의미 없으니 대충 반대 방향으로 폴백

            if (didHit)
            {
                Collider2D[] hitColliders = Physics2D.OverlapCircleAll(laserEndPos, aoeRadius, runnerLayer);
                foreach (var col in hitColliders)
                {
                    var runner = col.GetComponentInParent<RunnerPlayer>();
                    if (runner != null)
                    {
                        // 1, 착탄점 → 러너 (총알과 반대방향으로 밀어냄)
                        Vector2 radial = (Vector2)runner.transform.position - laserEndPos;
                        Vector2 radialDir = radial.sqrMagnitude > 0.0001f ? radial.normalized : direction;

                        // 2, 1 + 총알 진행방향 혼합
                        Vector2 knockbackDir = Vector2.Lerp(radialDir, direction, knockbackForwardBias);
                        knockbackDir = knockbackDir.sqrMagnitude > 0.0001f ? knockbackDir.normalized : direction;

                        runner.ServerNotifyCaught(_seeker.playerName, knockbackDir, knockbackImpulse, knockbackDrag, knockbackRotSpeed);
                    }
                }
            }

            RpcPlayFireEffect(laserEndPos, didHit, direction, hitNormal);   // direction, hitNormal 추가
        }

        [ClientRpc]
        private void RpcPlayFireEffect(Vector2 targetPos, bool didHit, Vector2 direction, Vector2 hitNormal)
        {
            if (didHit)
            {
                _lastImpactPos = targetPos;
                _lastImpactGizmoTime = Time.time;
            }

            if (!isLocalPlayer) PlayRecoil();

            if (_effects != null)
                _effects.PlayFireEffect(firePoint, targetPos, didHit, direction, hitNormal);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            
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
