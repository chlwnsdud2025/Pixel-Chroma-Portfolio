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

        private async UniTaskVoid ShowHUDAsync()
        {
            _hud = await UIManager.Instance.ShowScene<UIInGameHUD>();
        }

        protected override void Update()
        {
            if (!isLocalPlayer) return;

            HandleZoom();
            HandleMovement();
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
