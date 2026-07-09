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

            // 내 닉네임은 항상 보입니다.
            if (_owner == localPlayer)
            {
                _text.enabled = true;
                return;
            }

            // 아직 역할이 배정되지 않았다면(로비 등) 모두 표시합니다.
            if (localPlayer.role == PlayerRole.None || _owner.role == PlayerRole.None)
            {
                _text.enabled = true;
                return;
            }

            // Seeker에게는 Runner 닉네임을 숨깁니다 (숨은 위치 노출 방지).
            // Runner는 모든 닉네임을 볼 수 있습니다.
            _text.enabled = localPlayer.role == PlayerRole.Runner || _owner.role == PlayerRole.Seeker;
        }
    }
}
