using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PixelChroma
{
    public class SoundManager : MonoSingleton<SoundManager>
    {
        // ── 영속화 키 / 믹서 파라미터 ─────────────────────────────
        private const string PrefPrefix = "Sound.";
        private const float MinVolumeDb = -80f;   // 0(선형) → -80dB(무음)

        private static readonly AudioChannel[] AllChannels =
            { AudioChannel.Master, AudioChannel.Bgm, AudioChannel.Sfx };

        [Header("References")]
        [SerializeField] private SoundLibrarySO library;
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("SFX")]
        [Tooltip("3D 위치 효과음 동시 재생용 소스 풀 크기")]
        [SerializeField] private int sfx3DPoolSize = 12;

        // ── BGM (A/B 크로스페이드) ────────────────────────────────
        private AudioSource _activeBgm;   // 현재 재생 중
        private AudioSource _idleBgm;     // 다음 트랙 준비용
        private BgmType _currentBgm = BgmType.None;
        private AsyncOperationHandle<AudioClip> _currentBgmHandle;
        private bool _hasBgmHandle;
        private CancellationTokenSource _bgmCts;

        // ── SFX ───────────────────────────────────────────────────
        private AudioSource _sfx2D;       // 2D one-shot(PlayOneShot로 겹침 처리)
        private AudioSource[] _sfx3DPool;
        private int _sfx3DIndex;

        // ── 볼륨 상태 ─────────────────────────────────────────────
        private readonly float[] _volumes = { 1f, 1f, 1f };
        private readonly bool[] _mutes = { false, false, false };



        // ==========================================================
        // Lifecycle
        // ==========================================================
        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            BuildAudioSources();
        }

        private void Start()
        {
            // AudioMixer.SetFloat은 믹서가 활성화된 다음 프레임(Start)부터 안정적으로 반영된다.
            LoadAndApplyVolumes();
        }

        protected virtual void OnDestroy()
        {
            if (Instance != this) return;
            CancelBgmTransition();
            ReleaseBgmHandle();
        }

        // ==========================================================
        // BGM
        // ==========================================================
        public async UniTask PlayBgm(BgmType type, float fade = 1f)
        {
            if (type == _currentBgm) return;   // 이미 재생 중이면 무시

            CancelBgmTransition();
            var cts = _bgmCts = new CancellationTokenSource();

            try
            {
                await TransitionBgm(type, fade, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 더 최근의 PlayBgm 요청으로 대체됨 — 정상 흐름.
            }
            finally
            {
                if (_bgmCts == cts)
                {
                    _bgmCts.Dispose();
                    _bgmCts = null;
                }
            }
        }

        // 새 BGM 전환 요청을 취소하고 현재 사용 중인 두 소스를 함께 Fade-out한다.
        // 로딩 팝업은 이 작업을 기다리지 않고 바로 표시하므로 전환 초반에만 이전 BGM이 짧게 남는다.
        public async UniTask StopBgm(float fade = 1f)
        {
            CancelBgmTransition();
            var cts = _bgmCts = new CancellationTokenSource();

            try
            {
                float activeStart = _activeBgm.volume;
                float idleStart = _idleBgm.volume;

                if (fade > 0f)
                {
                    float elapsed = 0f;
                    while (elapsed < fade)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float k = Mathf.Clamp01(elapsed / fade);
                        _activeBgm.volume = Mathf.Lerp(activeStart, 0f, k);
                        _idleBgm.volume = Mathf.Lerp(idleStart, 0f, k);
                        await UniTask.Yield(PlayerLoopTiming.Update, cts.Token);
                    }
                }

                _activeBgm.Stop();
                _activeBgm.clip = null;
                _activeBgm.volume = 0f;

                _idleBgm.Stop();
                _idleBgm.clip = null;
                _idleBgm.volume = 0f;

                ReleaseBgmHandle();
                _currentBgm = BgmType.None;
            }
            catch (OperationCanceledException)
            {
                // 로딩이 끝나 새 BGM 요청으로 대체됨 — 정상 흐름.
            }
            finally
            {
                if (_bgmCts == cts)
                {
                    _bgmCts.Dispose();
                    _bgmCts = null;
                }
            }
        }

        private async UniTask TransitionBgm(BgmType type, float fade, CancellationToken token)
        {
            AsyncOperationHandle<AudioClip> newHandle = default;
            bool hasNew = false;
            float targetVolume = 1f;

            try
            {
                // None이 아니면 새 트랙을 로드해 idle 소스에 준비한다.
                if (type != BgmType.None
                    && library != null
                    && library.TryGetBgm(type, out var entry)
                    && entry.clip != null
                    && entry.clip.RuntimeKeyIsValid())
                {
                    newHandle = Addressables.LoadAssetAsync<AudioClip>(entry.clip.RuntimeKey);
                    hasNew = true;
                    var clip = await newHandle.ToUniTask(cancellationToken: token);
                    targetVolume = entry.volume;

                    _idleBgm.clip = clip;
                    _idleBgm.volume = 0f;
                    _idleBgm.loop = true;
                    _idleBgm.Play();
                }

                // 현재 트랙 → 0, 새 트랙 → targetVolume 크로스페이드.
                await CrossFade(_activeBgm, _idleBgm, targetVolume, fade, token);
            }
            catch (OperationCanceledException)
            {
                if (hasNew)
                {
                    _idleBgm.Stop();
                    _idleBgm.clip = null;
                    Addressables.Release(newHandle);
                }
                throw;
            }

            // 커밋: 이전 트랙 정지·해제 후 active/idle 스왑.
            _activeBgm.Stop();
            _activeBgm.clip = null;
            ReleaseBgmHandle();

            (_activeBgm, _idleBgm) = (_idleBgm, _activeBgm);
            _currentBgmHandle = newHandle;
            _hasBgmHandle = hasNew;
            _currentBgm = type;
        }

        private async UniTask CrossFade(
            AudioSource from, AudioSource to, float toTarget, float duration, CancellationToken token)
        {
            if (duration <= 0f)
            {
                from.volume = 0f;
                to.volume = toTarget;
                return;
            }

            float fromStart = from.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // 씬 전환/일시정지(timeScale=0) 중에도 페이드가 진행되도록 unscaled 시간을 쓴다.
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                from.volume = Mathf.Lerp(fromStart, 0f, k);
                to.volume = Mathf.Lerp(0f, toTarget, k);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            from.volume = 0f;
            to.volume = toTarget;
        }

        // ==========================================================
        // SFX
        // ==========================================================
        // 2D 효과음(UI 클릭·팝업 등). PlayOneShot으로 겹쳐 재생된다.
        public void PlaySfx(SfxType type)
        {
            if (library == null || !library.TryGetSfx(type, out var entry) || entry.clip == null)
                return;

            _sfx2D.PlayOneShot(entry.clip, entry.volume);
        }

        // 3D 위치 효과음(페인트 튐·피격 등). 풀에서 라운드로빈으로 소스를 사용한다.
        public void PlaySfxAt(SfxType type, Vector3 position)
        {
            if (library == null || !library.TryGetSfx(type, out var entry) || entry.clip == null)
                return;

            var src = _sfx3DPool[_sfx3DIndex];
            _sfx3DIndex = (_sfx3DIndex + 1) % _sfx3DPool.Length;

            src.transform.position = position;
            src.clip = entry.clip;
            src.volume = entry.volume;
            src.Play();
        }

        // ==========================================================
        // Volume / Mute  (옵션 UI가 호출할 공개 API)
        // ==========================================================
        public void SetMasterVolume(float v01) => SetVolume(AudioChannel.Master, v01);
        public void SetBgmVolume(float v01) => SetVolume(AudioChannel.Bgm, v01);
        public void SetSfxVolume(float v01) => SetVolume(AudioChannel.Sfx, v01);

        public void SetVolume(AudioChannel channel, float v01)
        {
            v01 = Mathf.Clamp01(v01);
            _volumes[(int)channel] = v01;
            PlayerPrefs.SetFloat(VolumeKey(channel), v01);
            ApplyChannel(channel);
        }

        public void SetMute(AudioChannel channel, bool mute)
        {
            _mutes[(int)channel] = mute;
            PlayerPrefs.SetInt(MuteKey(channel), mute ? 1 : 0);
            ApplyChannel(channel);
        }

        public float GetVolume(AudioChannel channel) => _volumes[(int)channel];
        public bool IsMuted(AudioChannel channel) => _mutes[(int)channel];

        // ==========================================================
        // Internal
        // ==========================================================
        private void LoadAndApplyVolumes()
        {
            foreach (var channel in AllChannels)
            {
                _volumes[(int)channel] = PlayerPrefs.GetFloat(VolumeKey(channel), 1f);
                _mutes[(int)channel] = PlayerPrefs.GetInt(MuteKey(channel), 0) == 1;
                ApplyChannel(channel);
            }
        }

        private void ApplyChannel(AudioChannel channel)
        {
            if (mixer == null) return;

            // 음소거면 무음, 아니면 선형 볼륨을 dB로 변환(사람 귀에 자연스러운 로그 스케일).
            float linear = _mutes[(int)channel] ? 0f : _volumes[(int)channel];
            float dB = linear <= 0.0001f ? MinVolumeDb : Mathf.Log10(linear) * 20f;
            mixer.SetFloat(MixerParam(channel), dB);
        }

        private void BuildAudioSources()
        {
            _activeBgm = CreateSource("BGM_A", bgmGroup, loop: true, spatialBlend: 0f);
            _idleBgm = CreateSource("BGM_B", bgmGroup, loop: true, spatialBlend: 0f);

            _sfx2D = CreateSource("SFX_2D", sfxGroup, loop: false, spatialBlend: 0f);

            int size = Mathf.Max(1, sfx3DPoolSize);
            _sfx3DPool = new AudioSource[size];
            for (int i = 0; i < size; i++)
                _sfx3DPool[i] = CreateSource($"SFX_3D_{i}", sfxGroup, loop: false, spatialBlend: 1f);
        }

        private AudioSource CreateSource(string name, AudioMixerGroup group, bool loop, float spatialBlend)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = spatialBlend;
            src.outputAudioMixerGroup = group;
            return src;
        }

        private void CancelBgmTransition()
        {
            if (_bgmCts == null) return;
            _bgmCts.Cancel();
            _bgmCts.Dispose();
            _bgmCts = null;
        }

        private void ReleaseBgmHandle()
        {
            if (!_hasBgmHandle) return;
            Addressables.Release(_currentBgmHandle);
            _currentBgmHandle = default;
            _hasBgmHandle = false;
        }

        private static string MixerParam(AudioChannel channel) => channel switch
        {
            AudioChannel.Master => "MasterVolume",
            AudioChannel.Bgm => "BgmVolume",
            AudioChannel.Sfx => "SfxVolume",
            _ => "MasterVolume",
        };

        private static string VolumeKey(AudioChannel channel) => PrefPrefix + MixerParam(channel);
        private static string MuteKey(AudioChannel channel) => PrefPrefix + "Mute" + channel;

    }

}
