using DG.Tweening;
using Mirror;
using OneText.UGUI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Animation")]
        [SerializeField] private float sweepDuration = 0.4f;      // 스윕 1회 시간
        [SerializeField] private float sweepStagger = 0.12f;      // Row 간 시간차
        [SerializeField] private float counterDuration = 0.6f;

        //---애니매이션(효과) 용--
        private readonly Dictionary<uint, int> _prevScoreMap = new();
        private readonly Dictionary<uint, int> _prevRankMap = new();

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
            NetworkClient.RegisterHandler<RevealedPixelMessage>(OnRevealedPixelReceived);
            NetworkClient.RegisterHandler<PlayerDeadMessage>(OnPlayerDeadReceived);
            GameRoundManager.OnTimerTick += HandleTimerTick;
        }

        private void OnDisable()
        {
            NetworkClient.UnregisterHandler<ScoreBoardMessage>();
            NetworkClient.UnregisterHandler<RevealedPixelMessage>();
            NetworkClient.UnregisterHandler<PlayerDeadMessage>();
            GameRoundManager.OnTimerTick -= HandleTimerTick;
        }

        // ── 네트워크 수신 ──

        private void OnScoreBoardReceived(ScoreBoardMessage msg)
        {
            _refreshCountdown = Mathf.RoundToInt(msg.refreshInterval);
            RebuildRows(msg.entries);
        }

        // ── UI 구축 (오브젝트 풀링) ──

        private void RebuildRows(ScoreEntry[] entries)
        {
            uint myNetId = NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.netId : 0;

            // ── 풀 확장 ──
            while (_pool.Count < entries.Length)
            {
                var row = Instantiate(rowPrefab, rowContainer);
                row.ResetRow();
                _pool.Add(row);
            }

            // ── 남는 행 비활성화 ──
            for (int i = entries.Length; i < _pool.Count; i++)
                _pool[i].ResetRow();

            // ── 최초 수신: 애니메이션 없이 즉시 표시 ──
            if (_prevScoreMap.Count == 0)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    _pool[i].gameObject.SetActive(true);
                    _pool[i].SetData(entries[i], entries[i].netId == myNetId);
                }

                SaveCurrentState(entries);
                return;
            }

            // ── 스윕 시퀀스: Row별 시간차로 스윕 → 중간에 데이터 갱신 ──
            for (int i = 0; i < entries.Length; i++)
            {
                int idx = i;   // 클로저용 캡처
                var entry = entries[idx];

                _pool[idx].gameObject.SetActive(true);

                // 이전 상태 조회
                _prevScoreMap.TryGetValue(entry.netId, out int oldScore);
                _prevRankMap.TryGetValue(entry.netId, out int oldRank);

                // 스윕 시작 (시간차 적용)
                DOVirtual.DelayedCall(idx * sweepStagger, () =>
                {
                    _pool[idx].PlaySweep(sweepDuration, onMidPoint: () =>
                    {
                        // ── 스윕 중간 지점에서 데이터 갱신 ──
                        _pool[idx].SetData(entry, entry.netId == myNetId);

                        // 점수 변경 → 카운터 롤
                        if (oldScore != entry.score)
                            _pool[idx].AnimateScoreCounter(oldScore, entry.score, counterDuration);

                        // 순위 변경 → 글로우 펄스 + 뱃지 모핑
                        if (oldRank != entry.rank)
                        {
                            _pool[idx].PlayGlowPulse(entry.rank < oldRank);
                            _pool[idx].AnimateRankBadge(oldRank, entry.rank);
                        }
                    });
                });
            }

            SaveCurrentState(entries);
        }

        private void SaveCurrentState(ScoreEntry[] entries)
        {
            _prevScoreMap.Clear();
            _prevRankMap.Clear();

            for (int i = 0; i < entries.Length; i++)
            {
                _prevScoreMap[entries[i].netId] = entries[i].score;
                _prevRankMap[entries[i].netId] = entries[i].rank;
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

        private void OnRevealedPixelReceived(RevealedPixelMessage msg)
        {
            foreach (var row in _pool)
            {
                if (row.gameObject.activeSelf && row.CurrentNetId == msg.netId)
                {
                    row.PlayExposeEffect(msg.color);
                    break;
                }
            }
        }

        private void OnPlayerDeadReceived(PlayerDeadMessage msg)
        {
            foreach (var row in _pool)
            {
                if (row.gameObject.activeSelf && row.CurrentNetId == msg.netId)
                {
                    row.ApplyDeadColor();
                    break;
                }
            }
        }
    }
}