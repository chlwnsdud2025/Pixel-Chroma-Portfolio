using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIRoomOptionPanel : MonoBehaviour
    {
        [Header("Registry")]
        [SerializeField] private GameModeRegistrySO gameModeRegistry;
        [SerializeField] private MapRegistrySO mapRegistry;

        [Header("Game Mode")]
        [SerializeField] private TMP_Text gameModeText;
        [SerializeField] private TMP_Text gameModeDescriptionText;

        [Header("Map")]
        [SerializeField] private TMP_Text mapNameText;
        [SerializeField] private Image mapPreviewImage;

        [Header("Time Settings")]
        [SerializeField] private TMP_Text hideTimeText;
        [SerializeField] private TMP_Text seekTimeText;
        [SerializeField] private TMP_Text checkTimeText;
        [SerializeField] private TMP_Text forcedExposeIntervalText;

        public void Refresh(RoomOption option)
        {
            if (option == null) return;

            RefreshGameMode(option);
            RefreshMapName(option);
            RefreshTimeSettings(option);
        }

        private void RefreshGameMode(RoomOption option)
        {
            var data = gameModeRegistry != null ? gameModeRegistry.Find(option.gameMode) : null;

            if (gameModeText != null)
            {
                gameModeText.text = data != null && !data.displayName.IsEmpty
                    ? data.displayName.GetLocalizedString()
                    : option.gameMode.ToString();
            }

            if (gameModeDescriptionText != null)
            {
                gameModeDescriptionText.text = data != null && !data.description.IsEmpty
                    ? data.description.GetLocalizedString()
                    : string.Empty;
            }
        }

        private void RefreshMapName(RoomOption option)
        {
            var mapData = mapRegistry != null ? mapRegistry.FindById(option.mapName) : null;

            if (mapNameText != null)
            {
                mapNameText.text = mapData != null
                    ? mapData.GetDisplayName()
                    : option.mapName;
            }

            if (mapPreviewImage != null)
            {
                mapPreviewImage.sprite = mapData != null ? mapData.previewSprite : null;
                mapPreviewImage.enabled = mapPreviewImage.sprite != null;
            }
        }

        private void RefreshTimeSettings(RoomOption option)
        {
            if (hideTimeText != null) hideTimeText.text = option.hideTime.ToString("0");
            if (seekTimeText != null) seekTimeText.text = option.seekTime.ToString("0");
            if (checkTimeText != null) checkTimeText.text = option.checkTime.ToString("0");
            if (forcedExposeIntervalText != null) forcedExposeIntervalText.text = option.forcedExposeInterval.ToString("0");
        }
    }
}
