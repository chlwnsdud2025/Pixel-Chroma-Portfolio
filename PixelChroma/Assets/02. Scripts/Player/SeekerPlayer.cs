using UnityEngine;
using Mirror;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;
using UnityEngine.InputSystem;
using System.Collections;

namespace PixelChroma
{
    public class SeekerPlayer : PlayerBase
    {
        [Header("Seeker Settings")]
        public float seekerMoveSpeed = 5.5f;

        [Header("Seeker Basic Settings")]
        public float catchRadius = 1.5f;

        [Header("Water Gun Settings")]
        [SerializeField] private Transform weaponPivot;
        // 실제 물총의 끝부분(총구)을 지정할 변수
        [SerializeField] private Transform firePoint;
        // 연구원님 말씀대로 코드가 아닌 프리팹으로 관리할 레이저
        [SerializeField] private GameObject laserPrefab;
        [SerializeField] private float fireCooldown = 1.5f;
        [SerializeField] private float fireRange = 5f;
        [SerializeField] private float aoeRadius = 2f;
        [SerializeField] private LayerMask runnerLayer;
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private GameObject waterSplashPrefab;

       


        [Header("Indicator Settings")]
        [Tooltip("도망자를 가리킬 화살표 프리팹")]
        public GameObject indicatorPrefab;
        [Tooltip("술래 몸 중심으로부터 화살표가 띄워질 거리(반지름)")]
        public float indicatorRadius = 1.5f;


        [SyncVar(hook = nameof(OnAimAngleChanged))]
        private float _syncAimAngle;

        private float _lastFireTime;
        private Camera _mainCam;
        private static Sprite _circleSprite;

        private SpriteRenderer[] _weaponRenderers;

        private RunnerPlayer _targetRunnerToKill = null;
        private UIInGameHUD_Seeker _seekerHUD;
        [SerializeField] private float wettimer = 5f;


        protected override void OnLocalPlayerStarted()
        {
            moveSpeed = seekerMoveSpeed;
            _mainCam = Camera.main;
            if (weaponPivot != null)
            {
                _weaponRenderers = weaponPivot.GetComponentsInChildren<SpriteRenderer>(true);
            }

            PaintController.OnPaintModeChanged += HandleLocalPaintModeChanged;
            ShowHUDAsync().Forget();
        }

        protected override void OnLocalPlayerStopped()
        {
            PaintController.UnbindHUD();
            PaintController.OnPaintModeChanged -= HandleLocalPaintModeChanged;

            if (CursorManager.Instance != null)
                CursorManager.Instance.overrideScreenPosition = null;
        }

        private void HandleLocalPaintModeChanged(bool isPaint)
        {
            if (_weaponRenderers == null) return;

            foreach (var sr in _weaponRenderers)
            {
                if (sr != null)
                {
                    sr.enabled = !isPaint;
                }
            }
        }

        protected async UniTaskVoid ShowHUDAsync() // (기존 코드 명칭에 맞춤)
        {
            // 어드레서블 명부에 등록된 "술래 전용 HUD 프리팹"을 스폰합니다.
            _seekerHUD = await UIManager.Instance.ShowScene<UIInGameHUD_Seeker>();
            PaintController.BindHUD(_seekerHUD);
        }

        protected override void Update()
        {
            base.Update();

            if (!isLocalPlayer) return;

            CheckForKillableRunner(); // 테스트
            HandleKillInput();
            HandleAiming();
            HandleShooting();
            if (_seekerHUD != null)
            {
                float elapsed = Time.time - _lastFireTime;
                float ratio = Mathf.Clamp01(elapsed / fireCooldown);
                _seekerHUD.UpdateWaterGunCooldown(1f - ratio);
            }

        }
        //---------------------------------물에젖음 테스트 --------------------------
        private void CheckForKillableRunner()
        {
            _targetRunnerToKill = null;
            float closestDist = catchRadius;

            if (InGameManager.Instance == null) return;
            foreach (var runner in InGameManager.Instance.Runners)
            {
                // 젖어 있고, 아직 죽지 않은(관전 중이 아닌) 도망자만 필터링
                if (runner != null && runner.isWet && !runner.IsSpectating)
                {
                    float dist = Vector2.Distance(transform.position, runner.transform.position);
                    if (dist <= closestDist)
                    {
                        closestDist = dist;
                        _targetRunnerToKill = runner;
                    }
                }
            }

            // HUD의 킬 버튼 켜기/끄기
            if (_seekerHUD != null)
            {
                if (_targetRunnerToKill != null)
                {
                    _seekerHUD.SetKillButtonInteractable(true, ExecuteKill);
                }
                else
                {
                    _seekerHUD.SetKillButtonInteractable(false);
                }
            }
        }

