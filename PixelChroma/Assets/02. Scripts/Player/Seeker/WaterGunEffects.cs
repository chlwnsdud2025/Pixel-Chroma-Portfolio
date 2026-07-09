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
        [Tooltip("레이저가 목표 지점까지 뻗어나가는 연출 시간(초)")]
        [SerializeField] private float laserDuration = 0.1f;
        [Tooltip("물 스플래시 이펙트가 서서히 사라지는 시간(초)")]
        [SerializeField] private float splashFadeDuration = 0.6f;

        public void PlayFireEffect(Transform firePoint, Vector2 targetPos, float aoeRadius)
        {
            StartCoroutine(AnimateLaserAndSplash(firePoint, targetPos, aoeRadius));
        }

        private IEnumerator AnimateLaserAndSplash(Transform firePoint, Vector2 targetPos, float aoeRadius)
        {
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

                // 캐릭터가 이동 중이어도 레이저 꼬리가 총구에 달라붙도록 실시간 위치를 사용합니다.
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

            // 스플래시 이펙트: 생성 후 서서히 사라지다가 스스로 파괴됩니다.
            // (현재 발사 빈도가 낮아 풀링 없이 Instantiate/Destroy로 처리합니다. 연사형 무기 도입 시 이 지점을 풀로 교체하세요.)
            if (waterSplashPrefab != null)
            {
                GameObject splash = Instantiate(waterSplashPrefab, targetPos, Quaternion.identity);
                var splashRenderer = splash.GetComponentInChildren<SpriteRenderer>();

                // 스프라이트 원본 크기를 기준으로 역산해 "보이는 반지름 = aoeRadius"가 되도록 맞춥니다.
                // 이렇게 하면 aoeRadius가 단일 기준이 되어 값이나 아트를 바꿔도 시각/판정이 항상 일치합니다.
                if (splashRenderer != null)
                {
                    float baseRadius = splashRenderer.sprite.bounds.extents.x;
                    if (baseRadius > 0.0001f)
                    {
                        float factor = aoeRadius / baseRadius;
                        splash.transform.localScale = new Vector3(factor, factor, 1f);
                    }

                    StartCoroutine(FadeOutAndDestroy(splash, splashRenderer, splashFadeDuration));
                }
                else
                {
                    Destroy(splash, splashFadeDuration);
                }
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
