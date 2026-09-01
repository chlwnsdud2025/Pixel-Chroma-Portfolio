using Unity.Cinemachine;
using UnityEngine;

namespace PixelChroma
{
    // TimeState 전환 시 아주 약한 화면 흔들림을 발생시키는 순수 클라이언트 컴포넌트입니다.
    // CinemachineCamera 프리팹에 부착되어 모든 맵 씬에서 자동으로 동작합니다.
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class TimeStateShake : MonoBehaviour
    {
        [Header("Shake Strength")]
        [Tooltip("술래 시간 시작 시 흔들림 세기. 피격(1.0) 대비 아주 약하게 잡습니다.")]
        [SerializeField] private float seekTimeStrength = 0.3f;
        [Tooltip("라운드 종료 시 흔들림 세기. 조준을 방해할 사람이 없으므로 조금 더 셉니다.")]
        [SerializeField] private float checkTimeStrength = 0.5f;

        private CinemachineImpulseSource _impulseSource;

        private void Awake()
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        private void OnEnable()
        {
            GameRoundManager.OnTimeStateChanged += HandleTimeStateChanged;
        }

        private void OnDisable()
        {
            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChanged;
        }

        private void HandleTimeStateChanged(TimeState state, int remainingSeconds)
        {
            if (_impulseSource == null || !GameSettings.ScreenShakeEnabled) return;

            float strength = state switch
            {
                TimeState.SeekTime => seekTimeStrength,
                TimeState.CheckTime => checkTimeStrength,
                _ => 0f,
            };

            if (strength <= 0f) return;

            _impulseSource.GenerateImpulse(Vector3.down * strength);
        }
    }
}