        private void HandleKillInput()
        {
            // 단축키 (F키) 연동
            if (Keyboard.current.eKey.wasPressedThisFrame && _targetRunnerToKill != null)
            {
                ExecuteKill();
            }
        }

        private void ExecuteKill()
        {
            if (_targetRunnerToKill != null)
            {
                CmdExecuteKill(_targetRunnerToKill);
                _targetRunnerToKill = null; // 킬 시도 후 타겟 초기화
            }
        }

        // 서버에서 킬 확정 처리
        [Command]
        public void CmdExecuteKill(RunnerPlayer target)
        {
            if (target == null || !target.isWet) return;

            float dist = Vector2.Distance(transform.position, target.transform.position);
            if (dist <= catchRadius + 0.5f) // 약간의 핑 보정
            {
                target.CmdNotifyCaught(); // 도망자 사망 처리 호출
            }
        }
        // ------------------------------------------------------------------------

        // ── 1. 마우스 조준 및 흔들림 방지(Override) 로직 ──────────────────────
        private void HandleAiming()
        {
            if (_mainCam == null || Mouse.current == null || weaponPivot == null) return;

            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = _mainCam.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0;

            Vector2 currentPos = weaponPivot.position;
            Vector2 targetWorldPos = mouseWorldPos;
            Vector2 aimDirection = (targetWorldPos - currentPos).normalized;

            if (!PaintController.IsPaintMode)
            {
                float distanceToTarget = Vector2.Distance(currentPos, targetWorldPos);

                // [1. 완벽한 시각적 고정] 거리가 사거리를 넘어서면 UI 커서를 1픽셀의 떨림도 없이 벽에 박제합니다.
                if (distanceToTarget > fireRange)
                {
                    Vector2 clampedWorldPos = currentPos + aimDirection * fireRange;
                    Vector2 clampedScreenPos = _mainCam.WorldToScreenPoint(clampedWorldPos);

                    if (CursorManager.Instance != null)
                        CursorManager.Instance.overrideScreenPosition = clampedScreenPos;
                }
                else
                {
                    // 사거리 안쪽일 땐 자연스럽게 따라가도록 해제
                    if (CursorManager.Instance != null)
                        CursorManager.Instance.overrideScreenPosition = null;
                }

                // [2. 부드러운 하드웨어 가두기] 마우스가 밖으로 너무 멀리 나가서 데드존이 생기는 것만 막아줍니다.
                // 사거리(fireRange)보다 1.5배 넓은 여유 공간을 주어 OS 마우스와 충돌(Jitter)하는 현상을 원천 차단합니다.
                float limitRadius = fireRange * 1.1f;
                if (distanceToTarget > limitRadius)
                {
                    Vector2 limitWorldPos = currentPos + aimDirection * limitRadius;
                    Vector2 limitScreenPos = _mainCam.WorldToScreenPoint(limitWorldPos);
                    Mouse.current.WarpCursorPosition(limitScreenPos);
                }
            }
            else
            {
                if (CursorManager.Instance != null)
                    CursorManager.Instance.overrideScreenPosition = null;
            }

            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            ApplyAimRotation(angle);

            if (Mathf.Abs(_syncAimAngle - angle) > 5f)
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

        // ── 2. 물총 발사 클라이언트 로직 ──────────────────────────────────────
        private void HandleShooting()
        {
            if (Mouse.current == null) return;

            if (!PaintController.IsPaintMode && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (Time.time - _lastFireTime >= fireCooldown)
                {
                    // [핵심] 실제 마우스 위치가 아니라, "우리가 얌전하게 박제해둔 UI 커서 위치"를 향해 발사합니다!
                    Vector2 mouseScreenPos = CursorManager.Instance.overrideScreenPosition ?? Mouse.current.position.ReadValue();
                    Vector3 mouseWorldPos = _mainCam.ScreenToWorldPoint(mouseScreenPos);
                    mouseWorldPos.z = 0;

                    Vector2 currentPos = weaponPivot != null ? weaponPivot.position : transform.position;
                    Vector2 targetPos = mouseWorldPos;
                    Vector2 dir = (targetPos - currentPos).normalized;

                    float dist = Mathf.Min(Vector2.Distance(currentPos, targetPos), fireRange);

                    RaycastHit2D hit = Physics2D.Raycast(currentPos, dir, dist, obstacleLayer);
                    if (hit.collider != null)
                    {
                        targetPos = hit.point;
                    }
                    else
                    {
                        targetPos = currentPos + dir * dist;
                    }

                    _lastFireTime = Time.time;
                    CmdFireWaterGun(targetPos);
                }
            }
        }

        // ── 3. 물총 폭발 서버 로직 ──────────────────────────────────────────
        [Command]
        private void CmdFireWaterGun(Vector2 targetPos)
        {
            Vector2 currentPos = firePoint != null ? firePoint.position :
                                 (weaponPivot != null ? weaponPivot.position : (Vector2)transform.position);
            Vector2 dir = (targetPos - currentPos).normalized;
            float dist = Vector2.Distance(currentPos, targetPos);

            dist = Mathf.Min(dist, fireRange);

            RaycastHit2D hit = Physics2D.Raycast(currentPos, dir, dist, obstacleLayer);
            if (hit.collider != null)
            {
                targetPos = hit.point;
            }

            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(targetPos, aoeRadius, runnerLayer);

            foreach (var col in hitColliders)
            {
                var runner = col.GetComponent<RunnerPlayer>();
                if (runner != null)
                {
                    runner.ServerTakeEraseDamage(targetPos, aoeRadius);

                    runner.ApplyWetState(wettimer);
                }
            }

            RpcPlayFireEffect(targetPos);
        }

        [ClientRpc]
        private void RpcPlayFireEffect(Vector2 targetPos) // 💡 인자 변경
        {
            StartCoroutine(AnimateLaserAndSplash(targetPos));
        }

        // 💡 [핵심 수정] 코루틴 내부에서 시작점을 '실시간'으로 추적합니다!
        private System.Collections.IEnumerator AnimateLaserAndSplash(Vector2 targetPos)
        {
            float laserDuration = 0.1f;

            GameObject laserObj = null;
            LineRenderer lr = null;

            if (laserPrefab != null)
            {
                // 생성 위치는 상관없습니다. 어차피 LineRenderer가 World Space로 덮어씌웁니다.
                laserObj = Instantiate(laserPrefab, transform.position, Quaternion.identity);
                lr = laserObj.GetComponent<LineRenderer>();
            }

            float elapsed = 0f;

            while (elapsed < laserDuration)
            {
                elapsed += Time.deltaTime;
                float ratio = elapsed / laserDuration;

                // 캐릭터가 아무리 빠르게 이동 중이어도 레이저 꼬리가 총구에 찰싹 달라붙습니다!
                Vector2 dynamicStartPos = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;

                // 레이저의 꼬리(Tail)와 머리(Head)를 실시간 총구 위치 기준으로 부드럽게 뻗어나가게 합니다.
                Vector2 currentTail = Vector2.Lerp(dynamicStartPos, targetPos, Mathf.Max(0, ratio - 0.5f));
                Vector2 currentHead = Vector2.Lerp(dynamicStartPos, targetPos, ratio);

                if (lr != null)
                {
                    lr.SetPosition(0, currentTail);
                    lr.SetPosition(1, currentHead);
                }

                yield return null;
            }

            if (laserObj != null) Destroy(laserObj);

            ShowSplashEffect(targetPos, aoeRadius);

            if (waterSplashPrefab != null)
            {
                Instantiate(waterSplashPrefab, targetPos, Quaternion.identity);
            }
        }

        // ── 임시 시각 효과 로직 ────────────────────────────────────────────
        private void EnsureCircleSprite()
        {
            if (_circleSprite != null) return;

            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            Color fill = Color.white;
            float r = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r + 0.5f;
                    float dy = y - r + 0.5f;
                    if (dx * dx + dy * dy <= r * r)
                        tex.SetPixel(x, y, fill);
                    else
                        tex.SetPixel(x, y, clear);
                }
            }
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void ShowSplashEffect(Vector2 pos, float radius)
        {
            EnsureCircleSprite();

            GameObject splash = new GameObject("WaterSplashEffect");
            splash.transform.position = pos;
            splash.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

            var sr = splash.AddComponent<SpriteRenderer>();
            sr.sprite = _circleSprite;
            sr.color = new Color(0.1f, 0.6f, 1f, 0.7f);
            sr.sortingOrder = 100;

            StartCoroutine(FadeOutAndDestroy(splash, sr, 0.6f));
        }

