using System.Collections.Generic;

namespace Palace.Core
{
    public enum PlaySource { Hand, FaceUp, FaceDown }

    public enum SevenCall { None, Higher, Lower }

    public enum GamePhase
    {
        Playing,
        AwaitingSevenCall,   // a 7 was just played; that player must call HIGHER or LOWER
        AwaitingAceChoice,   // an Ace was just played; that player chooses "high" or "burn"
        GameOver,
        Swapping             // optional "swap before play": everyone swaps hand / face-up cards, then presses Ready
    }

    public sealed class PlayerState
    {
        public readonly int Index;
        public readonly string Name;
        public readonly bool IsBot;

        public readonly List<Card> Hand = new List<Card>();
        public readonly Card?[] FaceUp = new Card?[HouseRules.TableCards];
        public readonly Card?[] FaceDown = new Card?[HouseRules.TableCards];

        /// <summary>1 = first to finish. 0 = still playing.</summary>
        public int FinishPosition;

        /// <summary>Swap-before-play mode: this player has finished swapping.</summary>
        public bool SwapReady;

        public PlayerState(int index, string name, bool isBot)
        {
            Index = index;
            Name = name;
            IsBot = isBot;
        }

        public bool IsFinished => FinishPosition > 0;
        public int FaceUpCount => CountSlots(FaceUp);
        public int FaceDownCount => CountSlots(FaceDown);
        public int TotalCards => Hand.Count + FaceUpCount + FaceDownCount;

        /// <summary>Where this player must play from: hand first, then face-up, then face-down.</summary>
        public PlaySource ActiveSource
        {
            get
            {
                if (Hand.Count > 0) return PlaySource.Hand;
                if (FaceUpCount > 0) return PlaySource.FaceUp;
                return PlaySource.FaceDown;
            }
        }

        private static int CountSlots(Card?[] slots)
        {
            int n = 0;
            foreach (var c in slots) if (c.HasValue) n++;
            return n;
        }
    }

    /// <summary>
    /// Everything about a game in progress. Plain data: the engine (PalaceGame) is the only
    /// thing that changes it, which keeps the rules in one place.
    /// </summary>
    public sealed class GameState
    {
        public readonly List<PlayerState> Players = new List<PlayerState>();
        public readonly List<Card> DrawPile = new List<Card>();
        public readonly List<Card> Discard = new List<Card>();
        public readonly List<int> FinishOrder = new List<int>();

        public int TotalCards;
        public int BurnedCount;
        public int Current;
        public int Direction = 1;              // +1 clockwise, -1 anticlockwise
        public SevenCall ActiveSeven = SevenCall.None;
        public GamePhase Phase = GamePhase.Playing;
        public int TurnNumber;

        // Pending choice details (only meaningful in the Awaiting* phases)
        public int PendingPlayer = -1;
        public int PendingCount;

        // Ace played as a high card stands for a chosen value (e.g. "this Ace counts as a Queen").
        public Rank? AceValue;
        public readonly List<Rank> PendingAceValues = new List<Rank>();
        public bool PendingAceBurnAllowed;

        public Rank? TopRank => Discard.Count > 0 ? Discard[Discard.Count - 1].Rank : (Rank?)null;

        /// <summary>The value the next card must match or beat (an Ace counts as its chosen value).</summary>
        public Rank? EffectiveTopRank => TopRank == Rank.Ace && AceValue.HasValue ? AceValue : TopRank;

        public int ActivePlayerCount
        {
            get
            {
                int n = 0;
                foreach (var p in Players) if (!p.IsFinished) n++;
                return n;
            }
        }

        /// <summary>The player who must act now (the chooser during a pending choice).</summary>
        public int ActingPlayer => Phase == GamePhase.AwaitingSevenCall || Phase == GamePhase.AwaitingAceChoice
            ? PendingPlayer
            : Current;
    }
}
