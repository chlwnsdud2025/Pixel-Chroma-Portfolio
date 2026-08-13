using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneText.UGUI;
using UnityEngine;

namespace PixelChroma.UI
{
    public class UILoading : UIPopup
    {
        public override bool CloseOnEscape => false;

        [Header("Tooltip")]
        [SerializeField] private LoadingTooltipTextDataSO textData;
        [SerializeField] private OneTextLabel tooltipLabel;
        [SerializeField] private float tooltipInterval = 5f;

        private CancellationTokenSource _tooltipCts;

        protected override UniTask OnShow()
        {
            StartTooltipCycle();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnHide()
        {
            StopTooltipCycle();
            return UniTask.CompletedTask;
        }

        private void StartTooltipCycle()
        {
            if (textData == null || textData.tooltips == null || textData.tooltips.Length == 0)
                return;
            if (tooltipLabel == null)
                return;

            StopTooltipCycle();
            _tooltipCts = new CancellationTokenSource();
            RunTooltipCycle(_tooltipCts.Token).Forget();
        }

        private void StopTooltipCycle()
        {
            if (_tooltipCts == null) return;
            _tooltipCts.Cancel();
            _tooltipCts.Dispose();
            _tooltipCts = null;
        }

        private async UniTaskVoid RunTooltipCycle(CancellationToken token)
        {
            // 셔플 인덱스 목록을 만들어 중복 없이 순회
            var indices = new List<int>();
            for (int i = 0; i < textData.tooltips.Length; i++)
                indices.Add(i);
            Shuffle(indices);

            int cursor = 0;

            while (!token.IsCancellationRequested)
            {
                // 현재 인덱스의 로컬라이즈 문자열 표시
                var localized = textData.tooltips[indices[cursor]];
                tooltipLabel.text = localized.GetLocalizedString();

                cursor++;
                if (cursor >= indices.Count)
                {
                    // 한 바퀴 다 돌면 다시 셔플
                    cursor = 0;
                    Shuffle(indices);
                }

                try
                {
                    await UniTask.Delay(
                        (int)(tooltipInterval * 1000),
                        ignoreTimeScale: true,
                        cancellationToken: token);
                }
                catch (System.OperationCanceledException)
                {
                    return;
                }
            }
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}