        private IEnumerator FadeOutAndDestroy(GameObject target, SpriteRenderer sr, float duration)
        {
            float t = 0f;
            Color startColor = sr.color;

            while (t < duration)
            {
                t += Time.deltaTime;
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t / duration));
                yield return null;
            }

            Destroy(target);
        }

        public override CursorType GetDefaultCursorType()
        {
            return CursorType.Crosshair;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (InGameManager.Instance != null)
                InGameManager.Instance.RegisterPlayer(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (InGameManager.Instance != null)
                InGameManager.Instance.UnregisterPlayer(this);
        }

        private void OnDrawGizmosSelected()
        {
            // 1. 기존 킬 사거리 (빨간색 원)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, catchRadius);

            // 2. 위치 노출 화살표가 맴도는 궤도 범위 (노란색 원)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, indicatorRadius);
        }

        [ClientRpc]
        public void RpcTeleport(Vector3 newPosition)
        {
            // 1. 위치 이동
            transform.position = newPosition;

            // 2. 이동하던 관성(Velocity) 초기화 
            if (TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.linearVelocity = Vector2.zero;
            }

            // 만약 카메라를 시네머신으로 쓴다면, 텔레포트 시 카메라가 쭈욱 따라오지 않고 즉시 순간이동 하도록 강제 업데이트
            if (cinemachineCamera != null)
            {
                cinemachineCamera.PreviousStateIsValid = false;
            }

            Debug.Log("[Seeker] 본진으로 텔레포트 되었습니다!");
        }


        [ClientRpc]
        public void RpcShowRunnerIndicator(NetworkIdentity runnerIdentity, float duration)
        {
            // 오직 이 술래를 조종하는 당사자의 화면에서만 화살표가 보이게 처리합니다.
            // (다른 도망자들이나 관전자의 화면에는 화살표가 안 보입니다)
            if (!isLocalPlayer) return;

            if (runnerIdentity == null || indicatorPrefab == null) return;

            // 화살표 생성 및 추적 코루틴 시작
            StartCoroutine(TrackRunnerCoroutine(runnerIdentity.transform, duration));
        }

        private System.Collections.IEnumerator TrackRunnerCoroutine(Transform target, float duration)
        {
            // 화살표 생성 (술래의 자식으로 묶지 않고 독립적으로 움직이게 합니다)
            GameObject arrow = Instantiate(indicatorPrefab, transform.position, Quaternion.identity);

            float elapsed = 0f;

            // duration 시간이 다 되거나, 타겟(도망자)이 게임에서 나갔을 때 루프 종료
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;

                // 1. 방향 벡터 구하기 (도망자 위치 - 술래 위치)
                Vector2 dir = (target.position - transform.position).normalized;

                // 2. 각도 계산 (Atan2를 사용해 방향을 회전값으로 변환)
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                // 3. 화살표 위치 세팅 (술래 중심에서 방향벡터 * 반지름만큼 떨어진 곳)
                arrow.transform.position = transform.position + (Vector3)(dir * indicatorRadius);

                // 4. 화살표 회전 세팅 (화살표가 도망자를 쳐다보도록 회전)
                arrow.transform.rotation = Quaternion.Euler(0, 0, angle);

                yield return null;
            }

            // 시간이 끝나면 화살표 삭제
            if (arrow != null) Destroy(arrow);
        }
    }
}