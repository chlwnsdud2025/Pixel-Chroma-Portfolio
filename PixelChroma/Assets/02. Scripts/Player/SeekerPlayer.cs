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
        public float catchRadius = 1.5f; // (기존) 몸통 박치기로 잡는 범위

        [Header("Water Gun Settings")]
        [SerializeField] private Transform weaponPivot;       // 무기(팔)가 회전할 중심축
        [SerializeField] private float fireCooldown = 1.5f;   // 물총 발사 쿨타임
        [SerializeField] private float fireRange = 5f;        // 물총 최대 사거리
        [SerializeField] private float aoeRadius = 2f;        // 물풍선(물총) 폭발/지워짐 범위
        [SerializeField] private LayerMask runnerLayer;       // Runner를 감지할 물리 레이어
        [SerializeField] private LayerMask obstacleLayer;     // 물총이 막힐 벽 Layer
        [SerializeField] private GameObject waterSplashPrefab;// 물 튀기는 폭발 이펙트 프리팹 (옵션)

        // 다른 클라이언트들에게 내 팔이 어디를 조준하고 있는지 보여주기 위한 동기화 변수
        [SyncVar(hook = nameof(OnAimAngleChanged))]
        private float _syncAimAngle;

        private float _lastFireTime;
        private Camera _mainCam;
        private static Sprite _circleSprite; // 임시 원형 스프라이트 캐싱용


        private SpriteRenderer[] _weaponRenderers;
        protected override void OnLocalPlayerStarted()
        {
            moveSpeed = 4f;
            _mainCam = Camera.main;

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

            // 페인트 모드(isPaint)가 켜지면 무기를 끄고(!isPaint), 꺼지면 무기를 켭니다.
            // 이 동작은 네트워크로 전송되지 않으므로 '나'에게만 안 보이게 됩니다!
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
        }

        protected override void Update()
        {
            base.Update(); // PlayerBase의 이동 및 스크롤 로직 실행

            if (!isLocalPlayer) return;

            HandleAiming();
            HandleShooting();
        }

        // ── 1. 마우스 조준 및 팔 회전 로직 ──────────────────────────────────────
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
                float maxDist = Mathf.Min(distanceToTarget, fireRange);

                // 💡 [조준선 벽 감지] 총구에서 조준점까지 레이저를 쏴서 벽이 있는지 검사합니다.
                RaycastHit2D hit = Physics2D.Raycast(currentPos, aimDirection, maxDist, obstacleLayer);

                if (hit.collider != null)
                {
                    // 벽에 막혔다면, 조준선을 벽 표면(hit.point)에 고정시킵니다!
                    targetWorldPos = hit.point;
                }
                else
                {
                    // 벽이 없다면 기존처럼 최대 사거리까지만 뻗어나갑니다.
                    targetWorldPos = currentPos + aimDirection * maxDist;
                }

                Vector3 clampedScreenPos = _mainCam.WorldToScreenPoint(targetWorldPos);
                if (CursorManager.Instance != null)
                    CursorManager.Instance.overrideScreenPosition = clampedScreenPos;
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

        // ── 2. 물총 발사 클라이언트 로직 (사거리 제한 및 쿨타임) ───────────────────
        private void HandleShooting()
        {
            if (Mouse.current == null) return;

            if (!isPaintMode && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (Time.time - _lastFireTime >= fireCooldown)
                {
                    Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                    Vector3 mouseWorldPos = _mainCam.ScreenToWorldPoint(mouseScreenPos);
                    mouseWorldPos.z = 0;

                    Vector2 currentPos = weaponPivot != null ? weaponPivot.position : transform.position;
                    Vector2 targetPos = mouseWorldPos;
                    Vector2 dir = (targetPos - currentPos).normalized;

                    float dist = Mathf.Min(Vector2.Distance(currentPos, targetPos), fireRange);

                    // 클라이언트 측에서도 즉각적인 타격감을 위해 벽을 계산해서 쏴줍니다.
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

        // ── 3. 물총 폭발 서버 로직 및 Runner 감지 ────────────────────────────────
        [Command]
        private void CmdFireWaterGun(Vector2 targetPos)
        {
            Vector2 currentPos = weaponPivot != null ? weaponPivot.position : (Vector2)transform.position;
            Vector2 dir = (targetPos - currentPos).normalized;
            float dist = Vector2.Distance(currentPos, targetPos);

            // 클라이언트가 보낸 거리가 최대 사거리를 넘으면 컷!
            dist = Mathf.Min(dist, fireRange);

            // 서버가 직접 레이캐스트를 쏴서 중간에 벽이 있는지 확인합니다.
            RaycastHit2D hit = Physics2D.Raycast(currentPos, dir, dist, obstacleLayer);
            if (hit.collider != null)
            {
                targetPos = hit.point; // 벽에 막혔다면 최종 폭발 위치를 벽 표면으로 강제 수정!
            }

            // 확정된 targetPos(폭발 지점)를 기준으로 Runner가 반경 안에 있는지 검사
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(targetPos, aoeRadius, runnerLayer);

            foreach (var col in hitColliders)
            {
                var runner = col.GetComponent<RunnerPlayer>();
                if (runner != null)
                {
                    runner.ServerTakeEraseDamage(targetPos, aoeRadius);
                }
            }

            RpcPlayFireEffect(targetPos);
        }

        [ClientRpc]
        private void RpcPlayFireEffect(Vector2 effectPos)
        {
            // 코드로 임시 물풍선 폭발 원형 이펙트를 그려줍니다!
            ShowSplashEffect(effectPos, aoeRadius);

            if (waterSplashPrefab != null)
            {
                Instantiate(waterSplashPrefab, effectPos, Quaternion.identity);
            }

        }

        // ── 임시 시각 효과 (VFX) 로직 ──────────────────────────────────────

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
            sr.color = new Color(0.1f, 0.6f, 1f, 0.7f); // 파란색 반투명
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

        // ────────────────────────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isServer) return;

            var runner = other.GetComponent<RunnerPlayer>();
            if (runner != null && runner.role == PlayerRole.Runner)
            {
                // 필요 시 몸통 박치기 로직 복구
                // RpcOnRunnerCaught(runner.netId);
            }
        }

        [ClientRpc]
        private void RpcOnRunnerCaught(uint runnerNetId)
        {
            Debug.Log($"[Seeker] Runner(netId:{runnerNetId}) caught!");
        }

        protected override CursorType GetDefaultCursorType()
        {
            return CursorType.Crosshair; 
        }
    }
}