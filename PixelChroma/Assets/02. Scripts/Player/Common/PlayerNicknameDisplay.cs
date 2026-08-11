using Mirror;
using OneText.UGUI;
using UnityEngine;

namespace PixelChroma
{
    public class PlayerNicknameDisplay
    {
        private readonly PlayerBase _owner;
        private readonly OneTextLabel _text;
        private readonly GameObject _root;
        private readonly OneTextLabel _likeCountText;

        public PlayerNicknameDisplay(PlayerBase owner, OneTextLabel text, GameObject root,
            OneTextLabel likeCountText)
        {
            _owner = owner;
            _text = text;
            _root = root;
            _likeCountText = likeCountText;
        }

        public void Refresh()
        {
            if (_text != null)
                _text.text = _owner.playerName;

            RefreshLikeCount();
        }

        // 좋아요 카운트는 닉네임 UI의 일부라 별도 표시 규칙이 없습니다. 여기서는 값만 갱신합니다.
        public void RefreshLikeCount()
        {
            if (_likeCountText != null)
                _likeCountText.text = _owner.LikeCount.ToString();
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

            if (_owner is RunnerPlayer runner && runner.IsDead)
            {
                bool isCheckTime = GameRoundManager.Instance != null
                    && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime;
                if (!isCheckTime)
                {
                    _root.SetActive(false);
                    return;
                }
            }


            if (_owner == localPlayer)
            {
                _root.SetActive(!_owner.IsPaintMode);
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
