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
        public const int HandSize = 3;     // the school rule: cards kept in hand while the deck lasts

        /// <summary>
        /// Game mode option: cards kept in hand while the deck lasts (3 = the school rule; 4 or 5 give
        /// more choice each turn). Dealt at the start and topped up after each play.
        /// </summary>
        public int HandCards = HandSize;

        /// <summary>
        /// Game mode option: how many 52-card decks are shuffled together. 0 = automatic (one, or two
        /// from <see cref="SecondDeckFromPlayers"/> players). More decks are added if the deal needs them.
        /// </summary>
        public int DeckCount = 0;

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

        /// <summary>
        /// Q24: with only 2 players still in, does an 8 skip? Skipping the only other player means you go
        /// again and must beat your own 8, which can leave both players stuck forever. Default: no,
        /// with 2 players left an 8 is just a normal card (it still has to be beaten as an 8).
        /// </summary>
        public bool EightSkipsWithTwoLeft = false;

        /// <summary>
        /// Q11 (your school rule): when you're on your face-up cards and can't play, you pick up the pile
        /// AND one face-up card of your choice goes into your hand with it. false = pile only.
        /// </summary>
        public bool FaceUpPickUpTakesOne = true;

        /// <summary>
        /// Optional game mode "Swap before play" (not part of the school rules, off by default): after the
        /// deal, everyone may swap any of their hand cards with their face-up cards, then presses Ready.
        /// Play starts once everyone is ready.
        /// </summary>
        public bool SwapBeforePlay = false;

        /// <summary>
        /// Q20 (decided: on): four cards of the same rank in a row on top of the pile burn it, whether they
        /// were played together or one after another by different players. The player who completes the
        /// four goes again, as with a 10.
        /// </summary>
        public bool FourOfAKindBurns = true;

        public int DecksFor(int playerCount)
        {
            int chosen = DeckCount > 0 ? DeckCount : (playerCount >= SecondDeckFromPlayers ? 2 : 1);
            int dealt = playerCount * (TableCards * 2 + System.Math.Max(1, HandCards)); // the deal must fit
            int needed = (dealt + 51) / 52;
            return System.Math.Max(chosen, needed);
        }
    }
}
