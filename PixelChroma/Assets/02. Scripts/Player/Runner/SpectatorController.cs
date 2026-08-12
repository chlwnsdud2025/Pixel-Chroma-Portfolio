using System;
using UnityEngine;
using Unity.Cinemachine;
namespace PixelChroma
{
    public enum SpectateMode
    {
        None,
        Free,
        Runner,
        Seeker
    }

    public class SpectatorController
    {
        private const float SpectateTransitionTime = 5f;  // 관전 대상 전환 시 카메라 도달 목표 시간(초). 거리와 무관하게 균일.
        private const float ArriveThreshold = 0.05f;

        private const float SnapDistance = 500f;        // 이 거리(월드 유닛) 이상 벌어지면 Lerp 대신 즉시 스냅(컷).

        private readonly PlayerBase _owner;
        private readonly CinemachineCamera _camera;
        private readonly CinemachinePositionComposer _composer;   // vcam의 Body(Position Composer)
        private readonly float _freeCamSpeed;

        private SpectateMode _mode = SpectateMode.None;
        private GameObject _freeCamTarget;
        private int _targetIndex = -1;
        private PlayerBase _currentTarget;

        private Vector3 _defaultDamping;

        // 관전 대상이 바뀔 때 발생합니다. null이면 자유 시점이거나 관전을 끝낸 상태입니다.
        public event Action<PlayerBase> OnTargetChanged;

        public SpectateMode Mode => _mode;
        public bool IsSpectating => _mode != SpectateMode.None;
        public PlayerBase CurrentTarget => _currentTarget;

        public SpectatorController(PlayerBase owner, CinemachineCamera camera, float freeCamSpeed)
        {
            _owner = owner;
            _camera = camera;
            _freeCamSpeed = freeCamSpeed;

            _composer = camera != null ? camera.GetComponent<CinemachinePositionComposer>() : null;
            if (_composer != null) _defaultDamping = _composer.Damping;

            _freeCamTarget = new GameObject($"FreeCamTarget_{_owner.netId}");
            _freeCamTarget.transform.position = owner.transform.position;
        }

        public void Dispose()
        {
            RestoreDefaultDamping();

            if (_freeCamTarget != null)
                UnityEngine.Object.Destroy(_freeCamTarget);
        }

        public void EnterDeathSpectator()
        {
            // 사망(관전 진입) 시 기본은 크로마(러너) 순회 시점.
            EnterRunnerMode();
        }

        public void EnterFreeMode()
        {
            if (_freeCamTarget == null)
                _freeCamTarget = new GameObject($"FreeCamTarget_{_owner.netId}");

            // 보던 시점에서 이어서 시작하도록, 관전 중이던 대상 위치에서 자유 카메라를 띄웁니다.
            if (_mode != SpectateMode.Free)
            {
                _freeCamTarget.transform.position = _currentTarget != null
                    ? _currentTarget.transform.position
                    : _owner.transform.position;
            }

            _mode = SpectateMode.Free;
            _targetIndex = -1;

            SetTarget(null, _freeCamTarget.transform);
        }

        public void EnterRunnerMode()
        {
            _mode = SpectateMode.Runner;
            _targetIndex = -1;
            CycleTarget(1);
        }

        public void EnterSeekerMode()
        {
            _mode = SpectateMode.Seeker;
            _targetIndex = -1;
            CycleTarget(1);
        }

        public void ExitSpectate()
        {
            _mode = SpectateMode.None;
            _targetIndex = -1;

            if (_owner == null) return;   // 파괴된 소유자면 transform 접근 시 NRE

            SetTarget(null, _owner.transform);   // 기본 damping으로 복귀
        }

        public void CycleTarget(int direction)
        {
            if (_mode == SpectateMode.Runner)
                CycleRunner(direction);
            else if (_mode == SpectateMode.Seeker)
                CycleSeeker(direction);
        }

        public void HandleFreeCamMovement(Vector2 input)
        {
            if (_mode != SpectateMode.Free) return;
            if (_freeCamTarget != null)
                _freeCamTarget.transform.position += (Vector3)input * _freeCamSpeed * Time.fixedDeltaTime;
        }

        private void CycleRunner(int direction)
        {
            // 러너가 관전할 때만 모드 규칙의 관전 대상 필터를 적용하고,
            // 그 외(시커 관전 등)에는 전체 러너 명부를 사용합니다.
            var runners = (_owner is RunnerPlayer runnerOwner
                    ? GameRoundManager.Instance?.CurrentRule?.GetSpectatableRunners(runnerOwner)
                    : null)
                ?? InGameManager.Instance?.Runners;

            if (runners == null || runners.Count == 0) return;

            int attemptCount = 0;
            while (attemptCount < runners.Count)
            {
                _targetIndex = (_targetIndex + direction + runners.Count) % runners.Count;
                var target = runners[_targetIndex];

                bool isCheckTime = GameRoundManager.Instance != null
                    && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime;

                if (target != null && (!target.IsDead || isCheckTime))
                {
                    SetTarget(target, target.transform);
                    return;
                }
                attemptCount++;
            }
        }

        private void CycleSeeker(int direction)
        {
            var seekers = InGameManager.Instance?.Seekers;
            if (seekers == null || seekers.Count == 0) return;

            int attemptCount = 0;
            while (attemptCount < seekers.Count)
            {
                _targetIndex = (_targetIndex + direction + seekers.Count) % seekers.Count;
                var target = seekers[_targetIndex];

                if (target != null)
                {
                    SetTarget(target, target.transform);
                    return;
                }
                attemptCount++;
            }
        }

        private void SetTarget(PlayerBase target, Transform cameraFollow)
        {
            if (_camera != null)
            {
                if (target != null)
                    ApplyDistanceBasedDamping(cameraFollow);   // 러너/시커 관전 → 균일 시간 전환
                else
                    RestoreDefaultDamping();                    // 자유 시점/관전 종료 → 기본 damping

                _camera.Follow = cameraFollow;
            }

            if (_currentTarget == target) return;

            _currentTarget = target;
            OnTargetChanged?.Invoke(target);
        }

        private void ApplyDistanceBasedDamping(Transform followTarget)
        {
            ApplyFixedTimeDamping(followTarget, SpectateTransitionTime);
        }

        // 현재 카메라 위치에서 대상까지, 대략 t초 안에 도착하도록 damping을 역산해 적용합니다.
        private void ApplyFixedTimeDamping(Transform followTarget, float t)
        {
            if (_composer == null || followTarget == null || _camera == null) return;

            float d0 = Vector2.Distance(_camera.transform.position, followTarget.position);

            // 존 간 이동처럼 거리가 매우 멀면, 빈 공간을 가로지르는 팬 대신 즉시 스냅(컷).
            if (d0 >= SnapDistance)
            {
                SnapDamping();
                return;
            }

            float ratio = Mathf.Max(d0 / ArriveThreshold, 1.01f);   // ln > 0 보장
            float damping = t / Mathf.Log(ratio);                   // τ = T / ln(d0/ε)
            damping = Mathf.Clamp(damping, 0.02f, 5f);

            var d = _composer.Damping;
            d.x = damping;
            d.y = damping;                 // z는 2D라 그대로 유지
            _composer.Damping = d;
        }

        // damping을 0으로 만들어 다음 갱신에서 대상 위치로 즉시 스냅시킵니다.
        private void SnapDamping()
        {
            if (_composer == null) return;

            var d = _composer.Damping;
            d.x = 0f;
            d.y = 0f;
            _composer.Damping = d;
        }

        private void RestoreDefaultDamping()
        {
            if (_composer != null)
                _composer.Damping = _defaultDamping;
        }
    }
}
