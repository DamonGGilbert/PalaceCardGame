using System;
using System.Collections.Generic;
using System.Linq;

namespace Palace.Core
{
    public enum BotMoveType { PlayCards, PlayFaceDown, PickUp, DeclareSeven, ChooseAce }

    /// <summary>How well a computer player plays. The rules are the same at every level.</summary>
    public enum BotLevel { Easy = 0, Normal = 1, Hard = 2 }

    public struct BotMove
    {
        public BotMoveType Type;
        public PlaySource Source;
        public List<int> CardIds;
        public int Slot;
        public SevenCall Call;
        public bool Burn;
        public Rank AceValue;
    }

    /// <summary>
    /// The computer players. They only use information a human in that seat could see (their
    /// own hand, everyone's face-up cards and the pile, which anyone can look through), and every
    /// move goes through the same rules engine as a human's, so bots can't cheat.
    ///
    ///  Easy:   often plays a random legal card and wastes its 2s, 10s and Aces.
    ///  Normal: plays its cheapest card and saves the special cards (the original bot).
    ///  Hard:   saves special cards for when it needs them, plays every copy of a card to empty
    ///          its hand faster, skips or reverses away from a player who is about to finish,
    ///          counts the cards it has seen to decide HIGHER or LOWER on a 7, and burns with Aces.
    /// </summary>
    public static class BotBrain
    {
        public static BotMove Decide(PalaceGame game, int player, Random rng) => Decide(game, player, rng, BotLevel.Normal);

        public static BotMove Decide(PalaceGame game, int player, Random rng, BotLevel level)
        {
            GameState s = game.State;

            if (s.Phase == GamePhase.AwaitingSevenCall)
                return new BotMove { Type = BotMoveType.DeclareSeven, Call = ChooseSevenCall(game, player, rng, level) };

            if (s.Phase == GamePhase.AwaitingAceChoice)
                return ChooseAce(game, player, rng, level);

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
            if (legal.Count == 0) return new BotMove { Type = BotMoveType.PickUp, Slot = FaceUpToTake(game, me, level, rng) };

            var ranks = legal.Select(c => c.Rank).Distinct().OrderBy(Cost).ToList();

            // Four of a kind: if our cards complete four in a row on the pile, burn it and go again.
            // (Easy bots don't think about it; they only manage it by luck.)
            if (level != BotLevel.Easy && game.Rules.FourOfAKindBurns)
            {
                foreach (Rank r in ranks)
                {
                    int have = available.Count(c => c.Rank == r);
                    if (RunAfter(game, r, have) >= 4)
                        return new BotMove { Type = BotMoveType.PlayCards, Source = source, CardIds = available.Where(c => c.Rank == r).Select(c => c.Id).ToList() };
                }
            }

            Rank best;
            List<int> ids;
            switch (level)
            {
                case BotLevel.Easy:
                    // Plays without much thought: often any legal card, and often just one of a set.
                    best = rng.NextDouble() < 0.5 ? ranks[rng.Next(ranks.Count)] : ranks[0];
                    ids = available.Where(c => c.Rank == best).Select(c => c.Id).ToList();
                    if (ids.Count > 1 && rng.NextDouble() < 0.6) ids = ids.Take(1).ToList();
                    break;

                case BotLevel.Hard:
                    best = HardPick(game, player, ranks, rng);
                    ids = available.Where(c => c.Rank == best).Select(c => c.Id).ToList();
                    // Keep spare 2s, 10s and Aces for later; play every copy of anything else.
                    if (IsSpecial(best) && ids.Count > 1) ids = ids.Take(1).ToList();
                    break;

                default:
                    // Usually the cheapest legal rank, saving 2s, 10s and Aces. A little variety (like a
                    // real player) also stops bots repeating the same loop forever.
                    best = rng.NextDouble() < 0.15 ? ranks[rng.Next(ranks.Count)] : ranks[0];
                    ids = available.Where(c => c.Rank == best).Select(c => c.Id).ToList();
                    if (ids.Count > 1 && rng.NextDouble() < 0.2) ids = ids.Take(1 + rng.Next(ids.Count - 1)).ToList();
                    break;
            }
            return new BotMove { Type = BotMoveType.PlayCards, Source = source, CardIds = ids };
        }

        public static CommandResult Execute(PalaceGame game, int player, BotMove move)
        {
            switch (move.Type)
            {
                case BotMoveType.PlayCards: return game.PlayCards(player, move.Source, move.CardIds);
                case BotMoveType.PlayFaceDown: return game.PlayFaceDown(player, move.Slot);
                case BotMoveType.PickUp: return game.PickUp(player, move.Slot);
                case BotMoveType.DeclareSeven: return game.DeclareSeven(player, move.Call);
                default: return game.ChooseAce(player, move.Burn, move.AceValue);
            }
        }

        // ------------------------------------------------------------------ swap before play

