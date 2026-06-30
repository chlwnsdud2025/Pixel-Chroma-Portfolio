using UnityEngine;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

namespace PixelChroma
{
    public class RunnerPlayer : PlayerBase
    {
        [Header("Runner References")]
        public SpriteRenderer spriteRenderer;

        private UIInGameHUD _hud;

        protected override void OnLocalPlayerStarted()
        {
            moveSpeed = 5f;
            ShowHUDAsync().Forget();
        }

        protected override void OnLocalPlayerStopped()
        {
            if (_hud != null && _hud.ColorPicker != null)
                _hud.ColorPicker.OnBrushSizeChanged -= OnUIBrushSizeChanged;

            ClearPaintEvents();
        }

        private async UniTaskVoid ShowHUDAsync()
        {
            _hud = await UIManager.Instance.ShowScene<UIInGameHUD>();
            if (_hud == null) return;

            OnPaintModeChanged += _hud.SetPaintMode;

            if (_hud.ColorPicker != null)
            {
                PaintColorProvider = () => _hud.ColorPicker.SelectedColor;
                MaxBrushSizeProvider = () => _hud.ColorPicker.MaxBrushSize > 0 ? _hud.ColorPicker.MaxBrushSize : 20;
                OnBrushSizeScrolled += _hud.ColorPicker.SetBrushSize;
                _hud.ColorPicker.OnBrushSizeChanged += OnUIBrushSizeChanged;

                if (paintCanvas != null)
                    paintCanvas.BrushSize = _hud.ColorPicker.BrushSize;
            }
        }

        private void OnUIBrushSizeChanged(int size)
        {
            if (paintCanvas != null)
                paintCanvas.BrushSize = size;
        }
    }
}
