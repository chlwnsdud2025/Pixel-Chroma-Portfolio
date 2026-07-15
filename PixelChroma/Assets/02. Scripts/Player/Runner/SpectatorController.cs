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
        private readonly PlayerBase _owner;
        private readonly CinemachineCamera _camera;
        private readonly float _freeCamSpeed;

        private SpectateMode _mode = SpectateMode.None;
        private GameObject _freeCamTarget;
        private int _targetIndex = -1;

        public SpectateMode Mode => _mode;
        public bool IsSpectating => _mode != SpectateMode.None;
        public event Action<string, bool> OnTargetNameChanged;

        public SpectatorController(PlayerBase owner, CinemachineCamera camera, float freeCamSpeed)
        {
            _owner = owner;
            _camera = camera;
            _freeCamSpeed = freeCamSpeed;

            _freeCamTarget = new GameObject("FreeCamTarget");
            _freeCamTarget.transform.position = owner.transform.position;
        }

        public void Dispose()
        {
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
            _mode = SpectateMode.Free;
            _targetIndex = -1;

            if (_freeCamTarget == null)
            {
                _freeCamTarget = new GameObject($"FreeCamTarget_{_owner.netId}");
                _freeCamTarget.transform.position = _owner.transform.position;
            }
            else
            {
                _freeCamTarget.transform.position = _owner.transform.position;
            }

            if (_camera != null)
                _camera.Follow = _freeCamTarget.transform;

            OnTargetNameChanged?.Invoke("자유 시점", false);
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

            if (_camera != null)
                _camera.Follow = _owner.transform;
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

                if (target != null && !target.IsDead)
                {
                    FollowTarget(target);
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
                    FollowTarget(target);
                    return;
                }
                attemptCount++;
            }
        }

        private void FollowTarget(PlayerBase target)
        {
            if (target == null || _camera == null) return;
            _camera.Follow = target.transform;
            OnTargetNameChanged?.Invoke(target.playerName, target == _owner);
        }

    }
}
