using TMPro;
using UnityEngine;

namespace PixelChroma
{
    [CreateAssetMenu(fileName = "DynamicTextStyle", menuName = "PixelChroma/Localization/DynamicTextStyle")]
    public class DynamicTextStyleSO : ScriptableObject
    {
        [Header("Face")]
        [SerializeField] private float _faceDilate;

        [Header("Outline")]
        [SerializeField] private bool _useOutline;
        [SerializeField] private float _outlineWidth;
        [SerializeField] private Color _outlineColor = Color.black;

        [Header("Underlay")]
        [SerializeField] private bool _useUnderlay;
        [SerializeField] private float _underlayOffsetX;
        [SerializeField] private float _underlayOffsetY;
        [SerializeField] private float _underlayDilate;
        [SerializeField] private float _underlaySoftness;
        [SerializeField] private Color _underlayColor = Color.black;

        private Material _cached;

        public Material GetOrCreateMaterial(TMP_FontAsset font)
        {
            if (_cached == null)
            {
                _cached = new Material(font.material);
                ApplyTo(_cached);
            }

            _cached.mainTexture = font.material.mainTexture;
            return _cached;
        }

        private void ApplyTo(Material mat)
        {
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, _faceDilate);

            if (_useOutline)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, _outlineWidth);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, _outlineColor);
            }

            if (_useUnderlay)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, _underlayOffsetX);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, _underlayOffsetY);
                mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, _underlayDilate);
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, _underlaySoftness);
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, _underlayColor);
            }
        }
    }
}
