using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Linq;
using TMPro; // [추가] TextMeshPro 네임스페이스

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager instance;

    [Header("UI References")]
    public Button readyButton;
    public Button startGameButton;
    // [추가] 인원수를 표시할 텍스트 UI (예: 4 / 7)
    public TextMeshProUGUI readyCountText;

    [HideInInspector] public LobbyPlayerState localLobbyPlayer;

    private void Awake() { instance = this; }

    private void Start()
    {
        startGameButton.gameObject.SetActive(NetworkServer.active);
        startGameButton.interactable = false;

        // 시작할 때 텍스트 초기화
        UpdateReadyCountText(0, 0);
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