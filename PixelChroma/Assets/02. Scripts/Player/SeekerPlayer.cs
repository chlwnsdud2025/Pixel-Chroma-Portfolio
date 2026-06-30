using UnityEngine;
using Mirror;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

namespace PixelChroma
{
    public class SeekerPlayer : PlayerBase
    {
        [Header("Seeker Settings")]
        public float catchRadius = 1.5f;

        private UIInGameHUD _hud;

        protected override void OnLocalPlayerStarted()
        {
            moveSpeed = 4f;
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

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isServer) return;

            var runner = other.GetComponent<RunnerPlayer>();
            if (runner != null && runner.role == PlayerRole.Runner)
            {
                RpcOnRunnerCaught(runner.netId);
            }
        }

        [ClientRpc]
        private void RpcOnRunnerCaught(uint runnerNetId)
        {
            Debug.Log($"[Seeker] Runner(netId:{runnerNetId}) caught!");
        }
    }
}
