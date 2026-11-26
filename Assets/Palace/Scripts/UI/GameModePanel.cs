using System;
using Palace.Core;
using TMPro;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// The "Game mode" pop-up: four one-tap presets (Classic, Big Battle, Strategy, Chaos) and the
    /// options behind them (decks, cards in hand, four of a kind burns, Swap before play). Opened from
    /// the Single Player screen and by the host of an online room.
    /// </summary>
    public static class GameModePanel
    {
        public const string FourInfoTitle = "Four of a kind burns";
        public const string FourInfoText =
            "Four cards of the same rank in a row on top of the pile burn it, even if different players put them there.\n\n" +
            "Whoever completes the four goes again, just like after a 10.\n\n" +
            "Turn it off for longer games where only 10s burn the pile.";

        public const string DecksInfoTitle = "Decks";
        public const string DecksInfoText =
            "How many 52-card decks are shuffled together. Auto uses one deck, or two for 6 or more players.\n\n" +
            "More decks mean a longer game with more 10s, 2s and Aces, and four of a kinds come up more often.\n\n" +
            "If the table needs more cards for the deal, another deck is added automatically.";

        /// <param name="players">How many players will be at the table (for the summary line).</param>
        /// <param name="changed">Called after every change, e.g. to update a label or tell the room.</param>
        public static void Open(Transform parent, Func<int> players, Action changed)
        {
            var canvas = parent.GetComponentInParent<Canvas>();
            var root = canvas != null ? canvas.rootCanvas.transform : parent;
            var layer = UIKit.Layer(root, "GameModePanel", UIKit.Scrim);
            layer.SetAsLastSibling();
            Build(layer, players, changed);
        }

        private static void Build(RectTransform layer, Func<int> players, Action changed)
        {
            for (int i = layer.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(layer.GetChild(i).gameObject);

            void Changed(bool rebuild)
            {
                GameSettings.Save();
                changed?.Invoke();
                if (rebuild) Build(layer, players, changed);
            }

            var panel = UIKit.Panel(layer, new Vector2(1060f, 900f), Vector2.zero).transform;
            UIKit.Label(panel, "Game mode", 46f, new Vector2(900f, 64f), new Vector2(0f, 385f), TextAlignmentOptions.Center, UIKit.Gold, true);

            HouseRules rules = GameModes.BuildRules();
            int preset = GameModes.PresetOf(rules);

            // One-tap presets. The current one is highlighted (none for a custom mix).
            float w = 240f;
            for (int i = 0; i < GameModes.PresetNames.Length; i++)
            {
                int index = i;
                UIKit.Button(panel, GameModes.PresetNames[i], new Vector2(w - 10f, 76f),
                    new Vector2((i - 1.5f) * w, 295f), () => { GameModes.ApplyPreset(index); Changed(true); }, i == preset, 26f);
            }
            var note = UIKit.Label(panel, preset >= 0 ? GameModes.PresetNotes[preset] : "Custom: your own mix of the options below.",
                24f, new Vector2(960f, 70f), new Vector2(0f, 215f), TextAlignmentOptions.Center, UIKit.Muted);
            note.textWrappingMode = TextWrappingModes.Normal;

            UIBuild.Image("Line", panel, null, new Color(1f, 1f, 1f, 0.12f), new Vector2(940f, 3f), new Vector2(0f, 165f));

            // The options themselves.
            Row(panel, "Decks", 95f, DecksInfoTitle, DecksInfoText);
            UIKit.Segmented(panel, GameModes.DeckNames, GameModes.Decks, new Vector2(520f, 64f), new Vector2(215f, 95f),
                i => { GameModes.Decks = i; Changed(true); });

            Row(panel, "Cards in hand", 5f, null, null);
            UIKit.Segmented(panel, GameModes.HandNames, GameModes.HandCards - GameModes.MinHand, new Vector2(520f, 64f), new Vector2(215f, 5f),
                i => { GameModes.HandCards = GameModes.MinHand + i; Changed(true); });

            Row(panel, "Four of a kind burns", -85f, FourInfoTitle, FourInfoText);
            UIKit.Segmented(panel, GameSettings.OnOffNames, GameModes.FourOfAKindBurns ? 1 : 0, new Vector2(520f, 64f), new Vector2(215f, -85f),
                i => { GameModes.FourOfAKindBurns = i == 1; Changed(true); });

            Row(panel, "Swap before play", -175f, UIKit.SwapInfoTitle, UIKit.SwapInfoText);
            UIKit.Segmented(panel, GameSettings.OnOffNames, GameModes.Swap ? 1 : 0, new Vector2(520f, 64f), new Vector2(215f, -175f),
                i => { GameModes.Swap = i == 1; Changed(true); });

            // What that means for this table.
            int n = Mathf.Clamp(players != null ? players() : 4, 2, 10);
            int decks = rules.DecksFor(n);
            string table = (decks == 1 ? Loc.F("{0} players, 1 deck ({1} cards)", n, 52) : Loc.F("{0} players, {1} decks ({2} cards)", n, decks, decks * 52));
            UIKit.Label(panel, table, 26f, new Vector2(940f, 44f), new Vector2(0f, -262f), TextAlignmentOptions.Center, UIKit.Text, false).text = table;

            UIKit.Button(panel, "Done", new Vector2(300f, 80f), new Vector2(0f, -360f), () => UnityEngine.Object.Destroy(layer.gameObject), true, 32f);
        }

        private static void Row(Transform panel, string label, float y, string infoTitle, string infoText)
        {
            UIKit.Label(panel, label, 28f, new Vector2(400f, 56f), new Vector2(-290f, y), TextAlignmentOptions.Left, UIKit.Muted, true);
            if (infoTitle != null) UIKit.InfoButton(panel, new Vector2(-75f, y), infoTitle, infoText);
        }
    }
}
