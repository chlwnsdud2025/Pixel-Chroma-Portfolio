using PixelChroma.UI;

namespace PixelChroma
{
    public class LobbyPlayer : PlayerBase
    {
        protected override void OnLocalPlayerStarted()
        {
            var lobby = UIManager.Instance.CurrentScene as UILobby;
            BindPaintHUD(lobby);
        }

        protected override void OnLocalPlayerStopped()
        {
            UnbindPaintHUD();
        }
    }
}
