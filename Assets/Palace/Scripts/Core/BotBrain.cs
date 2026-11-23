using System;
using System.Collections.Generic;
using System.Linq;

namespace Palace.Core
{
    public enum BotMoveType { PlayCards, PlayFaceDown, PickUp, DeclareSeven, ChooseAce }

    public struct BotMove
    {
        public BotMoveType Type;
        public PlaySource Source;
        public List<int> CardIds;
        public int Slot;
        public SevenCall Call;
        public bool Burn;
    }

    /// <summary>
    /// A simple, readable computer player. It only uses information a human in that seat
    /// could see (its own hand plus everyone's face-up cards), and every move goes through
    /// the same rules engine as a human's, so bots can't cheat.
    /// </summary>
    public static class BotBrain
    {
        public static BotMove Decide(PalaceGame game, int player, Random rng)
        {
            GameState s = game.State;

            if (s.Phase == GamePhase.AwaitingSevenCall)
                return new BotMove { Type = BotMoveType.DeclareSeven, Call = ChooseSevenCall(game, player, rng) };

            if (s.Phase == GamePhase.AwaitingAceChoice)
                return new BotMove { Type = BotMoveType.ChooseAce, Burn = s.Discard.Count >= 4 || rng.NextDouble() < 0.3 };

            PlayerState me = s.Players[player];
            PlaySource source = me.ActiveSource;

            if (source == PlaySource.FaceDown)
            {
                var slots = Enumerable.Range(0, me.FaceDown.Length).Where(i => me.FaceDown[i].HasValue).ToList();
                return new BotMove { Type = BotMoveType.PlayFaceDown, Slot = slots[rng.Next(slots.Count)] };
            }

            List<Card> available = source == PlaySource.Hand
                ? new List<Card>(me.Hand)
                : me.FaceUp.Where(c => c.HasValue).Select(c => c.Value).ToList();

            var legal = available.Where(c => game.CheckRank(c.Rank).Legal).ToList();
            if (legal.Count == 0) return new BotMove { Type = BotMoveType.PickUp };

            // Usually play the cheapest legal rank, saving 2s, 10s and Aces for when they're needed.
            // A little variety (like a real player) also stops bots repeating the same loop forever.
            var ranks = legal.Select(c => c.Rank).Distinct().OrderBy(Cost).ToList();
            Rank best = rng.NextDouble() < 0.15 ? ranks[rng.Next(ranks.Count)] : ranks[0];
            var ids = available.Where(c => c.Rank == best).Select(c => c.Id).ToList();
            if (ids.Count > 1 && rng.NextDouble() < 0.2) ids = ids.Take(1 + rng.Next(ids.Count - 1)).ToList();
            return new BotMove { Type = BotMoveType.PlayCards, Source = source, CardIds = ids };
        }

        public static CommandResult Execute(PalaceGame game, int player, BotMove move)
        {
            switch (move.Type)
            {
                case BotMoveType.PlayCards: return game.PlayCards(player, move.Source, move.CardIds);
                case BotMoveType.PlayFaceDown: return game.PlayFaceDown(player, move.Slot);
                case BotMoveType.PickUp: return game.PickUp(player);
                case BotMoveType.DeclareSeven: return game.DeclareSeven(player, move.Call);
                default: return game.ChooseAce(player, move.Burn);
            }
        }

        private static int Cost(Rank rank)
        {
            switch (rank)
            {
                case Rank.Two: return 20;
                case Rank.Ten: return 21;
                case Rank.Ace: return 22;
                default: return (int)rank;
            }
        }

        private static SevenCall ChooseSevenCall(PalaceGame game, int player, Random rng)
        {
            PlayerState next = game.State.Players[game.NextActiveFrom(player)];

            // If the next player is down to face-up cards, aim the call at the side they can't play.
            if (next.Hand.Count == 0 && next.FaceUpCount > 0)
            {
                int high = next.FaceUp.Count(c => c.HasValue && (int)c.Value.Rank > 7 && c.Value.Rank != Rank.Ten);
                int low = next.FaceUp.Count(c => c.HasValue && (int)c.Value.Rank < 7);
                return high <= low ? SevenCall.Higher : SevenCall.Lower;
            }

            return rng.NextDouble() < 0.6 ? SevenCall.Lower : SevenCall.Higher;
        }
    }
}
