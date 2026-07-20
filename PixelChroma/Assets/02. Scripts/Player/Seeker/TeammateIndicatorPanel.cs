using Mirror;
using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma.UI
{
    public class TeammateIndicatorPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform indicatorRoot;
        [SerializeField] private GameObject indicatorIconPrefab;
        [SerializeField] private float edgePadding = 60f;

        [Header("Target Settings")]
        [Tooltip("어떤 역할의 팀원을 추적할지 (Seeker → 다른 Seeker 표시, Runner → 다른 Runner 표시)")]
        [SerializeField] private PlayerRole targetRole = PlayerRole.Seeker;

        [Header("Distance Filter")]
        [Tooltip("이 거리 이내의 팀원만 인디케이터를 표시합니다 (0 = 제한 없음)")]
        [SerializeField] private float maxTrackingDistance = 30f;

        private Camera targetCamera;
        private readonly List<IndicatorIconView> _pool = new();

        private void OnEnable()
        {
            targetCamera = Camera.main;
        }

        public void Deactivate()
        {
            HideAll();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (targetCamera == null) { HideAll(); return; }

            // GameModeRule 체크: 현재 모드에서 이 역할의 인디케이터를 표시해야 하는가?
            var rule = GameRoundManager.Instance?.CurrentRule;
            if (rule != null && !rule.ShouldShowTeammateIndicator(targetRole))
            {
                HideAll();
                return;
            }

            //거리체크
            Transform localTransform = NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.transform
                : null;

            if (localTransform == null) { HideAll(); return; }


            // targetRole에 따라 추적할 플레이어 리스트 결정
            int used = 0;

            if (targetRole == PlayerRole.Seeker)
            {
                var seekers = InGameManager.Instance != null ? InGameManager.Instance.Seekers : null;
                if (seekers == null) { HideAll(); return; }

                foreach (var seeker in seekers)
                {
                    if (seeker == null || seeker.isLocalPlayer) continue;
                    if (!IsWithinRange(localTransform, seeker.transform)) continue;
                    TryPlaceIndicator(seeker.transform, seeker.playerName, ref used);
                }
            }
            else if (targetRole == PlayerRole.Runner)
            {
                var runners = InGameManager.Instance != null ? InGameManager.Instance.Runners : null;
                if (runners == null) { HideAll(); return; }

                foreach (var runner in runners)
                {
                    if (runner == null || runner.isLocalPlayer) continue;
                    if (runner.IsDead) continue; // 사망한 Runner는 표시하지 않음
                    if (!IsWithinRange(localTransform, runner.transform)) continue;
                    TryPlaceIndicator(runner.transform, runner.playerName, ref used);
                }
            }

            for (int i = used; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);
        }

        // 화면 밖에 있으면 가장자리에 배치, 화면 안이면 스킵. 배치했으면 used 증가.
        private bool TryPlaceIndicator(Transform target, string playerName, ref int used)
        {
            Vector3 viewport = targetCamera.WorldToViewportPoint(target.position);
            bool onScreen = viewport.z > 0f
                && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;

            if (onScreen) return true;

            var icon = GetIcon(used++);
            PlaceOnEdge(icon, viewport);
            icon.SetNickname(playerName);
            return true;
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

            icon.Slot.anchoredPosition = dir * scale;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            icon.RingArrow.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private void HideAll()
        {
            foreach (var icon in _pool)
                icon.gameObject.SetActive(false);
        }
    

        private bool IsWithinRange(Transform local, Transform target)
        {
            if (maxTrackingDistance <= 0f) return true; // 0이면 무제한
            float sqrDist = (target.position - local.position).sqrMagnitude;
            return sqrDist <= maxTrackingDistance * maxTrackingDistance;
        }
    }
}