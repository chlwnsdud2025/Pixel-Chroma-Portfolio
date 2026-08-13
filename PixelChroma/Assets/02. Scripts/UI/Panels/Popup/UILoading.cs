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
        private List<int> _indices;
        private int _cursor;

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

        private void EnsureIndices()
        {
            if (_indices != null) return;

            _indices = new List<int>();
            for (int i = 0; i < textData.tooltips.Length; i++)
                _indices.Add(i);
            Shuffle(_indices);
            _cursor = 0;
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
            EnsureIndices();

            // Show 될 때마다 바로 다음 문구를 보여준다
            while (!token.IsCancellationRequested)
            {
                tooltipLabel.text = textData.tooltips[_indices[_cursor]].GetLocalizedString();

                _cursor++;
                if (_cursor >= _indices.Count)
                {
                    _cursor = 0;
                    Shuffle(_indices);
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