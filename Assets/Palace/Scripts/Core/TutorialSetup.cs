using System.Collections.Generic;

namespace Palace.Core
{
    /// <summary>
    /// The guided first game: you plus two easy computer players, with the deck stacked so the
    /// lesson starts well. You go first holding a pair of 4s and a 7, and the first few cards off
    /// the deck bring a 10, 8, Queen, 2 and Ace into play early. Everything after that is a
    /// normal shuffled game with the normal rules.
    /// </summary>
    public static class TutorialSetup
    {
        public const int Players = 3;
        public static readonly string[] BotNames = { "Sam", "Alex" };

        // You / Sam / Alex, one card each per round.
        private static readonly Rank[] Deal =
        {
            // face-down slots 1, 2, 3
            Rank.Nine, Rank.Four, Rank.Five,
            Rank.Jack, Rank.Three, Rank.Three,
            Rank.Six, Rank.Three, Rank.Four,
            // face-up slots 1, 2, 3
            Rank.King, Rank.Six, Rank.Five,
            Rank.King, Rank.Ten, Rank.Eight,
            Rank.Nine, Rank.Jack, Rank.Queen,
            // hands
            Rank.Four, Rank.Five, Rank.Six,
            Rank.Four, Rank.Six, Rank.Nine,
            Rank.Seven, Rank.Jack, Rank.King,
            // the first draws
            Rank.Ten, Rank.Three, Rank.Eight, Rank.Queen, Rank.Two, Rank.Ace, Rank.Seven, Rank.Nine,
            Rank.Two, Rank.Ace, Rank.Eight, Rank.Queen
        };

        public static PalaceGame Create(string playerName, HouseRules rules, int seed)
        {
            var names = new List<string> { playerName };
            var isBot = new List<bool> { false };
            foreach (var n in BotNames)
            {
                names.Add(n);
                isBot.Add(true);
            }
            return PalaceGame.Create(names, isBot, rules, seed, Deal, 0);
        }
    }
}
