using UnityEngine;

namespace PixelChroma
{
    // 위치 노출 스킬 화살표: 술래(owner) 주위 궤도를 돌며 타겟(도망자)을 가리키다가
    // 수명이 끝나면 스스로 파괴됩니다. 수명을 직접 관리하므로 술래가 먼저 사라져도 잔류하지 않습니다.
    public class IndicatorArrow : MonoBehaviour
    {
        private Transform _owner;
        private Transform _target;
        private float _radius;
        private float _remaining;

        public void Init(Transform owner, Transform target, float radius, float duration)
        {
            _owner = owner;
            _target = target;
            _radius = radius;
            _remaining = duration;

            ApplyOrbitTransform();
        }

        private void Update()
        {
            _remaining -= Time.deltaTime;

            // 시간이 다 되었거나, 술래/도망자가 사라지면 함께 정리됩니다.
            if (_remaining <= 0f || _owner == null || _target == null)
            {
                Destroy(gameObject);
                return;
            }

            ApplyOrbitTransform();
        }

        // 술래 중심에서 반지름만큼 떨어진 곳에서 도망자를 가리키도록 위치/회전 세팅
        private void ApplyOrbitTransform()
        {
            if (_owner == null || _target == null) return;

            Vector2 dir = (_target.position - _owner.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            transform.position = _owner.position + (Vector3)(dir * _radius);
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
