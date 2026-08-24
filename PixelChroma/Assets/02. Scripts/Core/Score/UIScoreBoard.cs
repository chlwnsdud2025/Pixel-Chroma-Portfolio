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

        private readonly List<UIScoreRow> _pool = new();
        private float _refreshCountdown;
        private float _refreshInterval;
        private bool _isActive;

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
            _isActive = true;
        }

        private void OnDisable()
        {
            NetworkClient.UnregisterHandler<ScoreBoardMessage>();
            _isActive = false;
        }

        // ── 매 프레임: 갱신 카운트다운 ──

        private void Update()
        {
            if (!_isActive || _refreshInterval <= 0f) return;

            _refreshCountdown -= Time.deltaTime;
            if (_refreshCountdown < 0f) _refreshCountdown = 0f;

            if (refreshTimerText != null)
                refreshTimerText.text = $"{Mathf.CeilToInt(_refreshCountdown)}";
        }

        // ── 네트워크 수신 ──

        private void OnScoreBoardReceived(ScoreBoardMessage msg)
        {
            _refreshInterval  = msg.refreshInterval;
            _refreshCountdown = msg.refreshInterval;

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
    }
}