using UnityEngine;
using Cysharp.Threading.Tasks;
using PixelChroma.UI;

namespace PixelChroma
{
    public class RunnerPlayer : PlayerBase
    {
        [Header("Runner References")]
        public SpriteRenderer spriteRenderer;

        protected override void OnLocalPlayerStarted()
        {
            moveSpeed = 5f;
            ShowHUDAsync().Forget();
        }

        protected override void OnLocalPlayerStopped()
        {
            UnbindPaintHUD();
        }

        private async UniTaskVoid ShowHUDAsync()
        {
            var hud = await UIManager.Instance.ShowScene<UIInGameHUD>();
            BindPaintHUD(hud);
        }
    }
}
