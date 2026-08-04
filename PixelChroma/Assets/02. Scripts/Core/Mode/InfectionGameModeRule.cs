using System.Collections.Generic;

namespace PixelChroma
{
    public class InfectionGameModeRule : GameModeRule
    {
        public override GameMode Mode => GameMode.Infection;
        public override bool CanSeeRunnerNickname(TimeState currentState)// Runner끼리 CheckTime전까지 닉네임 못봄
            => currentState != TimeState.HideTime && currentState != TimeState.SeekTime;

        public override bool CanSeeRunnerBody(TimeState currentState)//Runner끼리 Hide때 서로 몸 안보임
            => currentState != TimeState.HideTime;

        //Runner관전 본인만 가능
        public override IReadOnlyList<RunnerPlayer> GetSpectatableRunners(RunnerPlayer viewer)
        {
            return viewer != null ? new[] { viewer } : System.Array.Empty<RunnerPlayer>();

        }

        public override bool ShouldShowTeammateIndicator(PlayerRole role) => role == PlayerRole.Seeker;
    }
}