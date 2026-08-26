using System.Collections;
using UnityEngine;

public class MedalEffect : MonoBehaviour
{
    [Header("빛 스윕")]
    [SerializeField] private float shineDuration = 0.5f;
    [SerializeField] private float pauseDuration = 2.0f;

    private SpriteRenderer _sr;
    private MaterialPropertyBlock _mpb;
    private Coroutine _loop;

    private static readonly int PropShineProgress
        = Shader.PropertyToID("_ShineProgress");

    private const float ShineStart = -0.3f;  // 화면 밖 좌하단
    private const float ShineEnd = 1.3f;  // 화면 밖 우상단

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        SetShine(ShineStart);
        _loop = StartCoroutine(ShineLoop());
    }

    private void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }

    private IEnumerator ShineLoop()
    {
        while (true)
        {
            // ── 빛 스윕 (↗ 방향) ──
            float elapsed = 0f;
            while (elapsed < shineDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shineDuration;
                SetShine(Mathf.Lerp(ShineStart, ShineEnd, t));
                yield return null;
            }
            SetShine(ShineStart);

            // ── 대기 ──
            yield return new WaitForSeconds(pauseDuration);
        }
    }

    private void SetShine(float value)
    {
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(PropShineProgress, value);
        _sr.SetPropertyBlock(_mpb);
    }
}