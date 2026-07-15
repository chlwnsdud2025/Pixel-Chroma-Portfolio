using Mirror;
using TMPro;

namespace PixelChroma
{
    // 닉네임 텍스트 갱신 및 역할별 가시성 로직을 PlayerBase에서 분리한 헬퍼입니다.
    public class PlayerNicknameDisplay
    {
        private readonly PlayerBase _owner;
        private readonly TMP_Text _text;

        public PlayerNicknameDisplay(PlayerBase owner, TMP_Text text)
        {
            _owner = owner;
            _text = text;
        }

        // SyncVar 갱신/스폰 시 닉네임 문자열을 반영합니다.
        public void Refresh()
        {
            if (_text != null)
                _text.text = _owner.playerName;
        }

        // 역할/사망 상태가 바뀔 때 이벤트로 호출: 로컬 플레이어 관점에서 이 닉네임을 보여줄지 결정합니다.
        public void UpdateVisibility()
        {
            if (_text == null) return;
            if (NetworkClient.localPlayer == null) return;

            PlayerBase localPlayer = NetworkClient.localPlayer.GetComponent<PlayerBase>();
            if (localPlayer == null) return;

            // 1순위: 내 닉네임은 항상 내가 볼 수 있음
            if (_owner == localPlayer)
            {
                _text.enabled = true;
                return;
            }

            // 2순위: 아직 역할이 없으면(로비 등) 전원 공개
            if (localPlayer.role == PlayerRole.None || _owner.role == PlayerRole.None)
            {
                _text.enabled = true;
                return;
            }

            // 3순위: 정산 화면(CheckTime)에는 모드 규칙보다 우선해서 무조건 전원 공개
            if (GameRoundManager.Instance != null && GameRoundManager.Instance.CurrentTimeState == TimeState.CheckTime)
            {
                _text.enabled = true;
                return;
            }

            // 4순위: 러너가 다른 러너를 보는 경우만 모드 규칙에 위임 (언제 숨길지는 GameModeRule이 결정)
            if (localPlayer.role == PlayerRole.Runner && _owner.role == PlayerRole.Runner)
            {
                var round = GameRoundManager.Instance;
                _text.enabled = round?.CurrentRule == null || round.CurrentRule.CanSeeRunnerNickname(round.CurrentTimeState);
                return;
            }

            // 5순위(기본값): 그 외 모든 조합 — 기존부터 있던 고정 규칙
            // Seeker는 Runner 닉네임을 못 보고, Runner는 Seeker 닉네임을 봄. 모드와 무관하게 항상 이 규칙.
            _text.enabled = localPlayer.role == PlayerRole.Runner || _owner.role == PlayerRole.Seeker;
        }
    }
}
