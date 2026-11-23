using TMPro;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Everything that controls how the table and player seats LOOK.
    /// Swap this asset to change the table style (later: the "Table appearance" setting).
    /// Created and filled automatically by Palace > Setup > Generate Art.
    /// </summary>
    [CreateAssetMenu(menuName = "Palace/Table Theme", fileName = "TableTheme")]
    public class TableTheme : ScriptableObject
    {
        [Header("Sprites")]
        public Sprite background;
        public Sprite table;
        public Sprite panel;      // rounded rectangle, 9-sliced
        public Sprite circle;
        public Sprite ring;
        public Sprite softGlow;

        [Tooltip("Felt colour choices for Settings > Table (index matches GameSettings.TableColourNames).")]
        public Sprite[] tableVariants;

        /// <summary>The table chosen in Settings (falls back to the default table).</summary>
        public Sprite CurrentTable
        {
            get
            {
                if (tableVariants == null || tableVariants.Length == 0) return table;
                Sprite s = tableVariants[Mathf.Clamp(GameSettings.TableColour, 0, tableVariants.Length - 1)];
                return s != null ? s : table;
            }
        }

        [Header("Colours")]
        public Color panelColor = new Color(0.03f, 0.05f, 0.04f, 0.78f);
        public Color panelTurnColor = new Color(0.42f, 0.31f, 0.09f, 0.92f);
        public Color textColor = new Color(0.95f, 0.93f, 0.89f);
        public Color mutedTextColor = new Color(0.72f, 0.75f, 0.70f);
        public Color accent = new Color(0.95f, 0.76f, 0.30f);
        public Color turnGlow = new Color(1f, 0.78f, 0.30f, 0.55f);

        public Color[] avatarColors =
        {
            new Color(0.80f, 0.33f, 0.27f),
            new Color(0.25f, 0.52f, 0.80f),
            new Color(0.88f, 0.64f, 0.20f),
            new Color(0.45f, 0.70f, 0.36f),
            new Color(0.62f, 0.40f, 0.78f),
            new Color(0.20f, 0.68f, 0.66f),
            new Color(0.86f, 0.45f, 0.62f),
            new Color(0.55f, 0.55f, 0.60f),
            new Color(0.80f, 0.52f, 0.32f),
            new Color(0.38f, 0.42f, 0.80f)
        };

        [Header("Menus and buttons")]
        [Tooltip("Optional painted button art. Empty = plain coloured buttons.")]
        public Sprite buttonPrimary;
        public Sprite buttonSecondary;
        public Color uiTextColor = new Color(0.95f, 0.93f, 0.89f);
        public Color uiMutedColor = new Color(0.68f, 0.72f, 0.66f);
        public Color uiAccentColor = new Color(0.95f, 0.76f, 0.30f);
        public Color uiPanelColor = new Color(0.05f, 0.07f, 0.06f, 0.94f);
        public Color uiInkColor = new Color(0.14f, 0.09f, 0.02f);

        [Header("Font (leave empty to use the TextMeshPro default)")]
        public TMP_FontAsset font;

        public Color GetAvatarColor(int index)
        {
            if (avatarColors == null || avatarColors.Length == 0) return Color.gray;
            return avatarColors[Mathf.Abs(index) % avatarColors.Length];
        }
    }
}
