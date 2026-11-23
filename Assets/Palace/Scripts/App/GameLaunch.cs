namespace Palace
{
    /// <summary>
    /// How the next game should start. The main menu fills this in, then loads the Game scene.
    /// Pressing Play directly on the Game scene uses these defaults.
    /// </summary>
    public static class GameLaunch
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameScene = "Game";

        public static string PlayerName = "You";
        public static int BotCount = 3;
        public static bool SpectateOnly;   // every seat is a bot (used for testing / "watch a game")
        public static int Seed;            // 0 = random

        /// <summary>Shown on the Multiplayer screen after returning from a game (e.g. "The host left").</summary>
        public static string MenuMessage;

        /// <summary>Set by the splash screen so it only shows once per app launch.</summary>
        public static bool SplashShown;
    }
}
