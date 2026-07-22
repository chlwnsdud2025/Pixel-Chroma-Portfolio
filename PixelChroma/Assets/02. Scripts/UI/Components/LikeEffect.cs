using DG.Tweening;
using UnityEngine;

namespace PixelChroma
{
    public class LikeEffect : MonoBehaviour
    {
        [SerializeField] private RectTransform iconTransform;
        [SerializeField] private RectTransform textTransform;
        [SerializeField] private float duration = 0.4f;
        [SerializeField] private float strength = 0.3f;
        [SerializeField] private int vibrato = 6;
        [SerializeField] private float elasticity = 0.5f;

        public void Play()
        {
            Punch(iconTransform);
            Punch(textTransform);
        }

        private void Punch(RectTransform target)
        {
            if (target == null) return;
            target.DOKill();
            target.localScale = Vector3.one;
            target.DOPunchScale(Vector3.one * strength, duration, vibrato, elasticity);
        }
    }
}