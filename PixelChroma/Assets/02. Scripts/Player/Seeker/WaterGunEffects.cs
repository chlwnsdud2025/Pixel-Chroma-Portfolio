using System.Collections;
using UnityEngine;

namespace PixelChroma
{
    // 물총 발사 연출(레이저/스플래시)을 담당하는 순수 클라이언트 컴포넌트입니다.
    // 네트워크 로직이 없으므로 SeekerPlayer의 RpcPlayFireEffect가 실행만 위임합니다.
    public class WaterGunEffects : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("LineRenderer를 가진 레이저 프리팹")]
        [SerializeField] private GameObject laserPrefab;
        [Tooltip("착탄 지점에 생성될 물 스플래시 프리팹")]
        [SerializeField] private GameObject waterSplashPrefab;

        [Header("Timing")]
        [Tooltip("레이저가 초당 뻗어나가는 속도(유닛/초)")]
        [SerializeField] private float laserSpeed = 40f;
        //[Tooltip("레이저 라인의 두께")]
        //[SerializeField] private float laserWidth = 0.1f;
        //[Tooltip("레이저 빔이 뻗어나갈 때 보이는 선분의 길이(유닛). 사거리와 무관하게 항상 이 길이로 고정됩니다.")]
        //[SerializeField] private float laserSegmentLength = 3f;
        [Tooltip("물 스플래시 이펙트가 서서히 사라지는 시간(초)")]
        [SerializeField] private float splashFadeDuration = 0.6f;

        public void PlayFireEffect(Transform firePoint, Vector2 targetPos, float aoeRadius, bool didHit, Vector2 direction, Vector2 hitNormal)
        {
            StartCoroutine(AnimateLaserAndSplash(firePoint, targetPos, aoeRadius, didHit, direction, hitNormal));
        }

        private IEnumerator AnimateLaserAndSplash(Transform firePoint, Vector2 targetPos, float aoeRadius, bool didHit, Vector2 direction, Vector2 hitNormal)
        {
            GameObject laserObj = null;

            Vector2 startPos = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
            float totalDistance = Vector2.Distance(startPos, targetPos);
            float duration = laserSpeed > 0f ? Mathf.Max(totalDistance / laserSpeed, 0.01f) : 0.01f;

            if (laserPrefab != null)
            {
                float travelAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                laserObj = Instantiate(laserPrefab, startPos, Quaternion.Euler(0, 0, travelAngle));
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float ratio = elapsed / duration;

                if (laserObj != null)
                    laserObj.transform.position = Vector2.Lerp(startPos, targetPos, ratio);

                yield return null;
            }

            if (laserObj != null) Destroy(laserObj);

            if (!didHit) yield break;

            if (waterSplashPrefab != null)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Quaternion splashRotation = Quaternion.Euler(0, 0, angle);   // 스프라이트 기본 방향에 따라 필요시 오프셋 조정

                GameObject splash = Instantiate(waterSplashPrefab, targetPos, splashRotation);
                var splashRenderer = splash.GetComponentInChildren<SpriteRenderer>();

                if (splashRenderer != null && splashRenderer.sprite != null)
                {
                    float baseRadius = splashRenderer.sprite.bounds.extents.x;
                    if (baseRadius > 0.0001f)
                    {
                        float factor = aoeRadius / baseRadius;
                        splash.transform.localScale = new Vector3(factor, factor, 1f);
                    }
                }

                if (splashRenderer != null)
                    StartCoroutine(FadeOutAndDestroy(splash, splashRenderer, splashFadeDuration));
                else
                    Destroy(splash, splashFadeDuration);
            }
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
    }
}
