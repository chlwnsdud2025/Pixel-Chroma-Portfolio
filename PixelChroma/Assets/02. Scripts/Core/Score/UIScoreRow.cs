using DG.Tweening;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIScoreRow : MonoBehaviour
    {
        [SerializeField] private OneTextLabel rankText;
        [SerializeField] private OneTextLabel nameText;
        [SerializeField] private OneTextLabel scoreText;
        [SerializeField] private RawImage pixelNicknameImage;
        [SerializeField] private Image backgroundImage;

        [Header("Color")]
        [SerializeField] private Color normalColor = new(0.12f, 0.12f, 0.15f, 0.8f);
        [SerializeField] private Color selfColor = new(0.2f, 0.4f, 0.8f, 0.4f);
        [SerializeField] private Color deadColor = new(0.4f, 0.1f, 0.1f, 0.6f);

        [Header("Expose Pixel")]
        [SerializeField] private RectTransform exposePixelRoot;   
        [SerializeField] private RawImage exposePixelImage;
        [SerializeField] private float exposeDisplayDuration = 3f;

        [Header("Glow")]
        [SerializeField] private Image glowOverlay;

        private Texture2D _pixelTex;
        private Texture2D _exposeTex;
        private uint _currentNetId;
        private RectTransform _rt;
        private Material _glowMat;
        public uint CurrentNetId => _currentNetId;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            if (glowOverlay != null) EnsureGlowMat();
        }


        public void SetData(ScoreEntry entry, bool isSelf)
        {
            _currentNetId = entry.netId;

            rankText.text = $"#{entry.rank}";
            nameText.text = entry.playerName;
            scoreText.text = entry.score.ToString("N0");

            // 그림 닉네임 적용
            if (pixelNicknameImage != null)
            {
                if (entry.pixelNicknamePng != null && entry.pixelNicknamePng.Length > 0)
                {
                    _pixelTex = PixelNicknameHelper.ApplyPng(pixelNicknameImage, entry.pixelNicknamePng, _pixelTex);
                    pixelNicknameImage.gameObject.SetActive(true);
                    if (nameText != null) nameText.gameObject.SetActive(false);
                }
                else
                {
                    pixelNicknameImage.gameObject.SetActive(false);
                    if (nameText != null) nameText.gameObject.SetActive(true);
                }
            }

            // 배경색: 사망 > 본인 > 일반
            if (backgroundImage != null)
            {
                if (entry.isDead)
                    backgroundImage.color = deadColor;
                else if (isSelf)
                    backgroundImage.color = selfColor;
                else
                    backgroundImage.color = normalColor;
            }

            if (rankText != null)
            {
                if (entry.isDead)
                    rankText.color = deadColor;
                else if (isSelf)
                    rankText.color = selfColor;
                else
                    rankText.color = normalColor;
            }
        }

        public void PlayExposeEffect(Color32 color)
        {
            if (_exposeTex == null)
                _exposeTex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point };

            _exposeTex.SetPixel(0, 0, color);
            _exposeTex.Apply();
            exposePixelImage.texture = _exposeTex;
            exposePixelRoot.gameObject.SetActive(true);

            exposePixelRoot.DOKill();
            exposePixelRoot.localScale = Vector3.zero;

            var seq = DOTween.Sequence().SetTarget(exposePixelRoot);

            seq.Append(exposePixelRoot.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 1.3f));
            seq.Append(exposePixelRoot.DOPunchPosition(new Vector3(5f, 0f, 0f), 0.3f, 10, 0.5f));
            seq.AppendInterval(exposeDisplayDuration);
            seq.Append(exposePixelRoot.DOScale(0f, 0.2f).SetEase(Ease.InBack));
            seq.OnComplete(() => exposePixelRoot.gameObject.SetActive(false));
        }

        public void ResetRow()
        {
            if (rankText != null) rankText.text = "";
            if (nameText != null) nameText.text = "";
            if (scoreText != null) scoreText.text = "";
            if (pixelNicknameImage != null) pixelNicknameImage.gameObject.SetActive(false);
            if (nameText != null) nameText.gameObject.SetActive(true);
            if (exposePixelRoot != null)
            {
                exposePixelRoot.DOKill();
                exposePixelRoot.gameObject.SetActive(false);
            }

            // 애니메이션 정리
            if (_rt != null) _rt.DOKill();
            if (scoreText != null) DOTween.Kill(scoreText);
            DOTween.Kill(this);  
            if (glowOverlay != null)
            {
                DOTween.Kill(glowOverlay);
                if (_glowMat != null)
                {
                    _glowMat.SetFloat("_GlowIntensity", 0f);
                    _glowMat.SetFloat("_SweepProgress", -1.5f);
                }
            }

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_pixelTex != null) Object.Destroy(_pixelTex);
            if (_exposeTex != null) Object.Destroy(_exposeTex);
            if (_glowMat != null) Object.Destroy(_glowMat);
        }

        public void ApplyDeadColor()
        {
            if (backgroundImage != null)
                backgroundImage.color = deadColor;
            if (rankText != null)
                rankText.color = deadColor;
        }


        //-----------------------Tween + Shader 효과-----------------------------------------
        public void PlaySweep(float duration, System.Action onMidPoint = null)
        {
            if (glowOverlay == null) return;

            EnsureGlowMat();

            // 시작 위치로 초기화
            _glowMat.SetFloat("_SweepProgress", -0.5f);

            DOTween.Kill(this);   // 이전 스윕 정리 (this를 타겟으로)

            var seq = DOTween.Sequence().SetTarget(this);

            // 좌→우 스윕: -0.5 → 2
            seq.Append(
                DOVirtual.Float(-0.5f, 3f, duration, v =>
                    _glowMat.SetFloat("_SweepProgress", v))
                   .SetEase(Ease.InOutCubic)
            );

            // 스윕이 45% 지점 도달 시 콜백 (데이터 갱신 타이밍)
            if (onMidPoint != null)
            {
                seq.InsertCallback(duration * 0.45f, () => onMidPoint());
            }
        }

        public void AnimateScoreCounter(int fromScore, int toScore, float duration)
        {
            DOTween.Kill(scoreText);

            DOVirtual.Int(fromScore, toScore, duration, value =>
            {
                if (scoreText != null)
                    scoreText.text = value.ToString("N0");
            })
            .SetTarget(scoreText)
            .SetEase(Ease.OutCubic);
        }

        public void PlayGlowPulse(bool isPromotion)
        {
            if (glowOverlay == null) return;

            EnsureGlowMat();

            // 색상 설정
            Color glowColor = isPromotion
                ? new Color(0.3f, 0.6f, 1f, 1f)     // 파란색 (순위 상승)
                : new Color(1f, 0.3f, 0.25f, 1f);   // 빨간색 (순위 하락)

            _glowMat.SetColor("_GlowColor", glowColor);

            // 0 → 1 → 0 펄스
            DOTween.Kill(glowOverlay);

            var seq = DOTween.Sequence().SetTarget(glowOverlay);
            seq.Append(
                DOVirtual.Float(0f, 1f, 0.15f, v =>
                    _glowMat.SetFloat("_GlowIntensity", v))
                   .SetEase(Ease.OutQuad)
            );
            seq.Append(
                DOVirtual.Float(1f, 0f, 0.5f, v =>
                    _glowMat.SetFloat("_GlowIntensity", v))
                   .SetEase(Ease.InQuad)
            );
        }

        public void AnimateRankBadge(int oldRank, int newRank)
        {
            if (rankText == null) return;

            var rankRt = rankText.GetComponent<RectTransform>();
            DOTween.Kill(rankRt);

            var seq = DOTween.Sequence().SetTarget(rankRt);

            // 현재 크기에서 1.4배로 팝
            seq.Append(rankRt.DOScale(1.4f, 0.12f).SetEase(Ease.OutQuad));

            // 팝 꼭대기에서 텍스트 교체
            seq.AppendCallback(() => rankText.text = $"#{newRank}");

            // 바운스하며 원래 크기로
            seq.Append(rankRt.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 2f));
        }

        private void EnsureGlowMat()
        {
            if (_glowMat == null)
            {
                _glowMat = new Material(glowOverlay.material);
                glowOverlay.material = _glowMat;
            }
        }
    }
}