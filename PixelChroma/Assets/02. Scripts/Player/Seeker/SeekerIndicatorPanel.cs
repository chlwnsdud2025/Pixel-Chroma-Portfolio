using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma.UI
{
    // 화면 밖에 있는 Seeker 방향을 화면 가장자리에 아이콘으로 표시합니다.
    public class SeekerIndicatorPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform indicatorRoot;    // 아이콘들이 배치될 부모 RectTransform (풀스크린 크기)
        [SerializeField] private GameObject indicatorIconPrefab; // IndicatorIconView가 붙은 아이콘 프리팹
        [SerializeField] private float edgePadding = 60f;        // 화면 가장자리에서 안쪽으로 띄우는 여백(px)

        private Camera targetCamera;
        private readonly List<IndicatorIconView> _pool = new();

        private void OnEnable()
        {
            targetCamera = Camera.main;
        }

        public void Deactivate()
        {
            _active = false;
            _remaining = 0f;
            HideAll();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            var seekers = InGameManager.Instance != null ? InGameManager.Instance.Seekers : null;
            if (seekers == null || targetCamera == null) { HideAll(); return; }

            int used = 0;
            foreach (var seeker in seekers)
            {
                if (seeker == null || seeker.isLocalPlayer) continue;   // 나 자신은 인디케이터 제외

                Vector3 viewport = targetCamera.WorldToViewportPoint(seeker.transform.position);
                bool onScreen = viewport.z > 0f
                    && viewport.x >= 0f && viewport.x <= 1f
                    && viewport.y >= 0f && viewport.y <= 1f;

                if (onScreen) continue;   // 화면 안이면 표시 안 함 (기존 로직 그대로)

                PlaceOnEdge(GetIcon(used++), viewport);
            }

            for (int i = used; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);
        }

        private IndicatorIconView GetIcon(int index)
        {
            if (index < _pool.Count)
            {
                _pool[index].gameObject.SetActive(true);
                return _pool[index];
            }

            var view = Instantiate(indicatorIconPrefab, indicatorRoot).GetComponent<IndicatorIconView>();
            _pool.Add(view);
            return view;
        }

        private void PlaceOnEdge(IndicatorIconView icon, Vector3 viewport)
        {
            if (viewport.z < 0f)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            Rect screenRect = indicatorRoot.rect;

            // 뷰포트(0~1, 정사각형 정규화 공간)의 델타를 그대로 쓰면 화면 종횡비 때문에 각도가 왜곡됨.
            // 캔버스의 실제 가로/세로 픽셀 크기를 곱해서 "픽셀 비율이 반영된" 방향벡터로 변환.
            Vector2 pixelDir = new Vector2(
                (viewport.x - 0.5f) * screenRect.width,
                (viewport.y - 0.5f) * screenRect.height);

            if (pixelDir.sqrMagnitude < 0.0001f) pixelDir = Vector2.up;
            Vector2 dir = pixelDir.normalized;

            float halfW = screenRect.width / 2f - edgePadding;
            float halfH = screenRect.height / 2f - edgePadding;

            float scale = Mathf.Min(
                dir.x != 0f ? halfW / Mathf.Abs(dir.x) : float.MaxValue,
                dir.y != 0f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);

            // 위치는 Slot(껍데기, icon 자신)에 — SeekerFace도 같이 따라 움직임
            icon.Slot.anchoredPosition = dir * scale;

            // 회전은 RingArrow(자식)에만 — SeekerFace는 손 안 댐 = 항상 정자세
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            icon.RingArrow.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private void HideAll()
        {
            foreach (var icon in _pool)
                icon.gameObject.SetActive(false);
        }
    }
}