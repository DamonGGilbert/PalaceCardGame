using System;
using System.Collections.Generic;
using UnityEngine;

namespace Palace
{
    public enum CosmeticCategory { Table, Deck, CardBack, Banner, NameStyle, Effects }

    public enum FxStyle { Classic, Water }

    [Serializable]
    public class CosmeticItem
    {
        public string id;
        public string displayName;
        public int price;            // in coins; 0 = free / owned from the start
        public Sprite preview;       // shown on the Customise screen
        public bool storeOnly;       // only from a real-money pack (e.g. the Supporter Pack)
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

        [Tooltip("Tileable light-ripple texture for animated water tables.")]
        public Texture2D waterCaustics;
        [Tooltip("White stadium shape matching the felt, used to keep the water inside the rail.")]
        public Sprite feltMask;

        private static CosmeticLibrary loaded;

        public static CosmeticLibrary Instance
        {
            get
            {
                if (loaded == null) loaded = Resources.Load<CosmeticLibrary>("CosmeticLibrary");
                return loaded;
            }
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
                default: return effects;
            }
        }
    }
}
