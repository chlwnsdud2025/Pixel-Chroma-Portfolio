using DG.Tweening;
using OneText.UGUI;
using UnityEngine;

namespace PixelChroma.UI
{
    /// <summary>
    /// Runner 머리 위의 좋아요 입력 안내를 표시하고 숨기는 World Space UI 뷰입니다.
    /// Canvas 루트에 붙이면 자신의 RectTransform/CanvasGroup을 기본 애니메이션 대상으로 사용합니다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class WorldLikeGuideView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform animatedRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private OneTextLabel keyText;

        [Header("Show")]
        [SerializeField, Min(0f)] private float showDuration = 0.2f;
        [SerializeField, Range(0f, 1f)] private float showStartScale = 0.75f;
        [SerializeField] private Vector2 showStartOffset = new(0f, -12f);
        [SerializeField] private Ease showEase = Ease.OutBack;

        [Header("Hide")]
        [SerializeField, Min(0f)] private float hideDuration = 0.15f;
        [SerializeField, Range(0f, 1f)] private float hideEndScale = 0.85f;
        [SerializeField] private Vector2 hideEndOffset = new(0f, 8f);
        [SerializeField] private Ease hideEase = Ease.InQuad;

        private Sequence _sequence;
        private Vector2 _visiblePosition;
        private Vector3 _visibleScale;
        private bool _initialized;
        private bool _isVisible;

        private void Awake()
        {
            Initialize();
            ApplyHiddenImmediately();
        }

        private void OnEnable()
        {
            if (!_initialized) return;

            if (_isVisible)
                ApplyVisibleImmediately();
            else
                ApplyHiddenImmediately();
        }

        private void OnDisable()
        {
            KillSequence();
        }

        private void OnDestroy()
        {
            KillSequence();
        }

        public void Show(string bindingDisplay)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            Initialize();
            if (animatedRoot == null || canvasGroup == null)
                return;

            if (keyText != null)
                keyText.text = string.IsNullOrWhiteSpace(bindingDisplay) ? "♥" : $"[{bindingDisplay}]";

            if (_isVisible)
                return;

            _isVisible = true;
            KillSequence();

            canvasGroup.alpha = 0f;
            animatedRoot.anchoredPosition = _visiblePosition + showStartOffset;
            animatedRoot.localScale = _visibleScale * showStartScale;

            _sequence = DOTween.Sequence().SetLink(gameObject);
            _sequence.Join(canvasGroup.DOFade(1f, showDuration).SetEase(Ease.OutQuad));
            _sequence.Join(animatedRoot.DOAnchorPos(_visiblePosition, showDuration).SetEase(Ease.OutQuad));
            _sequence.Join(animatedRoot.DOScale(_visibleScale, showDuration).SetEase(showEase));
            _sequence.OnComplete(() => _sequence = null);
        }

        public void Hide()
        {
            Initialize();
            if (!_isVisible || animatedRoot == null || canvasGroup == null)
                return;

            _isVisible = false;
            KillSequence();

            _sequence = DOTween.Sequence().SetLink(gameObject);
            _sequence.Join(canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad));
            _sequence.Join(animatedRoot.DOAnchorPos(_visiblePosition + hideEndOffset, hideDuration).SetEase(hideEase));
            _sequence.Join(animatedRoot.DOScale(_visibleScale * hideEndScale, hideDuration).SetEase(hideEase));
            _sequence.OnComplete(() =>
            {
                _sequence = null;
                ApplyHiddenImmediately();
                gameObject.SetActive(false);
            });
        }

        private void Initialize()
        {
            if (_initialized) return;

            if (animatedRoot == null)
                animatedRoot = transform as RectTransform;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (keyText == null)
                keyText = GetComponentInChildren<OneTextLabel>(true);

            if (animatedRoot != null)
            {
                _visiblePosition = animatedRoot.anchoredPosition;
                _visibleScale = animatedRoot.localScale;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            _initialized = true;
        }

        private void ApplyVisibleImmediately()
        {
            if (animatedRoot == null || canvasGroup == null) return;
            canvasGroup.alpha = 1f;
            animatedRoot.anchoredPosition = _visiblePosition;
            animatedRoot.localScale = _visibleScale;
        }

        private void ApplyHiddenImmediately()
        {
            if (animatedRoot == null || canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            animatedRoot.anchoredPosition = _visiblePosition;
            animatedRoot.localScale = _visibleScale;
        }

        private void KillSequence()
        {
            _sequence?.Kill();
            _sequence = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (animatedRoot == null)
                animatedRoot = transform as RectTransform;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }
#endif
    }
}
