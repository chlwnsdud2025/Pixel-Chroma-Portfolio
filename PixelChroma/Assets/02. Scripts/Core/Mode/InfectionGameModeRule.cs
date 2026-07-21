using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace PixelChroma
{
    public class InfectionGameModeRule : GameModeRule
    {
        public override GameMode Mode => GameMode.Infection;

        private static readonly LocalizedString _displayName = new("GameText", "HUD_Mode_Infection");
        public override string DisplayName => _displayName.GetLocalizedString();
        public override void OnRunnerCaught(RunnerPlayer runner) => runner.ServerConvertToSeeker();//잡힐시 술래

        public override bool CanSeeRunnerNickname(TimeState currentState)// Runner끼리 CheckTime전까지 닉네임 못봄
            => currentState != TimeState.HideTime && currentState != TimeState.SeekTime;

        public override bool CanSeeRunnerBody(TimeState currentState)//Runner끼리 Hide때 서로 몸 안보임
            => currentState != TimeState.HideTime;

        public override bool CanRunnersCollide(TimeState currentState) //Runner끼리 Hide때 충돌 안됨
            => currentState != TimeState.HideTime;

        //Runner관전 본인만 가능
        public override IReadOnlyList<RunnerPlayer> GetSpectatableRunners(RunnerPlayer viewer)
        {
            return viewer != null ? new[] { viewer } : System.Array.Empty<RunnerPlayer>();

        }

        public override bool ShouldShowTeammateIndicator(PlayerRole role) => role == PlayerRole.Seeker;
    }
}