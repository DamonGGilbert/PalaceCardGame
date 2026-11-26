using Palace.Core;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Game mode options (saved on the device): how many decks, how many cards in hand, whether four of
    /// a kind burns, and Swap before play. Used for single player games and by the host of an online
    /// game. Classic is the school rules and the default.
    /// </summary>
    public static class GameModes
    {
        public static readonly string[] DeckNames = { "Auto", "1", "2", "3" };
        public static readonly string[] HandNames = { "3", "4", "5" };
        public const int MinHand = 3, MaxHand = 5, MaxDecks = 3;

        public static readonly string[] PresetNames = { "Classic", "Big Battle", "Strategy", "Chaos" };

        public static readonly string[] PresetNotes =
        {
            "The school rules: one deck (two for big tables), 3 cards in hand.",
            "Two decks shuffled together: a longer game with twice the 10s and 2s, and more four of a kinds.",
            "Swap before play, and 4 cards in hand for more choice every turn.",
            "Three decks, 5 cards in hand and Swap before play. Anything can happen!"
        };

        // decks (0 = auto), cards in hand, four of a kind burns, swap before play
        private static readonly (int decks, int hand, bool four, bool swap)[] Presets =
        {
            (0, 3, true, false),
            (2, 3, true, false),
            (0, 4, true, true),
            (3, 5, true, true)
        };

        /// <summary>0 = automatic, otherwise 1-3 decks.</summary>
        public static int Decks
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("palace.mode.decks", 0), 0, MaxDecks);
            set => PlayerPrefs.SetInt("palace.mode.decks", Mathf.Clamp(value, 0, MaxDecks));
        }

        public static int HandCards
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("palace.mode.hand", HouseRules.HandSize), MinHand, MaxHand);
            set => PlayerPrefs.SetInt("palace.mode.hand", Mathf.Clamp(value, MinHand, MaxHand));
        }

        public static bool FourOfAKindBurns
        {
            get => PlayerPrefs.GetInt("palace.mode.four", 1) == 1;
            set => PlayerPrefs.SetInt("palace.mode.four", value ? 1 : 0);
        }

        public static bool Swap
        {
            get => GameSettings.SwapMode;
            set => GameSettings.SwapMode = value;
        }

        /// <summary>The rules for the next game from the saved options.</summary>
        public static HouseRules BuildRules() => new HouseRules
        {
            DeckCount = Decks,
            HandCards = HandCards,
            FourOfAKindBurns = FourOfAKindBurns,
            SwapBeforePlay = Swap
        };

        public static void ApplyPreset(int index)
        {
            if (index < 0 || index >= Presets.Length) return;
            var p = Presets[index];
            Decks = p.decks;
            HandCards = p.hand;
            FourOfAKindBurns = p.four;
            Swap = p.swap;
            PlayerPrefs.Save();
        }

        /// <summary>Which preset these rules match, or -1 for a custom mix.</summary>
        public static int PresetOf(HouseRules r)
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                var p = Presets[i];
                if (r.DeckCount == p.decks && r.HandCards == p.hand && r.FourOfAKindBurns == p.four && r.SwapBeforePlay == p.swap) return i;
            }
            return -1;
        }

        /// <summary>"Classic", "Big Battle"... or "Custom" (translated).</summary>
        public static string Name(HouseRules r)
        {
            int i = PresetOf(r);
            return Loc.T(i >= 0 ? PresetNames[i] : "Custom");
        }

        /// <summary>A one-line summary for this many players, e.g. "Big Battle: 2 decks, 3 cards in hand".</summary>
        public static string Summary(HouseRules r, int players)
        {
            int decks = r.DecksFor(players);
            string text = Name(r) + ": " + (decks == 1 ? Loc.T("1 deck") : Loc.F("{0} decks", decks)) + ", " + Loc.F("{0} cards in hand", r.HandCards);
            if (r.SwapBeforePlay) text += ", " + Loc.T("swap first");
            if (!r.FourOfAKindBurns) text += ", " + Loc.T("no four of a kind burns");
            return text;
        }
    }

    /// <summary>Names for computer players, picked at random so every table feels different.</summary>
    public static class BotNames
    {
        private static readonly string[] All =
        {
            "Alex", "Jordan", "Sam", "Charlie", "Riley", "Taylor", "Morgan", "Casey", "Jamie", "Robin",
            "Frankie", "Billie", "Ash", "Max", "Ellis", "Rowan", "Quinn", "Harley", "Drew", "Jesse",
            "Mia", "Leo", "Ava", "Noah", "Ella", "Oscar", "Isla", "Theo", "Ruby", "Finn",
            "Poppy", "Archie", "Freya", "Alfie", "Daisy", "Arlo", "Nora", "Hugo", "Ivy", "Reggie",
            "Maya", "Kai", "Zara", "Milo", "Lola", "Ezra", "Iris", "Rory", "Hazel", "Otis",
            "Priya", "Arjun", "Aisha", "Omar", "Yusuf", "Layla", "Kofi", "Amara", "Tariq", "Nia",
            "Mei", "Kenji", "Hana", "Ravi", "Sofia", "Mateo", "Lucia", "Diego", "Elena", "Pablo",
            "Zofia", "Kuba", "Ola", "Marek", "Ania", "Bruno", "Greta", "Lars", "Ingrid", "Sven",
            "Niamh", "Cian", "Siobhan", "Callum", "Morag", "Dylan", "Bethan", "Gethin", "Fern", "Duncan",
            "Bea", "Stan", "Dot", "Bernie", "Mabel", "Albie", "Peggy", "Wilf", "Edie", "Ted"
        };

        /// <summary>Some different names, none of them in <paramref name="avoid"/> (e.g. the players' own names).</summary>
        public static string[] Pick(int count, System.Random rng, params string[] avoid)
        {
            var pool = new System.Collections.Generic.List<string>(All);
            if (avoid != null)
                foreach (var a in avoid)
                    if (!string.IsNullOrEmpty(a)) pool.RemoveAll(n => string.Equals(n, a.Trim(), System.StringComparison.OrdinalIgnoreCase));
            var result = new string[count];
            for (int i = 0; i < count; i++)
            {
                if (pool.Count == 0) { result[i] = "Bot " + (i + 1); continue; }
                int k = rng.Next(pool.Count);
                result[i] = pool[k];
                pool.RemoveAt(k);
            }
            return result;
        }

        /// <summary>One name not already used by anyone in <paramref name="taken"/>.</summary>
        public static string PickOne(System.Random rng, System.Collections.Generic.IEnumerable<string> taken)
        {
            var avoid = new System.Collections.Generic.List<string>(taken);
            return Pick(1, rng, avoid.ToArray())[0];
        }
    }
}
