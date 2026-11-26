using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Palace.Core;
using TMPro;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// TESTING ONLY. Does nothing unless the game is launched with command-line options:
    ///   -palace-autoplay N    play N complete games automatically (your seat is played by a bot)
    ///   -palace-players P     table size (2-10)
    ///   -palace-shots DIR     save screenshots into DIR
    ///   -palace-botlevel L    computer skill: 0 Easy, 1 Normal, 2 Hard
    ///   -palace-tutorial      play the guided game (tips are dismissed automatically)
    ///   -palace-swap          play with the optional swap-before-play mode
    ///   -palace-clean         with -palace-shots: only take table shots with names, labels and buttons hidden
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
        private float tipTimer;
        private int tipShots;

        public static void AttachIfRequested(GameController controller)
        {
            string[] args = Environment.GetCommandLineArgs();
            int games = IntArg(args, "-palace-autoplay", 0);
            if (games <= 0) return;

            Active = true;
            GameLaunch.SpectateOnly = false;
            GameLaunch.BotCount = Mathf.Clamp(IntArg(args, "-palace-players", 4), 2, 10) - 1;
            GameLaunch.BotLevel = Mathf.Clamp(IntArg(args, "-palace-botlevel", 1), 0, 2);
            GameLaunch.Tutorial = Array.IndexOf(args, "-palace-tutorial") >= 0; // the guided game, tips auto-dismissed
            GameLaunch.SwapMode = Array.IndexOf(args, "-palace-swap") >= 0;    // the optional swap-before-play mode
            GameSettings.AnimationOverride = GameSettings.AnimationSpeed.Fast; // not saved to the player's settings

            var harness = controller.gameObject.AddComponent<AutoPlayHarness>();
            harness.controller = controller;
            harness.gamesWanted = games;
            harness.shotsDir = StringArg(args, "-palace-shots");
            harness.clean = Array.IndexOf(args, "-palace-clean") >= 0; // table-only shots for the app icon
            harness.idleTurnTest = Array.IndexOf(args, "-palace-idle-turn") >= 0; // network games: let the turn clock run out once
            if (!string.IsNullOrEmpty(harness.shotsDir)) Directory.CreateDirectory(harness.shotsDir);
            controller.GameFinished += harness.OnGameFinished;
            controller.EventStarted += harness.OnEventStarted;
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

            if (controller.TipShowing)
            {
                // Screenshot the first few tips, then press "Got it" (a moment later, so the shot includes the tip).
                float before = tipTimer;
                tipTimer += Time.unscaledDeltaTime;
                if (before < 0.5f && tipTimer >= 0.5f && tipShots < 4 && !string.IsNullOrEmpty(shotsDir)) Shot("tutorial_tip_" + tipShots++);
                if (tipTimer > 0.9f)
                {
                    controller.TestDismissTip();
                    tipTimer = 0f;
                }
                return;
            }

            if (idleTurnTest && !idleTested && Palace.Net.NetSession.Active && game.State.TurnNumber >= 6 && !controller.IsBusy
                && game.State.Phase == GamePhase.Playing && game.State.Current == controller.LocalPlayerIndex)
                StartCoroutine(IdleTurnTest());

            if (!dragTested && game.State.TurnNumber >= 3 && !controller.IsBusy)
            {
                CardView card = controller.FindPlayableHandCard();
                if (card != null) StartCoroutine(DragTest(card));
            }

            if (string.IsNullOrEmpty(shotsDir) || gamesPlayed > 0) return;
            if (clean)
            {
                // Clean shots only: the table with no names, buttons or labels (for the app icon).
                if (!cleanBusy && cleanIndex < CleanTurns.Length && game.State.TurnNumber >= CleanTurns[cleanIndex] && !controller.IsBusy)
                    StartCoroutine(CleanShot());
                return;
            }
            if (!pileShotTaken && game.State.Discard.Count >= 6 && !controller.IsBusy)
            {
                pileShotTaken = true;
                StartCoroutine(PileViewerShot());
            }
            if (!turnShotTaken && !idleTurnTest && game.State.TurnNumber >= 6 && game.State.Phase == GamePhase.Playing
                && game.State.Current == controller.LocalPlayerIndex && !controller.IsBusy)
            {
                turnShotTaken = true;
                StartCoroutine(YourTurnShots());
            }
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

        // ------------------------------------------------------------------ multiplayer turn timer

        private bool idleTurnTest, idleTested;

        /// <summary>Sits on one of your goes in a network game until the clock runs out, then checks you picked up the pile.</summary>
        private IEnumerator IdleTurnTest()
        {
            idleTested = true;
            controller.HoldAutoPlay = true;
            PalaceGame game = controller.Game;
            int me = controller.LocalPlayerIndex;
            int handBefore = game.State.Players[me].Hand.Count, pile = game.State.Discard.Count;
            float start = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(4f);
            if (!string.IsNullOrEmpty(shotsDir)) Shot("turn_timer");
            yield return new WaitForSecondsRealtime(12.5f);
            if (!string.IsNullOrEmpty(shotsDir)) Shot("turn_timer_low");
            while (controller.Game.State.Current == me && Time.unscaledTime - start < 40f) yield return null;
            float took = Time.unscaledTime - start;
            while (controller.IsBusy) yield return null;
            int handAfter = controller.Game.State.Players[me].Hand.Count;
            bool ok = took >= GameController.TurnSeconds - 0.5f && took < GameController.TurnSeconds + 6f && (pile == 0 || handAfter == handBefore + pile);
            Debug.Log($"[AutoPlay] turn timer: the go ended after {took:0.0}s, hand {handBefore} -> {handAfter} (pile had {pile}) " + (ok ? "OK" : "FAILED"));
            if (!ok) errors++;
            controller.HoldAutoPlay = false;
        }

        // ------------------------------------------------------------------ your turn prompt

        private bool turnShotTaken;

        /// <summary>Waits on your own go like a distracted player: the edge glow, then the reminder after 8 seconds.</summary>
        private IEnumerator YourTurnShots()
        {
            controller.HoldAutoPlay = true;
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("your_turn_glow");
            yield return new WaitForSecondsRealtime(7.7f); // the reminder fires at 8s: catch the bounce and the banner
            Shot("your_turn_nudge");
            yield return new WaitForSecondsRealtime(0.5f);
            controller.HoldAutoPlay = false;
        }

        // ------------------------------------------------------------------ clean screenshots (app icon)

        private static readonly int[] CleanTurns = { 8, 14, 20, 28, 36, 46, 58 };
        private bool clean, cleanBusy;
        private int cleanIndex;

        private IEnumerator CleanShot()
        {
            cleanBusy = true;
            controller.HoldAutoPlay = true;
            yield return new WaitForSecondsRealtime(0.8f); // let the cards settle
            while (controller.IsBusy) yield return null;
            HideForCleanShot();
            yield return null;
            yield return null;
            string path = Path.Combine(shotsDir, "clean_" + cleanIndex + ".png");
            ScreenCapture.CaptureScreenshot(path); // (supersizing zooms the overlay UI instead of enlarging it)
            Debug.Log("[AutoPlay] clean screenshot " + path);
            yield return new WaitForSecondsRealtime(0.4f);
            cleanIndex++;
            if (cleanIndex >= CleanTurns.Length) Quit();
            controller.HoldAutoPlay = false;
            cleanBusy = false;
        }

        /// <summary>Hides everything that isn't cards or the table: names, avatars, count bubbles, labels, buttons.</summary>
        private void HideForCleanShot()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            TableView table = controller.Table;
            if (typeof(GameController).GetField("hudRoot", flags)?.GetValue(controller) is RectTransform hud)
                foreach (Transform child in hud)
                    if (!table.transform.IsChildOf(child)) child.gameObject.SetActive(false);

            foreach (var text in table.GetComponentsInChildren<TMP_Text>(true))
                if (text.GetComponentInParent<CardView>(true) == null) text.enabled = false;

            string[] seatParts = { "plate", "avatar", "avatarRing", "handBadge", "countBubble", "turnGlow", "placeBadge" };
            foreach (var seat in table.GetComponentsInChildren<SeatView>(true))
                foreach (string part in seatParts)
                    if (typeof(SeatView).GetField(part, flags)?.GetValue(seat) is Component c && c != null) c.gameObject.SetActive(false);

            if (typeof(TableView).GetField("effectBadge", flags)?.GetValue(table) is Component badge && badge != null) badge.gameObject.SetActive(false);
            foreach (var ring in table.GetComponentsInChildren<DirectionRing>(true)) ring.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ effect screenshots

        private readonly System.Collections.Generic.HashSet<string> effectShots = new System.Collections.Generic.HashSet<string>();

        private void OnEventStarted(GameEvent e)
        {
            if (string.IsNullOrEmpty(shotsDir) || clean) return;
            switch (e.Type)
            {
                case GameEventType.PileBurned: EffectShot("fx_burn", 0.55f); break;
                case GameEventType.PlayersSkipped: EffectShot("fx_skip", 0.3f); break;
                case GameEventType.SevenDeclared: EffectShot("fx_seven", 0.3f); break;
                case GameEventType.DirectionReversed: EffectShot("fx_reverse", 0.2f); break;
                case GameEventType.AceChoiceNeeded:
                    if (e.Player == 0 && effectShots.Add("fx_ace_picker")) StartCoroutine(AcePickerShot());
                    break;
            }
        }

        private void EffectShot(string name, float delay)
        {
            if (effectShots.Add(name)) StartCoroutine(DelayedShot(name, delay));
        }

        private IEnumerator DelayedShot(string name, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Shot(name);
        }

        private IEnumerator AcePickerShot()
        {
            controller.HoldAutoPlay = true;
            while (controller.IsBusy) yield return null;
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("fx_ace_picker");
            yield return new WaitForSecondsRealtime(0.2f);
            controller.HoldAutoPlay = false;
        }

        // ------------------------------------------------------------------ drag-and-drop test

        private bool dragTested;

        /// <summary>Simulates a finger dragging a playable hand card onto the pile, like a real player.</summary>
        private IEnumerator DragTest(CardView view)
        {
            dragTested = true;
            controller.HoldAutoPlay = true;
            int turnBefore = controller.Game.State.TurnNumber;
            int handBefore = controller.Game.State.Players[0].Hand.Count;

            var canvas = controller.Table.GetComponentInParent<Canvas>();
            Camera cam = canvas.worldCamera;
            var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            Vector2 from = RectTransformUtility.WorldToScreenPoint(cam, view.transform.position);
            Vector2 to = RectTransformUtility.WorldToScreenPoint(cam, controller.Table.transform.TransformPoint(controller.Table.DiscardPoint));

            var hit = new UnityEngine.EventSystems.RaycastResult { module = raycaster, gameObject = view.gameObject };
            var ped = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                position = from, pressPosition = from, pointerPressRaycast = hit, pointerCurrentRaycast = hit, dragging = true
            };

            UnityEngine.EventSystems.ExecuteEvents.Execute(view.gameObject, ped, UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
            for (int i = 1; i <= 12; i++)
            {
                ped.position = Vector2.Lerp(from, to, i / 12f);
                UnityEngine.EventSystems.ExecuteEvents.Execute(view.gameObject, ped, UnityEngine.EventSystems.ExecuteEvents.dragHandler);
                if (i == 11 && !string.IsNullOrEmpty(shotsDir)) Shot("drag_over_pile");
                yield return null;
                yield return null;
            }
            UnityEngine.EventSystems.ExecuteEvents.Execute(view.gameObject, ped, UnityEngine.EventSystems.ExecuteEvents.endDragHandler);

            yield return new WaitForSecondsRealtime(0.2f);
            while (controller.IsBusy || controller.IsWaitingForHost) yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
            while (controller.IsBusy) yield return null;
            bool played = controller.Game.State.TurnNumber != turnBefore || controller.Game.State.Players[0].Hand.Count != handBefore;
            Debug.Log("[AutoPlay] drag-and-drop test: " + (played ? "card was played by dragging it onto the pile" : "FAILED - nothing was played"));
            if (!played) errors++;
            controller.HoldAutoPlay = false;
        }

        private bool pileShotTaken;

        private IEnumerator PileViewerShot()
        {
            controller.TestOpenPileViewer();
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("pile_viewer");
            yield return new WaitForSecondsRealtime(0.2f);
            controller.TestClosePileViewer();
        }

        private void OnGameFinished(PalaceGame game)
        {
            gamesPlayed++;
            var order = string.Join(", ", game.State.FinishOrder.Select(p => game.State.Players[p].Name));
            string role = Net.NetSession.Active ? (Net.NetSession.Instance.IsHost ? " [host]" : " [client]") : "";
            Debug.Log($"[AutoPlay]{role} game {gamesPlayed}/{gamesWanted} finished after {game.State.TurnNumber} turns. Order: {order}");
            StartCoroutine(Next());
        }

        private IEnumerator Next()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            if (gamesPlayed == 1 && !string.IsNullOrEmpty(shotsDir)) Shot("results");
            yield return new WaitForSecondsRealtime(0.4f);

            bool networkClient = Net.NetSession.Active && !Net.NetSession.Instance.IsHost;
            if (gamesPlayed >= gamesWanted)
            {
                if (Net.NetSession.Active && Net.NetSession.Instance.IsHost) yield return new WaitForSecondsRealtime(2f); // let clients log their result
                Quit();
            }
            else if (!networkClient) controller.StartNewGame(); // in multiplayer only the host deals
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
