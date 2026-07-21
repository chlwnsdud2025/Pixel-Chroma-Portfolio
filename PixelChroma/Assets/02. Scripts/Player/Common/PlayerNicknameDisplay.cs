using Mirror;
using TMPro;
using UnityEngine;

namespace PixelChroma
{
    public class PlayerNicknameDisplay
    {
        private readonly PlayerBase _owner;
        private readonly TMP_Text _text;
        private readonly GameObject _root;

        public PlayerNicknameDisplay(PlayerBase owner, TMP_Text text, GameObject root)
        {
            _owner = owner;
            _text = text;
            _root = root;
        }

        public void Refresh()
        {
            if (_text != null)
                _text.text = _owner.playerName;
        }

        public void HandlePaintModeChanged(bool isPaintMode)
        {
            if (_root == null) return;
            if (!_owner.isLocalPlayer) return;

            if (isPaintMode)
                _root.SetActive(false);
            else
                UpdateVisibility();
        }

        public void UpdateVisibility()
        {
            if (_root == null) return;
            if (NetworkClient.localPlayer == null) return;

            PlayerBase localPlayer = NetworkClient.localPlayer.GetComponent<PlayerBase>();
            if (localPlayer == null) return;

            if (_owner == localPlayer)
            {
                _root.SetActive(!_owner.IsPaintMode);
                return;
            }

            if (_owner is RunnerPlayer runner && runner.IsDead)
            {
                _root.SetActive(false);
                return;
            }

            if (localPlayer.role == PlayerRole.None || _owner.role == PlayerRole.None)
            {
                _root.SetActive(true);
                return;
            }

            if (GameRoundManager.Instance != null && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime)
            {
                _root.SetActive(true);
                return;
            }

            if (localPlayer.role == PlayerRole.Runner && _owner.role == PlayerRole.Runner)
            {
                var round = GameRoundManager.Instance;
                _root.SetActive(round?.CurrentRule == null || round.CurrentRule.CanSeeRunnerNickname(round.CurrentTimeState));
                return;
            }

            _root.SetActive(localPlayer.role == PlayerRole.Runner || _owner.role == PlayerRole.Seeker);
        }
    }
}
