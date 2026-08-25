using Mirror;
using PixelChroma;
using PixelChroma.UI;
using System;
using UnityEngine;

public class LobbyPlayer : PlayerBase
{
    [Header("Lobby Ready UI")]
    [Tooltip("레디 시 켜질 체크마크 이미지 오브젝트를 연결해주세요.")]
    [SerializeField] private GameObject readyCheckObject;

    [Header("Rank Icons")]
    [SerializeField] private GameObject goldMedalObject;    // 1등 금메달
    [SerializeField] private GameObject silverMedalObject;  // 2등 은메달
    [SerializeField] private GameObject bronzeMedalObject;  // 3등 동메달
    [SerializeField] private GameObject poopObject;         // 꼴지 💩

    private LobbyPlayerState _lobbyState;

    private static event Action<RankMessage> _onRankReceived;

    protected override void Awake()
    {
        base.Awake();
        _lobbyState = GetComponent<LobbyPlayerState>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        LobbyPlayerState.OnReadyChanged += UpdateReadyImage;
        _onRankReceived += HandleRank;

        NetworkClient.RegisterHandler<RankMessage>(OnRankMessage);

        UpdateReadyImage();
        HideAllRankIcons();
    }

    public override void OnStopClient()
    {
        LobbyPlayerState.OnReadyChanged -= UpdateReadyImage;
        _onRankReceived -= HandleRank;
        base.OnStopClient();
    }

    protected override void OnLocalPlayerStarted()
    {
        var lobby = UIManager.Instance.GetPanel<UILobby>();
        PaintController.BindHUD(lobby);
    }

    protected override void OnLocalPlayerStopped()
    {
        PaintController.UnbindHUD();
    }

    private void UpdateReadyImage()
    {
        if (readyCheckObject != null && _lobbyState != null)
        {
            readyCheckObject.SetActive(_lobbyState.isReady);
        }
    }

    private static void OnRankMessage(RankMessage msg)
    {
        _onRankReceived?.Invoke(msg);
    }

    private void HandleRank(RankMessage msg)
    {
        HideAllRankIcons();

        if (netId == msg.firstNetId && goldMedalObject != null)
            goldMedalObject.SetActive(true);
        else if (netId == msg.secondNetId && silverMedalObject != null)
            silverMedalObject.SetActive(true);
        else if (netId == msg.thirdNetId && bronzeMedalObject != null)
            bronzeMedalObject.SetActive(true);

        // 꼴지는 1~3등과 겹치지 않으므로 별도 if
        if (netId == msg.lastNetId && poopObject != null)
            poopObject.SetActive(true);
    }

    private void HideAllRankIcons()
    {
        if (goldMedalObject != null) goldMedalObject.SetActive(false);
        if (silverMedalObject != null) silverMedalObject.SetActive(false);
        if (bronzeMedalObject != null) bronzeMedalObject.SetActive(false);
        if (poopObject != null) poopObject.SetActive(false);
    }
}