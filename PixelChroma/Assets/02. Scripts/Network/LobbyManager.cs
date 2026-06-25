using Fusion;
using UnityEngine;

public class LobbyManager : NetworkBehaviour
{
    // 최대 16명까지의 레디 상태를 저장하는 네트워크 배열입니다.
    [Networked, Capacity(16)] private NetworkArray<NetworkBool> ReadyStates { get; }

    [Networked] private TickTimer StartTimer { get; set; }
    [Networked] private int ConnectedPlayerCount { get; set; }
    [Networked] private int ReadyPlayerCount { get; set; }

    // 임시 UI를 화면에 그려줍니다. (캐릭터 스폰 없이 이 UI만으로 상태를 통제합니다)
    private void OnGUI()
    {
        if (Runner == null) return;

        GUI.skin.label.fontSize = 30;
        GUI.skin.button.fontSize = 20;

        // 1. 현재 접속자 수 및 레디 상태 표시
        GUI.Label(new Rect(20, 20, 500, 50), $"Players: {ConnectedPlayerCount}");
        GUI.Label(new Rect(20, 70, 500, 50), $"Ready: {ReadyPlayerCount} / {ConnectedPlayerCount}");

        // 2. 카운트다운 표시
        if (StartTimer.IsRunning)
        {
            float time = StartTimer.RemainingTime(Runner) ?? 0;
            GUI.Label(new Rect(20, 120, 500, 50), $"Game starts in: {Mathf.Ceil(time)}s");
        }

        // 3. 레디 버튼 로직
        bool myReadyState = ReadyStates.Get(Runner.LocalPlayer.RawEncoded % 16);
        string btnText = myReadyState ? "CANCEL READY" : "READY";

        if (GUI.Button(new Rect(20, 200, 200, 60), btnText))
        {
            // 방장에게 내 레디 상태를 반전시켜달라고 요청 (RPC)
            RPC_SetReady(Runner.LocalPlayer, !myReadyState);
        }
    }

    // 클라이언트가 방장(Host)에게 호출하는 레디 신호 함수입니다.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetReady(PlayerRef player, NetworkBool isReady)
    {
        ReadyStates.Set(player.RawEncoded % 16, isReady);
    }

    public override void FixedUpdateNetwork()
    {
        // 아래 로직은 방장(서버)만 계산하여 모든 클라이언트에게 상태를 동기화합니다.
        if (!HasStateAuthority) return;

        // 1. 현재 방에 있는 실제 접속자 수 계산
        int currentCount = 0;
        foreach (var p in Runner.ActivePlayers) currentCount++;
        ConnectedPlayerCount = currentCount;

        // 2. 레디를 누른 사람 수 계산
        int readyCount = 0;
        foreach (var p in Runner.ActivePlayers)
        {
            if (ReadyStates.Get(p.RawEncoded % 16))
            {
                readyCount++;
            }
        }
        ReadyPlayerCount = readyCount;

        // 3. 게임 시작 조건 체크: 최소 1명 이상 접속 중이고, 모두가 레디했을 때
        if (ConnectedPlayerCount > 0 && ReadyPlayerCount == ConnectedPlayerCount)
        {
            if (!StartTimer.IsRunning)
            {
                StartTimer = TickTimer.CreateFromSeconds(Runner, 5f); // 5초 카운트다운 시작
            }
            else if (StartTimer.Expired(Runner))
            {
                StartTimer = TickTimer.None; // 씬 무한 로딩 방지를 위한 타이머 끄기

                // 전원 게임 씬(2번 인덱스)으로 이동!
                Runner.LoadScene(SceneRef.FromIndex(2));
            }
        }
        else
        {
            // 누군가 레디를 풀거나 나갔다면 즉시 카운트다운을 취소합니다.
            StartTimer = TickTimer.None;
        }
    }
}