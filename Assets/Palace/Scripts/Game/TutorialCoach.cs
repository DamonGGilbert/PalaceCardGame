using System.Collections.Generic;
using System.Linq;
using Palace.Core;
using TMPro;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// The guided first game. Each tip is shown once, at the moment it matters: when you first
    /// hold or see a special card, when you first have to pick up, when you move on to your
    /// face-up and face-down cards, and so on. While a tip is showing (or waiting to be shown)
    /// the computer players wait, so nothing happens behind it. The rules are exactly the same as
    /// a normal game; only the deal is arranged (see Palace.Core.TutorialSetup).
    /// </summary>
    public sealed class TutorialCoach
    {
        private readonly GameController game;
        private readonly RectTransform hud;
        private readonly int local;
        private readonly HashSet<string> shown = new HashSet<string>();
        private readonly Queue<string> queue = new Queue<string>();
        private RectTransform panel;
        private bool skipped;
        private int tipNumber;

        public TutorialCoach(GameController game, RectTransform hud, int localPlayer)
        {
            this.game = game;
            this.hud = hud;
            local = localPlayer;
        }

        /// <summary>True while a tip is on screen or waiting: the computer players hold off.</summary>
        public bool Holding => !skipped && (panel != null || queue.Count > 0);
        public bool Showing => panel != null;

        // ------------------------------------------------------------------ the tips

        private static string Text(string id)
        {
            switch (id)
            {
                case "welcome": return "Welcome to Palace! The aim is to get rid of all your cards. The last player still holding cards is the Shedhead.";
                case "stages": return "You have 3 cards in your hand, 3 face-up cards on the table and 3 face-down cards under them. Play your hand first, then your face-up cards, and your face-down cards last, without looking.";
                case "play": return "Your turn! Play a card equal to or higher than the top card of the pile. Drag it onto the pile, or tap it and press PLAY. Cards you can't play are darkened.";
                case "multi": return "You have more than one card of the same rank. Tap them all, then press PLAY, to get rid of them together.";
                case "draw": return "While there are cards in the deck, you draw back up to 3 cards after your turn.";
                case "two": return "2 is a reset card. It can go on anything, and the next player can then play any card.";
                case "seven": return "7: whoever plays it calls HIGHER or LOWER. The next player must then play higher than 7, or lower than 7.";
                case "eight": return "8 skips the next player. Two 8s together skip two players.";
                case "ten": return "10 burns the pile: it's removed from the game and the same player goes again. A 10 can go on anything (except after LOWER on a 7).";
                case "queen": return "A Queen reverses the direction of play. The arrows around the table show which way it's going.";
                case "ace": return "An Ace is the highest card. When you play one you choose what it counts as (any value that matches or beats the pile), or use it as a 10 to burn the pile.";
                case "pickup": return "None of your cards can go on the pile, so you have to pick it all up. Press PICK UP PILE. It happens to everyone!";
                case "faceup": return "Your hand is empty and the deck has run out, so now you play your face-up cards. Same rules as before.";
                case "facedown": return "Last stage! Tap one of your face-down cards to turn it over. If it can't go on the pile, you pick up the pile along with it.";
                case "viewpile": return "Tip: tap the pile (or View pile) to see every card in it and plan your next move.";
                case "finish": return "A player has run out of cards and finished! The game carries on until only one player still has cards.";
                default: return id;
            }
        }

        private void Want(string id)
        {
            if (skipped || shown.Contains(id) || queue.Contains(id)) return;
            queue.Enqueue(id);
        }

        /// <summary>The rank tip for a card, or null if it isn't a special card.</summary>
        private static string RankTip(Rank r)
        {
            switch (r)
            {
                case Rank.Two: return "two";
                case Rank.Seven: return "seven";
                case Rank.Eight: return "eight";
                case Rank.Ten: return "ten";
                case Rank.Queen: return "queen";
                case Rank.Ace: return "ace";
                default: return null;
            }
        }

        // ------------------------------------------------------------------ hooks from the game

        /// <summary>Called for every event as it is animated.</summary>
        public void OnEvent(GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.GameStarted:
                    Want("welcome");
                    Want("stages");
                    break;
                case GameEventType.CardsPlayed:
                {
                    string tip = RankTip(e.Cards[0].Rank);
                    if (tip != null) Want(tip);
                    if (e.Player == local && game.Game.State.DrawPile.Count > 0) Want("draw");
                    break;
                }
                case GameEventType.SevenCallNeeded:
                    if (e.Player == local) Want("seven");
                    break;
                case GameEventType.AceChoiceNeeded:
                    if (e.Player == local) Want("ace");
                    break;
                case GameEventType.PlayerFinished:
                    if (game.Game.State.Phase != GamePhase.GameOver) Want("finish");
                    break;
            }
        }

        /// <summary>Every frame: tips about your own turn, and showing the next tip once the table is still.</summary>
        public void Tick()
        {
            if (skipped || game.Game == null || game.IsBusy) return;
            GameState s = game.Game.State;

            if (panel == null && s.Phase == GamePhase.Playing && s.Current == local && !s.Players[local].IsFinished)
            {
                PlayerState me = s.Players[local];
                Want("play");
                if (me.ActiveSource == PlaySource.FaceUp) Want("faceup");
                if (me.ActiveSource == PlaySource.FaceDown) Want("facedown");
                if (game.Game.MustPickUp(local)) Want("pickup");
                else if (me.ActiveSource != PlaySource.FaceDown)
                {
                    var cards = me.ActiveSource == PlaySource.Hand ? me.Hand : me.FaceUp.Where(c => c.HasValue).Select(c => c.Value).ToList();
                    var legal = cards.Where(c => game.Game.CheckRank(c.Rank).Legal).ToList();
                    foreach (var c in legal)
                    {
                        string tip = RankTip(c.Rank);
                        if (tip != null && tip != "seven" && tip != "ace") Want(tip); // 7 and Ace are explained when played
                    }
                    if (legal.GroupBy(c => c.Rank).Any(g => g.Count() > 1)) Want("multi");
                }
                if (s.Discard.Count >= 5 && shown.Count >= 6) Want("viewpile");
            }

            if (panel == null && queue.Count > 0) Show(queue.Dequeue());
        }

        // ------------------------------------------------------------------ the tip panel

        private void Show(string id)
        {
            shown.Add(id);
            tipNumber++;
            panel = UIKit.Layer(hud, "Tip", new Color(0f, 0f, 0f, 0.45f));
            panel.SetAsLastSibling();
            var box = UIKit.Panel(panel, new Vector2(1060f, 340f), new Vector2(0f, 90f));
            UIKit.Label(box.transform, Loc.F("Tip {0}", tipNumber), 30f, new Vector2(980f, 46f), new Vector2(0f, 128f),
                TextAlignmentOptions.Center, UIKit.Gold, true);
            var body = UIKit.Label(box.transform, Text(id), 32f, new Vector2(980f, 160f), new Vector2(0f, 26f), TextAlignmentOptions.Center, UIKit.Text);
            body.textWrappingMode = TextWrappingModes.Normal;
            UIKit.Button(box.transform, "Got it", new Vector2(260f, 76f), new Vector2(130f, -112f), Dismiss, true, 30f);
            UIKit.Button(box.transform, "Skip tips", new Vector2(240f, 64f), new Vector2(-150f, -112f), SkipAll, false, 24f);
            AudioManager.Play(Sfx.Button, 0.5f);
        }

        public void Dismiss()
        {
            if (panel != null) Object.Destroy(panel.gameObject);
            panel = null;
        }

        private void SkipAll()
        {
            skipped = true;
            queue.Clear();
            Dismiss();
        }
    }
}
