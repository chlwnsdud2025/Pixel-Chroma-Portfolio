using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "HUDTextData", menuName = "PixelChroma/Localization/HUDTextData")]
public class HUDTextDataSO : ScriptableObject
{
    [Header("State")]
    public LocalizedString stateWaiting;
    public LocalizedString stateHideTime;
    public LocalizedString stateSeekTime;
    public LocalizedString stateCheckTime;
    public LocalizedString stateGameEnd;

    [Header("Result")]
    public LocalizedString resultSeekerWin;
    public LocalizedString resultRunnerWin;
}