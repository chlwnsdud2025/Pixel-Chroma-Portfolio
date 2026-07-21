using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "RoomSettingTextData", menuName = "PixelChroma/Localization/RoomSettingTextData")]
public class RoomSettingTextDataSO : ScriptableObject
{
    [Header("Button")]
    public LocalizedString createButtonLabel;
    public LocalizedString editButtonLabel;

    [Header("Warning")]
    public LocalizedString warningMaxLength;
    public LocalizedString warningBanned;
    public LocalizedString defaultRoomName;
}