        /// <summary>
        /// Swap-before-play mode: which hand cards to swap with which face-up slots. Face-up cards are
        /// played late in the game, so Normal and Hard bots put their strongest cards (10s, 2s, Aces,
        /// then high cards) face up and keep low cards in hand. Easy bots usually don't bother.
        /// </summary>
        public static List<(int handCardId, int faceUpSlot)> DecideSwaps(PlayerState p, BotLevel level, Random rng)
        {
            var swaps = new List<(int, int)>();
            if (level == BotLevel.Easy && rng.NextDouble() < 0.6) return swaps;

            var hand = new List<Card>(p.Hand);
            var up = (Card?[])p.FaceUp.Clone();
            for (int guard = 0; guard < 6; guard++)
            {
                int bestHand = -1, worstSlot = -1;
                for (int i = 0; i < hand.Count; i++)
                    if (bestHand < 0 || SwapValue(hand[i].Rank) > SwapValue(hand[bestHand].Rank)) bestHand = i;
                for (int k = 0; k < up.Length; k++)
                    if (up[k].HasValue && (worstSlot < 0 || SwapValue(up[k].Value.Rank) < SwapValue(up[worstSlot].Value.Rank))) worstSlot = k;
                if (bestHand < 0 || worstSlot < 0 || SwapValue(hand[bestHand].Rank) <= SwapValue(up[worstSlot].Value.Rank)) break;

                swaps.Add((hand[bestHand].Id, worstSlot));
                Card h = hand[bestHand];
                hand[bestHand] = up[worstSlot].Value;
                up[worstSlot] = h;
            }
            return swaps;
        }

        /// <summary>
        /// Picking up while on face-up cards (Q11): which face-up card goes into the hand with the pile.
        /// The weakest one: it's easier to get rid of from the hand than to be stuck with at the end.
        /// Returns -1 when no choice is needed.
        /// </summary>
        private static int FaceUpToTake(PalaceGame game, PlayerState me, BotLevel level, Random rng)
        {
            if (!game.PickUpNeedsFaceUpChoice(me.Index)) return -1;
            var slots = Enumerable.Range(0, me.FaceUp.Length).Where(k => me.FaceUp[k].HasValue).ToList();
            if (level == BotLevel.Easy) return slots[rng.Next(slots.Count)];
            return slots.OrderBy(k => SwapValue(me.FaceUp[k].Value.Rank)).First();
        }

        private static int SwapValue(Rank r) => r == Rank.Ten ? 30 : r == Rank.Two ? 29 : r == Rank.Ace ? 28 : (int)r;

        /// <summary>How long the run of same-rank cards on top of the pile would be after playing 'count' cards of rank r.</summary>
        private static int RunAfter(PalaceGame game, Rank r, int count) =>
            count + (game.State.TopRank == r ? game.TopRunLength() : 0);

        private static bool IsSpecial(Rank r) => r == Rank.Two || r == Rank.Ten || r == Rank.Ace;

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

        /// <summary>A player with this few cards left is close to finishing.</summary>
        private static bool AboutToFinish(PlayerState p) => !p.IsFinished && p.TotalCards <= 3;

        // ------------------------------------------------------------------ hard: which card

        private static Rank HardPick(PalaceGame game, int player, List<Rank> ranks, Random rng)
        {
            GameState s = game.State;
            var normal = ranks.Where(r => !IsSpecial(r)).ToList();

            // Very long games are usually two players passing the same cards back and forth:
            // play less predictably until the loop breaks.
            if (rng.NextDouble() < (s.TurnNumber > 250 ? 0.35 : 0.04)) return ranks[rng.Next(ranks.Count)];

            if (normal.Count > 0)
            {
                PlayerState next = s.Players[game.NextActiveFrom(player)];
                int others = s.ActivePlayerCount - 1;

                // Someone about to go out next: skip them with an 8, or turn play away with a Queen
                // (only useful if the player on the other side isn't also about to finish).
                if (AboutToFinish(next) && others >= 2)
                {
                    if (normal.Contains(Rank.Eight)) return Rank.Eight;
                    if (normal.Contains(Rank.Queen) && !AboutToFinish(s.Players[PreviousActive(game, player)])) return Rank.Queen;
                }

                // The next player is down to face-up cards we can all see: play something none of
                // them can beat, so they have to pick up.
                if (next.Hand.Count == 0 && next.FaceUpCount > 0
                    && next.FaceUp.All(c => !c.HasValue || !IsSpecial(c.Value.Rank)))
                {
                    int theirBest = next.FaceUp.Where(c => c.HasValue).Max(c => (int)c.Value.Rank);
                    foreach (Rank r in normal)
                        if ((int)r > theirBest && r != Rank.Seven) return r;
                }

                // Otherwise the cheapest normal card, but don't leave three of a kind on top for the next
                // player to finish off with a fourth (and burn the pile) if another card will do.
                if (game.Rules.FourOfAKindBurns)
                {
                    PlayerState me = s.Players[player];
                    var hand = me.ActiveSource == PlaySource.Hand ? me.Hand : me.FaceUp.Where(c => c.HasValue).Select(c => c.Value).ToList();
                    foreach (Rank r in normal)
                        if (RunAfter(game, r, hand.Count(c => c.Rank == r)) != 3) return r;
                }
                return normal[0];
            }

            // Only special cards can be played: burn a big pile (and go again), otherwise reset with a 2.
            if (ranks.Contains(Rank.Ten) && (s.Discard.Count >= 3 || !ranks.Contains(Rank.Two))) return Rank.Ten;
            if (ranks.Contains(Rank.Two)) return Rank.Two;
            if (ranks.Contains(Rank.Ten)) return Rank.Ten;
            return ranks[0];
        }

