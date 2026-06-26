using UnityEngine;
using Mirror;

public class LobbyPlayerState : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnReadyStateChanged))]
    public bool isReady = false;

    public override void OnStartLocalPlayer()
    {
        if (LobbyUIManager.instance != null)
            LobbyUIManager.instance.localLobbyPlayer = this;
    }

    [Command]
    public void CmdToggleReady()
    {
        isReady = !isReady;
        Debug.Log("레디완료! 현재 상태 " + isReady);
    }

    private void OnReadyStateChanged(bool oldReady, bool newReady)
    {
        if (LobbyUIManager.instance != null)
            LobbyUIManager.instance.CheckAllReadyStates();
    }

    // [추가] 누군가 방에 처음 접속했을 때 (분모 증가)
    public override void OnStartClient()
    {
        if (LobbyUIManager.instance != null)
        {
            LobbyUIManager.instance.CheckAllReadyStates();
        }
    }

    // [추가] 누군가 방을 나갔을 때 (분모 감소)
    public override void OnStopClient()
    {
        if (LobbyUIManager.instance != null)
        {
            // 주의: 나가는 순간에는 오브젝트가 완전히 파괴되기 전이라 
            // 배열 길이에 포함될 수 있으므로 0.1초 뒤에 숫자를 다시 세도록 예약합니다.
            LobbyUIManager.instance.Invoke(nameof(LobbyUIManager.CheckAllReadyStates), 0.1f);
        }
    }
}