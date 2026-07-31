using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    [DisallowMultipleComponent]
    public class UIActionKeyPanelIntro : MonoBehaviour
    {
        [Header("Panel Slide")]
        [SerializeField] private float slideDistance = 400f;   // 좌측에서 시작할 거리(px)
        [SerializeField] private float slideDuration = 0.45f;
        [SerializeField] private float slideOvershoot = 1.4f;  // OutBack 되튐 강도

        [Header("Item Stagger")]
        [SerializeField] private float itemStartDelay = 0.12f; // 패널이 들어온 뒤 항목 시작까지 대기(초)
        [SerializeField] private float itemStagger = 0.06f;    // 항목 간 간격(초)
        [SerializeField] private float itemDuration = 0.35f;
        [SerializeField] private float itemStartScale = 0.6f;
        [SerializeField] private float itemOvershoot = 2.0f;   // 항목 팝인 되튐 강도

        private readonly List<RectTransform> _itemRects = new();
        private readonly List<CanvasGroup> _itemGroups = new();

        private RectTransform _rect;
        private Vector2 _restPos;

        private Sequence _seq;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            CacheItems();

            // 부모 레이아웃을 먼저 확정해 정착 위치를 캡처(레이아웃 그룹과의 충돌 방지)
            if (_rect.parent is RectTransform parent)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            _restPos = _rect.anchoredPosition;

            Play();
        }

        private void OnDisable()
        {
            _seq?.Kill();
            _seq = null;

            // 다음 활성화를 위해 원상 복구
            _rect.anchoredPosition = _restPos;
            for (int i = 0; i < _itemRects.Count; i++)
            {
                _itemRects[i].localScale = Vector3.one;
                _itemGroups[i].alpha = 1f;
            }
        }

        private void CacheItems()
        {
            _itemRects.Clear();
            _itemGroups.Clear();

            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i) is not RectTransform child) continue;
                if (!child.gameObject.activeSelf) continue;

                var cg = child.GetComponent<CanvasGroup>();
                if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();

                _itemRects.Add(child);
                _itemGroups.Add(cg);
            }
        }

        private void Play()
        {
            _seq?.Kill();

            // 초기 상태 세팅(첫 프레임 렌더 전이라 깜빡임 없음)
            _rect.anchoredPosition = _restPos + new Vector2(-slideDistance, 0f);
            for (int i = 0; i < _itemRects.Count; i++)
            {
                _itemRects[i].localScale = Vector3.one * itemStartScale;
                _itemGroups[i].alpha = 0f;
            }

            _seq = DOTween.Sequence().SetLink(gameObject);

            // 1) 패널 슬라이드: 좌측 -> 정착, OutBack으로 살짝 넘쳤다 복귀
            _seq.Append(_rect.DOAnchorPos(_restPos, slideDuration)
                .SetEase(Ease.OutBack, slideOvershoot));

            // 2) 항목 순차 팝인: scale(OutBack) + alpha 페이드
            for (int i = 0; i < _itemRects.Count; i++)
            {
                float at = itemStartDelay + i * itemStagger;
                _seq.Insert(at, _itemRects[i].DOScale(1f, itemDuration)
                    .SetEase(Ease.OutBack, itemOvershoot));
                _seq.Insert(at, _itemGroups[i].DOFade(1f, itemDuration * 0.7f)
                    .SetEase(Ease.OutQuad));
            }
        }
    }
}
