using System;

namespace Palace.Core
{
    public enum Suit
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3
    }

    /// <summary>
    /// Card ranks. The numeric value IS the playing strength, so "higher or equal"
    /// checks are a simple integer comparison (Ace = 14 is the highest).
    /// </summary>
    public enum Rank
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14
    }

    /// <summary>
    /// One runtime playing card. Pure data: no Unity types, so the rules engine
    /// can be tested (and later run on a server) without any visuals.
    /// Id is unique within a game, which allows two physical decks to be combined.
    /// </summary>
    public readonly struct Card : IEquatable<Card>
    {
        public readonly int Id;
        public readonly Rank Rank;
        public readonly Suit Suit;

        public Card(int id, Rank rank, Suit suit)
        {
            Id = id;
            Rank = rank;
            Suit = suit;
        }

        public bool IsRed => Suit == Suit.Hearts || Suit == Suit.Diamonds;

        public bool IsCourt => Rank == Rank.Jack || Rank == Rank.Queen || Rank == Rank.King;

        public string RankLabel => RankToLabel(Rank);

        public static string RankToLabel(Rank rank)
        {
            switch (rank)
            {
                case Rank.Jack: return "J";
                case Rank.Queen: return "Q";
                case Rank.King: return "K";
                case Rank.Ace: return "A";
                default: return ((int)rank).ToString();
            }
        }

        public static char SuitToSymbol(Suit suit)
        {
            switch (suit)
            {
                case Suit.Clubs: return '♣';
                case Suit.Diamonds: return '♦';
                case Suit.Hearts: return '♥';
                default: return '♠';
            }
        }

        public bool Equals(Card other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => RankLabel + SuitToSymbol(Suit);
    }
}
