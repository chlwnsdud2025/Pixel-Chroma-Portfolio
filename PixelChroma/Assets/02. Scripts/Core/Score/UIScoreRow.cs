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

        private Texture2D _pixelTex;
        private Texture2D _exposeTex;

        public void SetData(ScoreEntry entry, bool isSelf)
        {
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

            // 도발 공개 픽셀
            if (exposePixelImage != null && entry.revealedColor.a > 0)
            {
                PlayExposeEffect(entry.revealedColor);
            }
        }

        private void PlayExposeEffect(Color32 color)
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
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_pixelTex != null) Object.Destroy(_pixelTex);
            if (_exposeTex != null) Object.Destroy(_exposeTex);
        }
    }
}