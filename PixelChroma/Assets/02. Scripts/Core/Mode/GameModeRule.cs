using System.Collections.Generic;

namespace PixelChroma
{

    public abstract class GameModeRule
    {
        public abstract GameMode Mode { get; }

        // 술래에게 잡혔을 때 해당 러너를 어떻게 처리할지 결정
        public virtual void OnRunnerCaught(RunnerPlayer runner) => runner.ServerBeginDeath(Mode);

        // 현재 타임스테이트에서 러너끼리 서로의 닉네임을 볼 수 있는지 결정
        public abstract bool CanSeeRunnerNickname(TimeState currentState);

        // Runner가 다른 Runner의 몸(스프라이트)을 볼 수 있는지
        public abstract bool CanSeeRunnerBody(TimeState currentState);

        //Runner관전자 List
        public abstract IReadOnlyList<RunnerPlayer> GetSpectatableRunners(RunnerPlayer viewer, TimeState currentState);
        //팀 위치 확인용 indicator 
        public abstract bool ShouldShowTeammateIndicator(PlayerRole role);
    }
}