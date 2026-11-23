namespace Palace.Core
{
    /// <summary>
    /// The adjustable parts of the school rules. Every option here answers one of the
    /// open questions in Docs/DESIGN.md. The defaults are the proposed answers, and each
    /// can be changed in one place without touching the rules engine.
    /// </summary>
    public sealed class HouseRules
    {
        public const int TableCards = 3;   // face-down and face-up cards per player
        public const int HandSize = 3;     // cards kept in hand while the deck lasts

        /// <summary>Q1: from this many players a second 52-card deck is shuffled in.</summary>
        public int SecondDeckFromPlayers = 6;

        /// <summary>Q2: a 10 (and an Ace used as a 10) can be played on any card.</summary>
        public bool TenPlayableOnAnything = true;

        /// <summary>Q3: may a 10 / Ace-as-10 burn when the previous player called LOWER on a 7?</summary>
        public bool BurnAllowedOnLowerSeven = false;

        /// <summary>
        /// An Ace played as a high card stands for a value the player picks. true = the value may
        /// equal the top card (like the normal "equal or higher" rule); false = it must be strictly higher.
        /// </summary>
        public bool AceValueMayEqualTop = true;

        /// <summary>Q20: four cards of the same rank burn the pile. Not part of the school rules.</summary>
        public bool FourOfAKindBurns = false;

        public int DecksFor(int playerCount) => playerCount >= SecondDeckFromPlayers ? 2 : 1;
    }
}
