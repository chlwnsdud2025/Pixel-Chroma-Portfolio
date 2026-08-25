using System.Collections.Generic;
using Mirror;
using OneText.UGUI;
using UnityEngine;

namespace PixelChroma.UI
{
    public class UIScoreBoard : MonoBehaviour
    {
        [Header("Row")]
        [SerializeField] private UIScoreRow rowPrefab;
        [SerializeField] private Transform rowContainer;

        [Header("Header")]
        [SerializeField] private OneTextLabel refreshTimerText;

        [Header("Pool")]
        [SerializeField] private int initialPoolSize = 6;

        [Header("Visibility")]
        [SerializeField] private GameObject visualRoot;

        private readonly List<UIScoreRow> _pool = new();

        private int _refreshCountdown;
        // ── 초기화 ──

        public void Initialize()
        {
            _pool.Clear();

            // Container 안에 에디터에서 미리 배치해둔 UIScoreRow들을 풀에 등록
            foreach (Transform child in rowContainer)
            {
                var row = child.GetComponent<UIScoreRow>();
                if (row != null)
                {
                    row.ResetRow();
                    _pool.Add(row);
                }
            }
        }

        private void OnEnable()
        {
            NetworkClient.RegisterHandler<ScoreBoardMessage>(OnScoreBoardReceived);
            GameRoundManager.OnTimerTick += HandleTimerTick;
        }

        private void OnDisable()
        {
            NetworkClient.UnregisterHandler<ScoreBoardMessage>();
            GameRoundManager.OnTimerTick -= HandleTimerTick;
        }

        // ── 네트워크 수신 ──

        private void OnScoreBoardReceived(ScoreBoardMessage msg)
        {
            if (msg.refreshInterval > 0)                                 
                _refreshCountdown = Mathf.RoundToInt(msg.refreshInterval);
            RebuildRows(msg.entries);
        }

        // ── UI 구축 (오브젝트 풀링) ──

        private void RebuildRows(ScoreEntry[] entries)
        {
            uint myNetId = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0;

            // 풀이 부족하면 추가 생성
            while (_pool.Count < entries.Length)
            {
                var row = Instantiate(rowPrefab, rowContainer);
                row.ResetRow();
                _pool.Add(row);
            }

            // 데이터 바인딩
            for (int i = 0; i < entries.Length; i++)
            {
                _pool[i].gameObject.SetActive(true);
                _pool[i].SetData(entries[i], entries[i].netId == myNetId);
            }

            // 남는 행 비활성화 (풀에 반환)
            for (int i = entries.Length; i < _pool.Count; i++)
            {
                _pool[i].ResetRow();
            }
        }

        public void SetVisible(bool visible)
        {
            if (visualRoot != null) visualRoot.SetActive(visible);
        }

        private void HandleTimerTick(int remainingSeconds)
        {
            if (refreshTimerText != null)
                refreshTimerText.text = _refreshCountdown.ToString();

            _refreshCountdown--;
            if (_refreshCountdown < 0) _refreshCountdown = 0;
        }
    }
}