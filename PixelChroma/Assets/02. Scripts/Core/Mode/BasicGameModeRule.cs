using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace PixelChroma
{
    public class BasicGameModeRule : GameModeRule
    {
        public override GameMode Mode => GameMode.Basic;

        private static readonly LocalizedString _displayName = new("GameText", "HUD_Mode_Basic");
        public override string DisplayName => _displayName.GetLocalizedString();
        public override void OnRunnerCaught(RunnerPlayer runner) => runner.ServerBecomeSpectator();//잡힐시 관전

        public override bool CanSeeRunnerNickname(TimeState currentState) => true;//Runner끼리 닉네임 항상 보임
        public override bool CanSeeRunnerBody(TimeState currentState) => true;//Runner끼리 몸 항상 보임
        public override bool CanRunnersCollide(TimeState currentState) => true;//Runner끼리 충돌 항상 됨

        public override IReadOnlyList<RunnerPlayer> GetSpectatableRunners(RunnerPlayer viewer)//Runner전원 관전 가능
        {
            return InGameManager.Instance != null ? 
                InGameManager.Instance.Runners : System.Array.Empty<RunnerPlayer>();
        }

        public override bool ShouldShowTeammateIndicator(PlayerRole role) => true;
    }
}