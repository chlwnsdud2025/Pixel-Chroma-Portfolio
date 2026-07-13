namespace PixelChroma
{
    public static class GameModeRuleFactory
    {
        public static GameModeRule Create(GameMode mode)
        {
            return mode switch
            {
                GameMode.Infection => new InfectionGameModeRule(),
                GameMode.Basic => new BasicGameModeRule(),
                _ => new BasicGameModeRule(),
            };
        }
    }
}