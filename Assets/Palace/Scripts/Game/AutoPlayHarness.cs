using System;
using System.Collections;
using System.IO;
using System.Linq;
using Palace.Core;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// TESTING ONLY. Does nothing unless the game is launched with command-line options:
    ///   -palace-autoplay N    play N complete games automatically (your seat is played by a bot)
    ///   -palace-players P     table size (2-10)
    ///   -palace-shots DIR     save screenshots into DIR
    /// Logs a summary line per game and any errors, then quits. Used to test real builds.
    /// </summary>
    public class AutoPlayHarness : MonoBehaviour
    {
        public static bool Active { get; private set; }

        private GameController controller;
        private int gamesWanted;
        private int gamesPlayed;
        private int errors;
        private string shotsDir;
        private bool midShotTaken, lateShotTaken;

        public static void AttachIfRequested(GameController controller)
        {
            string[] args = Environment.GetCommandLineArgs();
            int games = IntArg(args, "-palace-autoplay", 0);
            if (games <= 0) return;

            Active = true;
            GameLaunch.SpectateOnly = false;
            GameLaunch.BotCount = Mathf.Clamp(IntArg(args, "-palace-players", 4), 2, 10) - 1;
            GameSettings.AnimationOverride = GameSettings.AnimationSpeed.Fast; // not saved to the player's settings

            var harness = controller.gameObject.AddComponent<AutoPlayHarness>();
            harness.controller = controller;
            harness.gamesWanted = games;
            harness.shotsDir = StringArg(args, "-palace-shots");
            if (!string.IsNullOrEmpty(harness.shotsDir)) Directory.CreateDirectory(harness.shotsDir);
            controller.GameFinished += harness.OnGameFinished;
            Application.logMessageReceived += harness.OnLog;
            Debug.Log($"[AutoPlay] starting {games} games with {GameLaunch.BotCount + 1} players");
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors++;
                Console.WriteLine("[AutoPlay] ERROR: " + message + "\n" + stackTrace);
            }
        }

        private void Update()
        {
            PalaceGame game = controller.Game;
            if (game == null) return;

            if (game.State.TurnNumber > 4000)
            {
                Debug.LogError("[AutoPlay] game exceeded 4000 turns");
                Quit();
            }

            if (string.IsNullOrEmpty(shotsDir) || gamesPlayed > 0) return;
            if (!midShotTaken && game.State.TurnNumber >= 12 && !controller.IsBusy)
            {
                midShotTaken = true;
                Shot("game_early");
            }
            if (!lateShotTaken && game.State.DrawPile.Count == 0 && game.State.TurnNumber >= 60 && !controller.IsBusy)
            {
                lateShotTaken = true;
                Shot("game_late");
            }
        }

        private void OnGameFinished(PalaceGame game)
        {
            gamesPlayed++;
            var order = string.Join(", ", game.State.FinishOrder.Select(p => game.State.Players[p].Name));
            Debug.Log($"[AutoPlay] game {gamesPlayed}/{gamesWanted} finished after {game.State.TurnNumber} turns. Order: {order}");
            StartCoroutine(Next());
        }

        private IEnumerator Next()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            if (gamesPlayed == 1 && !string.IsNullOrEmpty(shotsDir)) Shot("results");
            yield return new WaitForSecondsRealtime(0.4f);

            if (gamesPlayed >= gamesWanted) Quit();
            else controller.StartNewGame();
        }

        private void Shot(string name)
        {
            string path = Path.Combine(shotsDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[AutoPlay] screenshot " + path);
        }

        private void Quit()
        {
            Debug.Log($"[AutoPlay] DONE games={gamesPlayed} errors={errors}");
            Application.Quit(errors == 0 ? 0 : 1);
        }

        private static int IntArg(string[] args, string name, int fallback)
        {
            string s = StringArg(args, name);
            return int.TryParse(s, out int v) ? v : fallback;
        }

        private static string StringArg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
