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

        /// <summary>The card back chosen in Settings (falls back to the default back).</summary>
        public Sprite CurrentBack
        {
            get
            {
                if (backVariants == null || backVariants.Length == 0) return back;
                Sprite s = backVariants[Mathf.Clamp(GameSettings.CardBack, 0, backVariants.Length - 1)];
                return s != null ? s : back;
            }
        }

        public Sprite GetCourtIcon(Rank rank)
        {
            switch (rank)
            {
                case Rank.Jack: return jackIcon;
                case Rank.Queen: return queenIcon;
                case Rank.King: return kingIcon;
                default: return null;
            }
        }

        public Sprite GetSuitSprite(Suit suit)
        {
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
