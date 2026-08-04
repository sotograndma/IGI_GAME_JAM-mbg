namespace MBG.Core
{
    /// <summary>
    /// State machine tunggal milik <see cref="GameManager"/>. Semua sistem lain
    /// membaca state ini lewat <see cref="GameEventBus.OnGameStateChanged"/>,
    /// bukan dengan menyimpan flag sendiri.
    /// </summary>
    public enum GameState
    {
        Boot,
        MainMenu,
        Playing,
        Paused,
        InQTE,
        InObstacle,
        DaySummary,
        GameOver
    }

    public static class GameStateExtensions
    {
        /// <summary>
        /// True untuk state di mana dunia gameplay berjalan (pemain bisa bergerak
        /// atau sedang di dalam mini-game). Dipakai <see cref="GameManager.IsGameplayActive"/>.
        /// </summary>
        public static bool IsGameplay(this GameState state)
        {
            return state == GameState.Playing
                || state == GameState.InQTE
                || state == GameState.InObstacle;
        }
    }
}
