using UnityEngine;
using TMPro;
using DG.Tweening;

namespace PixelChroma.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public class UIPunchText : MonoBehaviour
    {
        [SerializeField] private float punchScale = 0.2f;
        [SerializeField] private float duration = 0.2f;

        private TMP_Text _text;
        private Vector3 _baseScale;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _baseScale = transform.localScale;
        }

        public void SetText(string value)
        {
            if (_text.text == value) return;
            _text.text = value;
            Punch();
        }

        private void Punch()
        {
            transform.DOKill();
            transform.localScale = _baseScale;
            transform.DOPunchScale(_baseScale * punchScale, duration);
        }
    }
}
