using UnityEngine;
using Mirror;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

namespace PixelChroma
{
    public class SeekerPlayer : PlayerBase
    {
 
        [Header("Indicator Settings")]
        [Tooltip("도망자를 가리킬 화살표 프리팹")]
        public GameObject indicatorPrefab;
        [Tooltip("술래 몸 중심으로부터 화살표가 띄워질 거리(반지름)")]
        public float indicatorRadius = 1.5f;

        private SeekerWaterGun _waterGun;
        private UIInGameHUD_Seeker _seekerHUD;
        

        protected override UIInGameHUD InGameHUD => _seekerHUD;
        protected override bool CanUseFormWheel => false;   // Seeker는 변신 불가

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

            if (!IsChatActive && !IsPopupOpen)
            {
                HandleSpectateInput();
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
        }

        protected override void OnLocalPlayerStopped()
        {
            PaintController.UnbindHUD();
            PaintController.OnPaintModeChanged -= HandleLocalPaintModeChanged;

            CleanupSpectator();

            if (CursorManager.Instance != null)
                CursorManager.Instance.overrideScreenPosition = null;
        }

        // Seeker의 관전은 체크타임 잠금 전용 → ESC로 종료하지 않는다.
        // (해제는 OnCheckTimeSpectatorExit에서만 일어남)
        protected override bool TryExitSpectateByEscape()
        {
            if (Spectator == null || !Spectator.IsSpectating) return false;

            Spectator.ExitSpectate();
            _seekerHUD?.SetSpectatorMode(false);
            return true;
        }

        protected override void HandleSpectateInput()
        {
            if (Spectator == null || !IsCheckTimeSpectatorLocked) return;

            if (Input.ToggleSpectate.WasPressedThisFrame())
            {
                if (Spectator.IsSpectating)
                {
                    // 관전 중 → 관전 종료 (자기 캐릭터로 복귀)
                    Spectator.ExitSpectate();
                    _seekerHUD?.SetSpectatorMode(false);
                }
                else
                {
                    // 관전 아닌 상태 → 다시 관전 진입
                    Spectator.EnterRunnerMode();
                    _seekerHUD?.SetSpectatorMode(true);
                    _seekerHUD?.SetActiveSpectateMode(SpectateMode.Runner);
                }
                return;
            }

            base.HandleSpectateInput();
        }

        protected override void OnCheckTimeSpectatorEnter()
        {
            _seekerHUD?.SetSpectateKeyHintVisible(true);
        }

        protected override void OnCheckTimeSpectatorExit()
        {
            _seekerHUD?.SetSpectateKeyHintVisible(false);

            if (Spectator != null && Spectator.IsSpectating)
            {
                Spectator.ExitSpectate();
                _seekerHUD?.SetSpectatorMode(false);
            }
        }

        public override CursorType GetDefaultCursorType()
        {
            return CursorType.Crosshair;
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, indicatorRadius);
        }
    }
}
