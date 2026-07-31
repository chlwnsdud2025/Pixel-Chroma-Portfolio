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

    [Header("Spectate")]
    public LocalizedString spectateFreeView;

    [Header("Transition")]
    public LocalizedString transitionAllCaught;   // "전원 사망!"
    public LocalizedString transitionTimeUp;      // " 시간 종료!"
}