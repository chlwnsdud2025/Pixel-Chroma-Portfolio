using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Linq;
using TMPro; // TextMeshPro를 사용하기 위해 필수!

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager instance;

    [Header("UI References")]
    public Button readyButton;
    public Button startGameButton;
    public TextMeshProUGUI readyCountText;

    // [신규] 방 코드를 화면에 띄워줄 텍스트 UI 변수 추가
    public TextMeshProUGUI roomCodeText;

    [HideInInspector] public LobbyPlayerState localLobbyPlayer;

    private void Awake() { instance = this; }

    private void Start()
    {
        startGameButton.gameObject.SetActive(NetworkServer.active);
        startGameButton.interactable = false;
        UpdateReadyCountText(0, 0);

        // [신규] 스팀 로비 데이터에서 "RoomCode"를 읽어와 UI 텍스트에 적용합니다.
        // 호스트와 클라이언트 모두 똑같이 로비 데이터를 읽을 수 있습니다!
        if (SteamLobbyManager.instance != null && roomCodeText != null)
        {
            // 스팀 매치메이킹 API를 통해 현재 방의 코드를 가져옵니다.
            string roomCode = Steamworks.SteamMatchmaking.GetLobbyData(SteamLobbyManager.instance.currentLobbyID, "RoomCode");

            // UI에 예쁘게 띄워줍니다.
            roomCodeText.text = $"입장 코드: {roomCode}";

            Debug.Log($"[LobbyUIManager] 화면에 입장 코드({roomCode})를 표시했습니다.");
        }
    }
    public void OnClickReady()
    {
        if (localLobbyPlayer != null)
        {
            localLobbyPlayer.CmdToggleReady();
            Debug.Log("레디완료! 현재 상태 ");
        }
    }

    public void OnClickStartGame()
    {
        if (NetworkServer.active)
        {
            ChromaNetworkManager manager = NetworkManager.singleton as ChromaNetworkManager;
            SteamLobbyManager.instance.ChangeLobbyStatusToInGame();
            manager?.StartGame();
        }
    }

    // [수정됨] 레디 상태 및 인원수 텍스트 갱신
    public void CheckAllReadyStates()
    {
        LobbyPlayerState[] allPlayers = FindObjectsByType<LobbyPlayerState>(FindObjectsSortMode.None);

        // 전체 인원과 레디한 인원 계산
        int totalPlayers = allPlayers.Length;
        int readyPlayers = allPlayers.Count(player => player.isReady);

        // 1. 텍스트 UI 업데이트 (예: "준비 완료: 4 / 7")
        UpdateReadyCountText(readyPlayers, totalPlayers);

        // 방에 아무도 없으면 에러 방지
        if (totalPlayers == 0) return;

        // 모든 사람이 레디했는지 확인
        bool isEveryoneReady = (readyPlayers == totalPlayers);

        // 방장 화면의 Start 버튼 제어
        if (NetworkServer.active)
        {
            startGameButton.interactable = isEveryoneReady;
        }
    }

    // 텍스트를 바꿔주는 내부 함수
    private void UpdateReadyCountText(int ready, int total)
    {
        if (readyCountText != null)
        {
            readyCountText.text = $"{ready} / {total}";
        }
    }
}