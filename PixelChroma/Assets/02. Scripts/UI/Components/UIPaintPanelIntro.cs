using DG.Tweening;
using UnityEngine;

namespace PixelChroma.UI
{
    /// <summary>
    /// PaintModeUI(UIColorPickerGroup) 등장 연출.
    /// 그룹 전체를 좌측에서 우측으로 slide + fade 시킨다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPaintPanelIntro : MonoBehaviour
    {
        [Header("Slide")]
        [Tooltip("좌측에서 시작할 거리(px)")]
        [SerializeField] private float slideDistance = 60f;
        [Tooltip("이동 시간(초)")]
        [SerializeField] private float duration = 0.35f;
        [Tooltip("시작 지연(초)")]
        [SerializeField] private float startDelay = 0f;
        [Tooltip("감속 커브")]
        [SerializeField] private Ease ease = Ease.OutCubic;

        private RectTransform _rect;
        private CanvasGroup _cg;
        private Vector2 _restPos;
        private bool _captured;
        private Sequence _seq;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            // 정착 위치는 레이아웃 제어를 받지 않아 불변 → 최초 1회만 캡처
            if (!_captured)
            {
                _restPos = _rect.anchoredPosition;
                _captured = true;
            }
            Play();
        }

        private void OnDisable()
        {
            _seq?.Kill();
            _seq = null;

            _rect.anchoredPosition = _restPos;
            _cg.alpha = 1f;
        }

        private void Play()
        {
            _seq?.Kill();

            // 초기 상태: 좌측으로 밀고 투명하게
            _rect.anchoredPosition = _restPos + new Vector2(-slideDistance, 0f);
            _cg.alpha = 0f;

            _seq = DOTween.Sequence().SetLink(gameObject);
            _seq.Insert(startDelay, _rect.DOAnchorPos(_restPos, duration).SetEase(ease));
            _seq.Insert(startDelay, _cg.DOFade(1f, duration).SetEase(Ease.OutQuad));
        }
    }
}