        private static int PreviousActive(PalaceGame game, int player)
        {
            GameState s = game.State;
            int n = s.Players.Count;
            for (int k = 1; k <= n; k++)
            {
                int j = (((player - s.Direction * k) % n) + n) % n;
                if (!s.Players[j].IsFinished) return j;
            }
            return player;
        }

        // ------------------------------------------------------------------ aces

        private static BotMove ChooseAce(PalaceGame game, int player, Random rng, BotLevel level)
        {
            GameState s = game.State;
            List<Rank> values = s.PendingAceValues;
            bool burn;
            Rank value;

            switch (level)
            {
                case BotLevel.Easy:
                    burn = s.PendingAceBurnAllowed && (values.Count == 0 || rng.NextDouble() < 0.5);
                    value = values.Count > 0 ? values[rng.Next(values.Count)] : Rank.Ace;
                    break;

                case BotLevel.Hard:
                {
                    // Burning whenever allowed wins the most games in testing (the pile is gone and you go again).
                    burn = s.PendingAceBurnAllowed;
                    value = values.Count > 0 ? values.Max() : Rank.Ace;
                    break;
                }

                default:
                    burn = s.PendingAceBurnAllowed && (values.Count == 0 || s.Discard.Count >= 4 || rng.NextDouble() < 0.3);
                    // As a high card, usually make it as hard as possible for the next player.
                    value = values.Count > 0
                        ? (rng.NextDouble() < 0.8 ? values.Max() : values[rng.Next(values.Count)])
                        : Rank.Ace;
                    break;
            }
            return new BotMove { Type = BotMoveType.ChooseAce, Burn = burn, AceValue = value };
        }

        // ------------------------------------------------------------------ sevens

        private static SevenCall ChooseSevenCall(PalaceGame game, int player, Random rng, BotLevel level)
        {
            if (level == BotLevel.Easy) return rng.NextDouble() < 0.5 ? SevenCall.Lower : SevenCall.Higher;

            PlayerState next = game.State.Players[game.NextActiveFrom(player)];

            // If the next player is down to face-up cards, aim the call at the side they can't play.
            // (Not every time: the "perfect" call every time can trap players in an endless 7 loop.)
            double aim = level == BotLevel.Hard ? 0.9 : 0.75;
            if (next.Hand.Count == 0 && next.FaceUpCount > 0 && rng.NextDouble() < aim)
            {
                int high = next.FaceUp.Count(c => c.HasValue && Beats(c.Value.Rank, SevenCall.Higher));
                int low = next.FaceUp.Count(c => c.HasValue && Beats(c.Value.Rank, SevenCall.Lower));
                return high <= low ? SevenCall.Higher : SevenCall.Lower;
            }

            if (level == BotLevel.Hard && rng.NextDouble() < 0.9)
            {
                // Count the cards nobody can see yet (everything minus our hand, the face-up cards and
                // the pile) and pick the side the next player is least likely to be able to play.
                int[] unseen = UnseenCounts(game, player);
                int higher = 0, lower = 0;
                for (int r = 3; r <= 14; r++)
                {
                    if (Beats((Rank)r, SevenCall.Higher)) higher += unseen[r];
                    if (Beats((Rank)r, SevenCall.Lower)) lower += unseen[r];
                }
                return lower <= higher ? SevenCall.Lower : SevenCall.Higher;
            }

            return rng.NextDouble() < 0.6 ? SevenCall.Lower : SevenCall.Higher;
        }

        /// <summary>Can a card of this rank be played on a 7 with this call? (2s always can, so they're left out.)</summary>
        private static bool Beats(Rank r, SevenCall call)
        {
            if (r == Rank.Ace) return true; // an Ace can stand for a low or a high value
            if (call == SevenCall.Higher) return (int)r > 7;
            return (int)r < 7 && r != Rank.Two;
        }

        private static int[] UnseenCounts(PalaceGame game, int player)
        {
            GameState s = game.State;
            var counts = new int[15];
            int decks = Math.Max(1, s.TotalCards / 52);
            for (int r = 2; r <= 14; r++) counts[r] = 4 * decks;
            foreach (var c in s.Players[player].Hand) counts[(int)c.Rank]--;
            foreach (var c in s.Discard) counts[(int)c.Rank]--;
            foreach (var p in s.Players)
                foreach (var c in p.FaceUp)
                    if (c.HasValue) counts[(int)c.Value.Rank]--;
            for (int r = 2; r <= 14; r++) counts[r] = Math.Max(0, counts[r]);
            return counts;
        }
    }
}
