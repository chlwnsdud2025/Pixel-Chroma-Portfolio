using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace PixelChroma
{
    // ── Undo / Redo (스트로크 단위 되돌리기) ─────────────────────────────────────
    //   한 번의 드래그(마우스 down→up)를 1 스트로크로 묶어, 그 동안 값이 바뀐 셀만
    //   delta(before/after)로 기록한다. 스택은 폼(도화지)마다 독립이라(PaintForm.undo/redo)
    //   변신해도 각 폼의 되돌리기가 보존된다. 물총 페이드 · 전체 리셋 시엔 ClearHistory()로
    //   "활성 폼" 히스토리만 비운다 → 전투 중 되돌리기 악용/페이드 정합성 문제 회피
    //   (숨는 시간 위주 사용 전제).
    public partial class PlayerPaintCanvas
    {
        private const int MaxHistory = 50;

        internal struct CellChange
        {
            public int idx;
            public Color32 before;
            public Color32 after;
        }

        internal sealed class PaintCommand
        {
            public CellChange[] changes;
        }

        // 진행 중인 스트로크에서 각 셀의 "칠하기 전" 값을 최초 1회만 저장.
        // Undo/Redo 스택 자체는 폼별로 PaintForm.undo/redo 에 있다(도화지=폼마다 독립).
        private Dictionary<int, Color32> _strokeBefore;
        private PaintForm _strokeForm;
        private bool _strokeActive;

        public bool CanUndo => _activeForm?.undo != null && _activeForm.undo.Count > 0;
        public bool CanRedo => _activeForm?.redo != null && _activeForm.redo.Count > 0;

        // ── 스트로크 경계 (PlayerPaintController가 마우스 down/up에서 호출) ──────

        public void BeginStroke()
        {
            if (_strokeActive) return;              // 드래그 도중 반복 호출은 무시(첫 호출만 시작)
            _strokeBefore ??= new Dictionary<int, Color32>(256);
            _strokeBefore.Clear();
            _strokeForm = _activeForm;
            _strokeActive = true;
        }

        public void EndStroke()
        {
            if (!_strokeActive) return;
            _strokeActive = false;

            var form = _strokeForm;
            _strokeForm = null;
            if (form == null || form.undo == null || _strokeBefore.Count == 0) return;

            var list = new List<CellChange>(_strokeBefore.Count);
            foreach (var kv in _strokeBefore)
            {
                Color32 after = form.maskPixels[kv.Key];
                if (!ColorEquals(kv.Value, after))
                    list.Add(new CellChange { idx = kv.Key, before = kv.Value, after = after });
            }
            _strokeBefore.Clear();

            if (list.Count == 0) // 순 변화 없음(같은 색 덧칠 등) → 기록 안 함
                return;

            Push(form.undo, new PaintCommand { changes = list.ToArray() });

            // 새 스트로크가 생기면 이 폼의 redo 무효화
            form.redo.Clear();
        }

        // PaintLocal / EraseLocal 루프에서 셀을 건드리기 "직전"에 호출한다.
        private void RecordBefore(PaintForm form, int idx)
        {
            if (!_strokeActive || form != _strokeForm) return;
            if (!_strokeBefore.ContainsKey(idx))
                _strokeBefore[idx] = form.maskPixels[idx];
        }

        // ── Undo / Redo 실행 ────────────────────────────────────────────────────

        public bool Undo()
        {
            var form = _activeForm;
            if (form?.undo == null || form.undo.Count == 0) return false;
            var cmd = Pop(form.undo);
            ApplyCommand(cmd, useBefore: true);
            form.redo.Add(cmd);
            RelayCells(cmd, useBefore: true);   // 원격 뷰 동기화(변경 셀의 before 값)
            return true;
        }

        public bool Redo()
        {
            var form = _activeForm;
            if (form?.redo == null || form.redo.Count == 0) return false;
            var cmd = Pop(form.redo);
            ApplyCommand(cmd, useBefore: false);
            Push(form.undo, cmd);
            RelayCells(cmd, useBefore: false);  // 원격 뷰 동기화(변경 셀의 after 값)
            return true;
        }

        private void ApplyCommand(PaintCommand cmd, bool useBefore)
        {
            // 커맨드는 항상 현재 활성 폼 스택에서 나오므로 좌표계가 일치한다.
            var form = _activeForm;
            var changes = cmd.changes;
            for (int i = 0; i < changes.Length; i++)
                ApplyCellRaw(form, changes[i].idx, useBefore ? changes[i].before : changes[i].after);

            form.isDirty = true;
            ApplyChanges();
        }

        // ── 네트워크 릴레이 ─────────────────────────────────────────────────────
        //   Undo/Redo는 소유자 로컬에서만 실행되므로, 결과(변경 셀 + 목표색)를
        //   기존 CmdPaint/RpcPaint와 동일한 흐름(Cmd→Rpc, 비소유자만 적용)으로 원격에
        //   반영한다. 대형 스트로크/Fill은 메시지 크기 상한을 넘지 않도록 청크로 쪼갠다.
        //   폼 정보는 보내지 않는다 — 폼 전환이 CmdSwitchForm으로 이미 동기화되어
        //   원격의 _activeForm이 소유자와 같다는 전제(기존 페인팅과 동일).

        private const int RelayMaxCells = 1000;   // 청크당 셀 수 (셀당 약 8바이트)

        private void RelayCells(PaintCommand cmd, bool useBefore)
        {
            if (!isOwned) return;

            var changes = cmd.changes;
            int n = changes.Length;
            for (int start = 0; start < n; start += RelayMaxCells)
            {
                int len = Mathf.Min(RelayMaxCells, n - start);
                var idx = new int[len];
                var cols = new Color32[len];
                for (int i = 0; i < len; i++)
                {
                    var c = changes[start + i];
                    idx[i] = c.idx;
                    cols[i] = useBefore ? c.before : c.after;
                }
                CmdApplyCells(idx, cols);
            }
        }

        [Command]
        private void CmdApplyCells(int[] indices, Color32[] colors)
        {
            RpcApplyCells(indices, colors);
        }

        [ClientRpc]
        private void RpcApplyCells(int[] indices, Color32[] colors)
        {
            if (isOwned) return;

            var form = _activeForm;
            if (form == null || form.maskPixels == null || indices == null) return;

            int count = Mathf.Min(indices.Length, colors.Length);
            for (int i = 0; i < count; i++)
            {
                int idx = indices[i];
                if (idx < 0 || idx >= form.maskPixels.Length) continue;   // 폼 불일치 등 방어
                ApplyCellRaw(form, idx, colors[i]);
            }

            form.isDirty = true;
            ApplyChanges();
        }

        // maskPixels에 값을 쓰면서 paintedCells 카운터를 SetMaskCell/ClearMaskCell과
        // 동일한 규칙(referenceOpaque 셀의 알파 0↔비0 전이)으로 보정한다.
        private static void ApplyCellRaw(PaintForm form, int idx, Color32 value)
        {
            byte oldA = form.maskPixels[idx].a;
            byte newA = value.a;
            if (form.referenceOpaque[idx])
            {
                if (oldA == 0 && newA != 0) form.paintedCells++;
                else if (oldA != 0 && newA == 0) form.paintedCells--;
            }
            form.maskPixels[idx] = value;
            if (form.fading != null && form.fading.Count > 0) form.fading.Remove(idx);
        }

        // ── 히스토리 정리 ───────────────────────────────────────────────────────
        //   피격(EraseArea) · 전체 리셋(ResetTexture)에서 호출 → "활성 폼" 히스토리만 비운다.
        //   폼 전환은 clear하지 않고(폼별 독립) 진행 중 스트로크만 확정한다(ActivateForm).

        public void ClearHistory()
        {
            var form = _activeForm;
            if (form != null)
            {
                form.undo?.Clear();
                form.redo?.Clear();
            }
            _strokeActive = false;
            _strokeForm = null;
            _strokeBefore?.Clear();
        }

        // ── 스택 유틸 ───────────────────────────────────────────────────────────

        private static void Push(List<PaintCommand> stack, PaintCommand cmd)
        {
            stack.Add(cmd);
            if (stack.Count > MaxHistory) stack.RemoveAt(0);   // 상한 초과 시 가장 오래된 것 폐기
        }

        private static PaintCommand Pop(List<PaintCommand> stack)
        {
            int last = stack.Count - 1;
            var cmd = stack[last];
            stack.RemoveAt(last);
            return cmd;
        }

        private static bool ColorEquals(Color32 a, Color32 b)
            => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
