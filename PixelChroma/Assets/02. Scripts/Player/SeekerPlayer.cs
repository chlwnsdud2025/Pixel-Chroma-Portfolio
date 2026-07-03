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
        [Header("Seeker Basic Settings")]
        public float catchRadius = 1.5f;

        [Header("Water Gun Settings")]
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private float fireCooldown = 1.5f;
        [SerializeField] private float fireRange = 5f;
        [SerializeField] private float aoeRadius = 2f;
        [SerializeField] private LayerMask runnerLayer;
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private GameObject waterSplashPrefab;

        [SyncVar(hook = nameof(OnAimAngleChanged))]
        private float _syncAimAngle;

        private float _lastFireTime;
        private Camera _mainCam;
        private static Sprite _circleSprite;

        private SpriteRenderer[] _weaponRenderers;

        public static readonly System.Collections.Generic.List<SeekerPlayer> AllSeekers = new();

        private RunnerPlayer _targetRunnerToKill = null;
        private UIInGameHUD _inGameHUD;

        [SerializeField] private float wettimer = 5f;


        protected override void OnLocalPlayerStarted()
        {
            moveSpeed = 4f;
            _mainCam = Camera.main;
            _inGameHUD = FindObjectOfType<UIInGameHUD>();
            if (weaponPivot != null)
            {
                _weaponRenderers = weaponPivot.GetComponentsInChildren<SpriteRenderer>(true);
            }

            OnPaintModeChanged += HandleLocalPaintModeChanged;
            ShowHUDAsync().Forget();
        }

        protected override void OnLocalPlayerStopped()
        {
            UnbindPaintHUD();
            OnPaintModeChanged -= HandleLocalPaintModeChanged;

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

        private async UniTaskVoid ShowHUDAsync()
        {
            var hud = await UIManager.Instance.ShowScene<UIInGameHUD>();
            BindPaintHUD(hud);

            hud.SetKillButtonVisible(true);
        }

        protected override void Update()
        {
            base.Update();

            if (!isLocalPlayer) return;

            CheckForKillableRunner(); // 테스트
            HandleKillInput();
            HandleAiming();
            HandleShooting();
        }
        //---------------------------------물에젖음 테스트 --------------------------
        private void CheckForKillableRunner()
        {
            _targetRunnerToKill = null;
            float closestDist = catchRadius;

            foreach (var runner in RunnerPlayer.AllRunners)
            {
                // 💡 젖어 있고, 아직 죽지 않은(관전 중이 아닌) 도망자만 필터링
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
            if (_inGameHUD != null)
            {
                if (_targetRunnerToKill != null)
                {
                    _inGameHUD.SetKillButtonInteractable(true, ExecuteKill);
                }
                else
                {
                    _inGameHUD.SetKillButtonInteractable(false);
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

            if (!isPaintMode)
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

            if (!isPaintMode && Mouse.current.leftButton.wasPressedThisFrame)
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
            Vector2 currentPos = weaponPivot != null ? weaponPivot.position : (Vector2)transform.position;
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
        private void RpcPlayFireEffect(Vector2 effectPos)
        {
            ShowSplashEffect(effectPos, aoeRadius);

            if (waterSplashPrefab != null)
            {
                Instantiate(waterSplashPrefab, effectPos, Quaternion.identity);
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

        protected override CursorType GetDefaultCursorType()
        {
            return CursorType.Crosshair;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!AllSeekers.Contains(this)) AllSeekers.Add(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            AllSeekers.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, catchRadius);
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

            // 💡 만약 카메라를 시네머신으로 쓴다면, 텔레포트 시 카메라가 쭈욱 따라오지 않고 즉시 순간이동 하도록 강제 업데이트
            if (cinemachineCamera != null)
            {
                cinemachineCamera.PreviousStateIsValid = false;
            }

            Debug.Log("[Seeker] 본진으로 텔레포트 되었습니다!");
        }
    }
}