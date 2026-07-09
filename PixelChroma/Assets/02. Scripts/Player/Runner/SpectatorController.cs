using UnityEngine;
using Unity.Cinemachine;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

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
        private readonly RunnerPlayer _owner;
        private readonly CinemachineCamera _camera;
        private readonly float _freeCamSpeed;

        private SpectateMode _mode = SpectateMode.None;
        private GameObject _freeCamTarget;
        private int _targetIndex = -1;
        private UISpectatorPopup _popup;
        private bool _isLoadingPopup;

        public SpectateMode Mode => _mode;
        public bool IsSpectating => _mode != SpectateMode.None;

        public SpectatorController(RunnerPlayer owner, CinemachineCamera camera, float freeCamSpeed)
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
                Object.Destroy(_freeCamTarget);
        }

        public void EnterDeathSpectator()
        {
            EnterFreeMode();
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

            UpdatePopupAsync("자유 시점").Forget();
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

            if (_popup != null)
            {
                _popup.OnClickBack();
                _popup = null;
            }
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
            var runners = InGameManager.Instance?.Runners;
            if (runners == null || runners.Count == 0) return;

            int attemptCount = 0;
            while (attemptCount < runners.Count)
            {
                _targetIndex = (_targetIndex + direction + runners.Count) % runners.Count;
                var target = runners[_targetIndex];

                if (target != null && target != _owner && !target.IsDead)
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

            _targetIndex = (_targetIndex + direction + seekers.Count) % seekers.Count;
            var target = seekers[_targetIndex];
            if (target != null)
                FollowTarget(target);
        }

        private void FollowTarget(PlayerBase target)
        {
            if (target == null || _camera == null) return;
            _camera.Follow = target.transform;
            UpdatePopupAsync(target.playerName).Forget();
        }

        private async UniTaskVoid UpdatePopupAsync(string targetName)
        {
            if (_popup == null && !_isLoadingPopup)
            {
                _isLoadingPopup = true;
                _popup = await UIManager.Instance.ShowPopup<UISpectatorPopup>();
                _isLoadingPopup = false;
            }

            if (_popup != null)
                _popup.SetTargetName(targetName);
        }
    }
}
