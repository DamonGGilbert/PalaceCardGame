using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Palace
{
    public enum CosmeticCategory { Table, Deck, CardBack, Banner, NameStyle, Effects, Font, Celebration }

    /// <summary>Burn effects. Add new ones at the end (online play sends the item id, saves keep ids).</summary>
    public enum FxStyle { Classic, Water, Neon, Frost, GoldRush, Galaxy, Toxic, Confetti, Lightning, Shadow }

    /// <summary>What plays when a player wins the round.</summary>
    public enum CelebrationStyle { Confetti, Fireworks, CoinShower, Starburst, Bubbles }

    [Serializable]
    public class CosmeticItem
    {
        public string id;
        public string displayName;
        public int price;            // in coins; 0 = free / owned from the start
        public Sprite preview;       // shown on the Customise screen
        public bool storeOnly;       // only from a real-money pack (none at the moment: everything is bought with coins)
    }

    [Serializable] public class TableSkin : CosmeticItem
    {
        public Sprite table;
        public bool animatedWater;   // shimmering caustics over the felt
    }

    [Serializable] public class DeckSkin : CosmeticItem
    {
        public Sprite face, hearts, diamonds, spades, clubs, jack, queen, king;
        public Color red = Color.red, black = Color.black;
    }

    [Serializable] public class BackSkin : CosmeticItem
    {
        public Sprite back;
    }

    [Serializable] public class BannerSkin : CosmeticItem
    {
        public Sprite plate;
        public Color tint = Color.white;
        public Color text = Color.white;
        [Range(0f, 0.5f)] public float textOutline;   // dark outline so a name reads on a busy banner (flags)
    }

    [Serializable] public class NameStyle : CosmeticItem
    {
        public Color face = Color.white;
        public Color outline = Color.black;
        [Range(0f, 0.5f)] public float outlineWidth;
    }

    [Serializable] public class EffectSkin : CosmeticItem
    {
        public FxStyle style;
    }

    /// <summary>A font for your name on your seat (shown to everyone at the table, online too).</summary>
    [Serializable] public class FontSkin : CosmeticItem
    {
        public TMP_FontAsset font;
        public float sizeScale = 1f;   // some display fonts run large or small
    }

    [Serializable] public class CelebrationSkin : CosmeticItem
    {
        public CelebrationStyle style;
    }

    /// <summary>
    /// Every cosmetic in the game. Lives at Assets/Palace/Resources/CosmeticLibrary.asset and is
    /// filled in by Palace > Setup > 2 - Generate Art. To add an item: add an entry here (or in the
    /// generator) with a unique id and a price; the Customise screen picks it up automatically.
    /// </summary>
    [CreateAssetMenu(menuName = "Palace/Cosmetic Library", fileName = "CosmeticLibrary")]
    public class CosmeticLibrary : ScriptableObject
    {
        public List<TableSkin> tables = new List<TableSkin>();
        public List<DeckSkin> decks = new List<DeckSkin>();
        public List<BackSkin> backs = new List<BackSkin>();
        public List<BannerSkin> banners = new List<BannerSkin>();
        public List<NameStyle> nameStyles = new List<NameStyle>();
        public List<EffectSkin> effects = new List<EffectSkin>();
        public List<FontSkin> fonts = new List<FontSkin>();
        public List<CelebrationSkin> celebrations = new List<CelebrationSkin>();

        [Tooltip("Tileable light-ripple texture for animated water tables.")]
        public Texture2D waterCaustics;
        [Tooltip("White stadium shape matching the felt, used to keep the water inside the rail.")]
        public Sprite feltMask;

        private static CosmeticLibrary loaded;

        public static CosmeticLibrary Instance
        {
            get
            {
                if (loaded == null)
                {
                    loaded = Resources.Load<CosmeticLibrary>("CosmeticLibrary");
                    if (loaded != null) loaded.AddBuiltIns();
                }
                return loaded;
            }
        }

        /// <summary>
        /// Effects and celebrations are drawn entirely in code, so they're listed here rather than by
        /// Generate Art: they appear straight away without regenerating anything. Prices are before
        /// Cosmetics.PriceMultiplier (x10), so 400 here = 4,000 coins in the game.
        /// </summary>
        private static readonly (string id, string name, int price, FxStyle style)[] CodeEffects =
        {
            ("fx_neon", "Neon", 400, FxStyle.Neon),
            ("fx_frost", "Frost", 400, FxStyle.Frost),
            ("fx_gold", "Gold Rush", 600, FxStyle.GoldRush),
            ("fx_galaxy", "Galaxy", 600, FxStyle.Galaxy),
            ("fx_toxic", "Toxic", 450, FxStyle.Toxic),
            ("fx_confetti", "Party", 500, FxStyle.Confetti),
            ("fx_lightning", "Lightning", 700, FxStyle.Lightning),
            ("fx_shadow", "Shadow", 700, FxStyle.Shadow)
        };

        private static readonly (string id, string name, int price, CelebrationStyle style)[] CodeCelebrations =
        {
            ("win_confetti", "Confetti", 0, CelebrationStyle.Confetti),
            ("win_fireworks", "Fireworks", 500, CelebrationStyle.Fireworks),
            ("win_coins", "Coin Shower", 500, CelebrationStyle.CoinShower),
            ("win_starburst", "Starburst", 400, CelebrationStyle.Starburst),
            ("win_bubbles", "Bubbles", 350, CelebrationStyle.Bubbles)
        };

        private void AddBuiltIns()
        {
            if (!fonts.Exists(f => f.id == "font_default")) // the game's own font, free (the name fonts come from Generate Art)
                fonts.Insert(0, new FontSkin { id = "font_default", displayName = "Classic", price = 0, font = null });
            // The old Supporter Pack items are now ordinary (expensive) coin items, even in a library
            // generated before the shop was simplified.
            foreach (var item in banners)
                if (item.id == "banner_supporter" && item.storeOnly) { item.storeOnly = false; item.price = 800; item.displayName = "Royal Gold"; }
            foreach (var item in nameStyles)
                if (item.id == "name_royal" && item.storeOnly) { item.storeOnly = false; item.price = 600; item.displayName = "Royal"; }

            foreach (var (id, name, price, style) in CodeEffects)
                if (!effects.Exists(e => e.id == id))
                    effects.Add(new EffectSkin { id = id, displayName = name, price = price, style = style });
            foreach (var (id, name, price, style) in CodeCelebrations)
                if (!celebrations.Exists(c => c.id == id))
                    celebrations.Add(new CelebrationSkin { id = id, displayName = name, price = price, style = style });
        }

        public CosmeticItem Find(CosmeticCategory category, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var item in Items(category)) if (item.id == id) return item;
            return null;
        }

        public IReadOnlyList<CosmeticItem> Items(CosmeticCategory category)
        {
            switch (category)
            {
                case CosmeticCategory.Table: return tables;
                case CosmeticCategory.Deck: return decks;
                case CosmeticCategory.CardBack: return backs;
                case CosmeticCategory.Banner: return banners;
                case CosmeticCategory.NameStyle: return nameStyles;
                case CosmeticCategory.Font: return fonts;
                case CosmeticCategory.Celebration: return celebrations;
                default: return effects;
            }
        }
    }
}
