using TMPro;
using UnityEngine;

namespace PixelChroma
{
    public class PlayerLikeDisplay
    {
        private readonly PlayerBase _owner;
        private readonly TMP_Text _countText;
        private readonly GameObject _root;

        private static bool _globalVisible = true;

        public static event System.Action OnGlobalVisibilityChanged;

        public PlayerLikeDisplay(PlayerBase owner, TMP_Text countText, GameObject root)
        {
            _owner = owner;
            _countText = countText;
            _root = root;
        }

        public static void ToggleGlobalVisibility()
        {
            _globalVisible = !_globalVisible;
            OnGlobalVisibilityChanged?.Invoke();
        }

        public void Refresh()
        {
            if (_countText != null)
                _countText.text = _owner.LikeCount.ToString();

            UpdateSelfVisibility();
        }

        public void UpdateSelfVisibility()
        {
            if (_root == null) return;
            _root.SetActive(_globalVisible);
        }
    }
}