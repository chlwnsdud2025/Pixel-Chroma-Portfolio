using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIRoomListItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private TMP_Text gameModeText;
        [SerializeField] private TMP_Text mapText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button joinButton;

        private RoomListEntry _entry;

        public event Action<RoomListEntry> OnJoinClicked;

        private void Awake()
        {
            if (joinButton != null)
                joinButton.onClick.AddListener(() => OnJoinClicked?.Invoke(_entry));
        }

        public void Setup(RoomListEntry entry)
        {
            _entry = entry;

            if (roomNameText != null) roomNameText.text = entry.roomName;
            if (playerCountText != null) playerCountText.text = $"{entry.currentPlayers}/{entry.maxPlayers}";
            if (gameModeText != null) gameModeText.text = entry.gameMode.ToString();

            if (mapText != null)
            {
                string mapDisplay = "-";
                if (!string.IsNullOrEmpty(entry.mapId))
                {
                    var registry = LobbyManager.Instance != null ? LobbyManager.Instance.MapRegistry : null;
                    var mapData = registry != null ? registry.FindById(entry.mapId) : null;
                    mapDisplay = mapData != null && !string.IsNullOrEmpty(mapData.displayName)
                        ? mapData.displayName
                        : entry.mapId;
                }
                mapText.text = mapDisplay;
            }

            if (statusText != null) statusText.text = entry.IsJoinable ? "대기 중" : "게임 중";
            statusText.color = entry.IsJoinable ? Color.green : Color.red;
            if (joinButton != null) joinButton.interactable = entry.IsJoinable;
        }

        public void SetInteractable(bool interactable)
        {
            if (joinButton != null) joinButton.interactable = interactable && _entry.IsJoinable;
        }
    }
}