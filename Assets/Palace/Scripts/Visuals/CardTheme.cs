using System;
using Palace.Core;
using TMPro;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Everything that controls how a card LOOKS. Swap this asset to change the
    /// card style (later: the "Card appearance" setting).
    /// Created and filled automatically by Palace > Setup > Generate Art.
    /// </summary>
    [CreateAssetMenu(menuName = "Palace/Card Theme", fileName = "CardTheme")]
    public class CardTheme : ScriptableObject
    {
        [Header("Card sprites")]
        public Sprite face;
        public Sprite back;
        public Sprite shadow;
        public Sprite glow;
        public Sprite outline;

        [Tooltip("Card back choices for Settings > Card back (index matches GameSettings.CardBackNames).")]
        public Sprite[] backVariants;

        [Header("Suit sprites (white, tinted in code)")]
        public Sprite clubs;
        public Sprite diamonds;
        public Sprite hearts;
        public Sprite spades;

        [Header("Court card pictures (optional; white, tinted in code)")]
        public Sprite jackIcon;
        public Sprite queenIcon;
        public Sprite kingIcon;

        [Header("Colours")]
        public Color redSuit = new Color(0.78f, 0.07f, 0.17f);
        public Color blackSuit = new Color(0.09f, 0.09f, 0.11f);
        public Color selectedGlow = new Color(1f, 0.8f, 0.3f, 1f);
        public Color shadowTint = new Color(1f, 1f, 1f, 0.9f);

        [Header("Font (leave empty to use the TextMeshPro default)")]
        public TMP_FontAsset rankFont;

        // Equipped cosmetics (set at runtime by Cosmetics.ApplyThemes; never saved into the asset).
        [NonSerialized] public DeckSkin deckSkin;
        [NonSerialized] public BackSkin backSkin;

        public Sprite Face => deckSkin != null && deckSkin.face != null ? deckSkin.face : face;
        public Color RedInk => deckSkin != null ? deckSkin.red : redSuit;
        public Color BlackInk => deckSkin != null ? deckSkin.black : blackSuit;

        // Four-colour deck (Settings > Card colours): diamonds blue, clubs green.
        private static readonly Color FourBlue = new Color(0.10f, 0.33f, 0.78f);
        private static readonly Color FourGreen = new Color(0.08f, 0.50f, 0.22f);
        private static readonly Color FourBlueLight = new Color(0.55f, 0.75f, 1f);   // for dark decks
        private static readonly Color FourGreenLight = new Color(0.55f, 0.92f, 0.6f);

        /// <summary>The ink colour for a suit, respecting the 4-colour setting.</summary>
        public Color InkFor(Suit suit)
        {
            bool red = suit == Suit.Hearts || suit == Suit.Diamonds;
            Color normal = red ? RedInk : BlackInk;
            if (!GameSettings.FourColourDeck || suit == Suit.Hearts || suit == Suit.Spades) return normal;
            bool darkDeck = BlackInk.grayscale > 0.5f; // light ink means a dark card face
            if (suit == Suit.Diamonds) return darkDeck ? FourBlueLight : FourBlue;
            return darkDeck ? FourGreenLight : FourGreen;
        }

        /// <summary>The equipped card back (falls back to the default back).</summary>
        public Sprite CurrentBack
        {
            get
            {
                if (backSkin != null && backSkin.back != null) return backSkin.back;
                if (backVariants == null || backVariants.Length == 0) return back;
                Sprite s = backVariants[Mathf.Clamp(GameSettings.CardBack, 0, backVariants.Length - 1)];
                return s != null ? s : back;
            }
        }

        public Sprite GetCourtIcon(Rank rank)
        {
            var d = deckSkin;
            switch (rank)
            {
                case Rank.Jack: return d != null ? d.jack : jackIcon;
                case Rank.Queen: return d != null ? d.queen : queenIcon;
                case Rank.King: return d != null ? d.king : kingIcon;
                default: return null;
            }
        }

        public Sprite GetSuitSprite(Suit suit)
        {
            var d = deckSkin;
            if (d != null && d.hearts != null)
            {
                switch (suit)
                {
                    case Suit.Clubs: return d.clubs;
                    case Suit.Diamonds: return d.diamonds;
                    case Suit.Hearts: return d.hearts;
                    default: return d.spades;
                }
            }
            switch (suit)
            {
                case Suit.Clubs: return clubs;
                case Suit.Diamonds: return diamonds;
                case Suit.Hearts: return hearts;
                default: return spades;
            }
        }
    }
}
