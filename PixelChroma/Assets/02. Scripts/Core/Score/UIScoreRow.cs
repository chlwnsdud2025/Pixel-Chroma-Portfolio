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

        private Texture2D _pixelTex;

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
                    if (nameText != null) nameText.gameObject.SetActive(false); // 그림 닉네임이면 텍스트 숨기기
                }
                else
                {
                    pixelNicknameImage.gameObject.SetActive(false);
                    if (nameText != null) nameText.gameObject.SetActive(true);
                }
            }

            if (backgroundImage != null)
                backgroundImage.color = isSelf ? selfColor : normalColor;
        }

        // 풀 반환 시 초기화
        public void ResetRow()
        {
            if (rankText != null) rankText.text = "";
            if (nameText != null) nameText.text = "";
            if (scoreText != null) scoreText.text = "";
            if (pixelNicknameImage != null) pixelNicknameImage.gameObject.SetActive(false);
            if (nameText != null) nameText.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_pixelTex != null)
                Object.Destroy(_pixelTex);
        }
    }
}