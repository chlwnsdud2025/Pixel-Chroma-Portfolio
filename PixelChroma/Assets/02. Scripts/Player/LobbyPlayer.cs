using UnityEngine;
using PixelChroma.UI;

namespace PixelChroma
{
    public class LobbyPlayer : PlayerBase
    {
        private UILobby _lobby;

        protected override void OnLocalPlayerStarted()
        {
            _lobby = UIManager.Instance.CurrentScene as UILobby;
            if (_lobby == null) return;

            OnPaintModeChanged += _lobby.SetPaintMode;

            if (_lobby.ColorPicker != null)
            {
                PaintColorProvider = () => _lobby.ColorPicker.SelectedColor;
                MaxBrushSizeProvider = () => _lobby.ColorPicker.MaxBrushSize > 0 ? _lobby.ColorPicker.MaxBrushSize : 20;
                OnBrushSizeScrolled += _lobby.ColorPicker.SetBrushSize;
                OnEyedropperStateChanged += _lobby.ColorPicker.SetEyedropperActive;
                OnEyedropperPreview += _lobby.ColorPicker.SetEyedropperPreview;
                OnEyedropperPicked += _lobby.ColorPicker.SetColor;
                _lobby.ColorPicker.OnBrushSizeChanged += OnUIBrushSizeChanged;

                if (paintCanvas != null)
                    paintCanvas.BrushSize = _lobby.ColorPicker.BrushSize;
            }
        }

        protected override void OnLocalPlayerStopped()
        {
            if (_lobby != null && _lobby.ColorPicker != null)
                _lobby.ColorPicker.OnBrushSizeChanged -= OnUIBrushSizeChanged;

            ClearPaintEvents();
        }

        private void OnUIBrushSizeChanged(int size)
        {
            if (paintCanvas != null)
                paintCanvas.BrushSize = size;
        }
    }
}
