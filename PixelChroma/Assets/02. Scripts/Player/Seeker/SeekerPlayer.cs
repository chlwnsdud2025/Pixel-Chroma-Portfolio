using UnityEngine;
using Mirror;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

namespace PixelChroma
{
    public class SeekerPlayer : PlayerBase
    {
        [Header("Seeker Basic Settings")]
        public float catchRadius = 1.5f;

        [Header("Indicator Settings")]
        [Tooltip("도망자를 가리킬 화살표 프리팹")]
        public GameObject indicatorPrefab;
        [Tooltip("술래 몸 중심으로부터 화살표가 띄워질 거리(반지름)")]
        public float indicatorRadius = 1.5f;

        private SeekerWaterGun _waterGun;
        private UIInGameHUD_Seeker _seekerHUD;
        private RunnerPlayer _targetRunnerToCatch;
        private bool _isCheckTimeSpectateAllowed;

        protected override UIInGameHUD InGameHUD => _seekerHUD;

        protected override void Awake()
        {
            base.Awake();
            _waterGun = GetComponent<SeekerWaterGun>();
        }

        protected override void Update()
        {
            base.Update();

            if (!isLocalPlayer) return;

            if (Spectator != null && Spectator.IsSpectating)
            {
                if (!IsChatActive)
                    HandleSpectateInput();
                return;
            }

            if (!IsChatActive)
            {
                HandleSpectateInput();
                CheckForCatchableRunner();
                HandleCatchInput();
                if (_waterGun != null)
                    _waterGun.LocalTick(IsPaintMode);
            }

            if (_seekerHUD != null && _waterGun != null)
            {
                _seekerHUD.UpdateWaterGunCooldown(_waterGun.CooldownFill01);
            }
        }

        protected override void OnLocalPlayerStarted()
        {
            PaintController.OnPaintModeChanged += HandleLocalPaintModeChanged;
            ShowHUDAsync().Forget();
            InitializeSpectator();
            GameRoundManager.OnTimeStateChanged += HandleTimeStateChanged;
        }

        protected override void OnLocalPlayerStopped()
        {
            PaintController.UnbindHUD();
            PaintController.OnPaintModeChanged -= HandleLocalPaintModeChanged;

            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChanged;
            CleanupSpectator();

            if (CursorManager.Instance != null)
                CursorManager.Instance.overrideScreenPosition = null;
        }

        protected override void HandleSpectateInput()
        {
            if (Spectator == null || !_isCheckTimeSpectateAllowed) return;

            if (Input.ToggleSpectate.WasPressedThisFrame())
            {
                if (Spectator.IsSpectating)
                {
                    Spectator.ExitSpectate();
                    _seekerHUD?.SetSpectatorMode(false);
                }
                else
                {
                    if (PaintController.IsPaintMode)
                        PaintController.TogglePaintMode();

                    Spectator.EnterRunnerMode();
                    _seekerHUD?.SetSpectatorMode(true);
                    _seekerHUD?.SetActiveSpectateMode(SpectateMode.Runner);
                }
                return;
            }

            base.HandleSpectateInput();
        }

        public override CursorType GetDefaultCursorType()
        {
            return CursorType.Crosshair;
        }

        [Command]
        public void CmdExecuteCatch(RunnerPlayer target)
        {
            if (target == null || target.IsDead)
                return;

            if (target.IsCamouflaged && !target.IsWet)
                return;

            float dist = Vector2.Distance(transform.position, target.transform.position);
            if (dist <= catchRadius + 0.5f)
            {
                target.ServerNotifyCaught();
            }
        }

        [ClientRpc]
        public void RpcTeleport(Vector3 newPosition)
        {
            transform.position = newPosition;

            if (Rb != null)
            {
                Rb.linearVelocity = Vector2.zero;
            }

            if (cinemachineCamera != null)
            {
                cinemachineCamera.PreviousStateIsValid = false;
            }

            Debug.Log("[Seeker] 본진으로 텔레포트 되었습니다!");
        }

        [ClientRpc]
        public void TargetShowRunnerIndicator(NetworkIdentity runnerIdentity, float duration)
        {
            if (runnerIdentity == null || indicatorPrefab == null) 
                return;

            GameObject arrowObj = Instantiate(indicatorPrefab, transform.position, Quaternion.identity);
            if (arrowObj.TryGetComponent(out IndicatorArrow arrow))
            {
                arrow.Init(transform, runnerIdentity.transform, indicatorRadius, duration);
            }
            else
            {
                Debug.LogWarning("[Seeker] indicatorPrefab에 IndicatorArrow 컴포넌트가 없습니다. 추적 없이 시간 경과 후 제거합니다.");
                Destroy(arrowObj, duration);
            }
        }

        private void CheckForCatchableRunner()
        {
            _targetRunnerToCatch = null;
            float closestDist = catchRadius;

            if (InGameManager.Instance == null) 
                return;

            foreach (var runner in InGameManager.Instance.Runners)
            {
                if (runner != null && !runner.IsDead && (!runner.IsCamouflaged || runner.IsWet))
                {
                    float dist = Vector2.Distance(transform.position, runner.transform.position);
                    if (dist <= closestDist)
                    {
                        closestDist = dist;
                        _targetRunnerToCatch = runner;
                    }
                }
            }

            if (_seekerHUD != null)
                _seekerHUD.SetCatchIconActive(_targetRunnerToCatch != null);
        }

        private void HandleCatchInput()
        {
            if (Input.Catch.WasPressedThisFrame() && _targetRunnerToCatch != null)
            {
                ExecuteCatch();
            }
        }

        private void ExecuteCatch()
        {
            if (_targetRunnerToCatch != null)
            {
                CmdExecuteCatch(_targetRunnerToCatch);
                _targetRunnerToCatch = null;
            }
        }

        private async UniTaskVoid ShowHUDAsync()
        {
            _seekerHUD = await UIManager.Instance.ShowScene<UIInGameHUD_Seeker>();
            PaintController.BindHUD(_seekerHUD);

            if (_waterGun != null)
                _seekerHUD.SetWaterGunCooldownDuration(_waterGun.FireCooldownDuration);

            _seekerHUD.SetSpectateKeyHintVisible(false);
        }

        private void HandleLocalPaintModeChanged(bool isPaint)
        {
            if (_waterGun != null)
                _waterGun.SetWeaponVisible(!isPaint);
        }

        private void HandleTimeStateChanged(TimeState state, int remainingSeconds)
        {
            if (Spectator == null) return;

            bool isRunnerWinReview = state == TimeState.CheckTime
                && GameRoundManager.Instance != null
                && GameRoundManager.Instance.CurrentWinner == PlayerRole.Runner;

            if (isRunnerWinReview)
            {
                _isCheckTimeSpectateAllowed = true;
                _seekerHUD?.SetSpectateKeyHintVisible(true);

                if (!Spectator.IsSpectating)
                {
                    if (PaintController.IsPaintMode)
                        PaintController.TogglePaintMode();

                    Spectator.EnterRunnerMode();
                    _seekerHUD?.SetSpectatorMode(true);
                    _seekerHUD?.SetActiveSpectateMode(SpectateMode.Runner);
                }
            }
            else
            {
                _isCheckTimeSpectateAllowed = false;
                _seekerHUD?.SetSpectateKeyHintVisible(false);

                if (Spectator.IsSpectating)
                {
                    Spectator.ExitSpectate();
                    _seekerHUD?.SetSpectatorMode(false);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, catchRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, indicatorRadius);
        }
    }
}
