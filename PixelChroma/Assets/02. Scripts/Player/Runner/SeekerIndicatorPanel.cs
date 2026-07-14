using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma.UI
{
    // 화면 밖에 있는 Seeker 방향을 화면 가장자리에 아이콘으로 표시합니다.
    public class SeekerIndicatorPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform indicatorRoot;    // 아이콘들이 배치될 부모 RectTransform (풀스크린 크기)
        [SerializeField] private GameObject indicatorIconPrefab; // Image가 붙은 아이콘 프리팹
        [SerializeField] private float edgePadding = 60f;        // 화면 가장자리에서 안쪽으로 띄우는 여백(px)

        private Camera targetCamera;
        private readonly List<RectTransform> _pool = new();
        private bool _active;
        private float _remaining;

        public void Activate(float duration)
        {
            _active = true;
            _remaining = duration;
            targetCamera = Camera.main;
        }

        private void Update()
        {
            if (!_active) return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                _active = false;
                HideAll();
                return;
            }

            Refresh();
        }

        private void Refresh()
        {
            var seekers = InGameManager.Instance != null ? InGameManager.Instance.Seekers : null;
            if (seekers == null || targetCamera == null) { HideAll(); return; }

            int used = 0;
            foreach (var seeker in seekers)
            {
                if (seeker == null) continue;

                Vector3 viewport = targetCamera.WorldToViewportPoint(seeker.transform.position);
                bool onScreen = viewport.z > 0f
                    && viewport.x >= 0f && viewport.x <= 1f
                    && viewport.y >= 0f && viewport.y <= 1f;

                if (onScreen) continue; // 요구사항 1: 화면 안이면 표시 안 함

                PlaceOnEdge(GetIcon(used++), viewport);
            }

            for (int i = used; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);
        }

        private RectTransform GetIcon(int index)
        {
            if (index < _pool.Count)
            {
                _pool[index].gameObject.SetActive(true);
                return _pool[index];
            }

            var rect = Instantiate(indicatorIconPrefab, indicatorRoot).GetComponent<RectTransform>();
            _pool.Add(rect);
            return rect;
        }

        private void PlaceOnEdge(RectTransform icon, Vector3 viewport)
        {
            // 카메라 뒤에 있는 경우 방향이 반전되므로 보정 (2D 탑다운에서는 거의 없지만 안전장치)
            if (viewport.z < 0f)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            Vector2 dir = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;

            Rect screenRect = indicatorRoot.rect;
            float halfW = screenRect.width / 2f - edgePadding;
            float halfH = screenRect.height / 2f - edgePadding;

            // 방향벡터를 화면 사각형 경계까지 늘렸을 때, 가로/세로 중 먼저 닿는 축 기준으로 스케일 계산
            float scale = Mathf.Min(
                dir.x != 0f ? halfW / Mathf.Abs(dir.x) : float.MaxValue,
                dir.y != 0f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);

            icon.anchoredPosition = dir * scale;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            icon.localRotation = Quaternion.Euler(0, 0, angle); // 아이콘 아트가 오른쪽(→)을 기본 방향으로 그려졌다고 가정
        }

        private void HideAll()
        {
            foreach (var icon in _pool)
                icon.gameObject.SetActive(false);
        }
    }
}