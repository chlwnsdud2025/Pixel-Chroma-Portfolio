using UnityEngine;
using UnityEngine.UI; // Toggle, Slider 사용을 위해 추가
using Mirror;
using TMPro;

public class MenuUIManager : MonoBehaviour
{
    [Header("Offline Scene UI")]
    public SteamLobbyManager steamLobbyManager;
    public TMP_InputField codeInputField;

    [Header("Room Settings Panel")]
    public GameObject roomSettingsPanel;       // 설정 팝업 패널 전체
    public Toggle publicToggle;                // 공개/비공개 체크박스
    public Slider maxPlayerSlider;             // 인원수 조절 슬라이더
    public TextMeshProUGUI maxPlayerText;      // "현재 인원: 8명" 표시 텍스트

    private void Start()
    {
        // 시작 시 패널은 숨겨둡니다.
        if (roomSettingsPanel != null) roomSettingsPanel.SetActive(false);
    }

    // 1. 메인 화면의 '방 만들기' 버튼을 누르면 패널 열기
    public void OnClickOpenRoomSettings()
    {
        if (roomSettingsPanel != null)
        {
            roomSettingsPanel.SetActive(true);
            OnSliderValueChanged(); // 패널 열릴 때 텍스트 갱신
        }
    }

    // 2. 패널 닫기 (취소 버튼)
    public void OnClickCloseRoomSettings()
    {
        if (roomSettingsPanel != null) roomSettingsPanel.SetActive(false);
    }

    // 3. 슬라이더를 움직일 때마다 텍스트 갱신 (UI 슬라이더 이벤트에 연결할 함수)
    public void OnSliderValueChanged()
    {
        if (maxPlayerSlider != null && maxPlayerText != null)
        {
            maxPlayerText.text = $"Max Player: {maxPlayerSlider.value}";
        }
    }

    // 4. 패널 안의 '최종 생성(확인)' 버튼을 눌렀을 때
    public void OnConfirmCreateRoom()
    {
        // 토글과 슬라이더의 현재 값을 읽어옵니다.
        bool isPublic = publicToggle.isOn;
        int maxPlayers = (int)maxPlayerSlider.value;

        // 로비 매니저에게 방 생성을 지시합니다.
        steamLobbyManager?.HostLobby(isPublic, maxPlayers);

        // 방 생성이 시작되었으므로 패널을 닫습니다.
        roomSettingsPanel.SetActive(false);
    }

    // ==========================================
    // 기존에 있던 빠른 참가와 코드로 참가 로직은 그대로 둡니다.
    public void OnClickQuickMatch() { steamLobbyManager?.JoinQuickMatch(); }
    public void OnClickJoinByCode()
    {
        if (codeInputField != null && !string.IsNullOrEmpty(codeInputField.text))
            steamLobbyManager?.JoinLobbyByCode(codeInputField.text);
    }
}