using UnityEditor;
using UnityEngine;
using static Palace.EditorTools.PalaceArtGenerator;

namespace Palace.EditorTools
{
    /// <summary>
    /// The premium range: luxury card backs, decks and tables, priced high and marked PREMIUM on the
    /// Customise screen. The designs are in PalacePremiumPatterns; this paints them into sprites and
    /// lists them. Prices here are before Cosmetics.PriceMultiplier (x10): 2,500 here = 25,000 coins.
    /// </summary>
    public static class PalacePremiumArt
    {
        // id, name shown, price (before x10)
        private static readonly (string id, string name, int price)[] Backs =
        {
            ("carbon", "Carbon Fibre", 1500), ("neon", "Neon Grid", 1600), ("marble", "Marble", 1700), ("gold", "Gold Filigree", 1800),
            ("damask", "Royal Damask", 1800), ("galaxy", "Galaxy", 2000), ("dragon", "Dragon Scale", 2200), ("holo", "Holographic", 2500)
        };

        private static readonly (string id, string name, int price)[] Tables =
        {
            ("marble", "Marble Hall", 3800), ("gold", "Gold Filigree", 4000), ("damask", "Royal Velvet", 4000),
            ("neon", "Neon Arcade", 4200), ("galaxy", "Galaxy", 4500), ("holo", "Holographic", 5000)
        };

        // id, name, price, paper, paper shade, edge, border ink, red suit ink, black suit ink
        private static readonly (string id, string name, int price, string paper, string shade, string edge, string ink, Color red, Color black)[] Decks =
        {
            ("ivory", "Royal Ivory", 2800, "#FFF8E8", "#EADFC4", "#B8963E", "#A07A28", new Color(0.70f, 0.05f, 0.12f), new Color(0.08f, 0.10f, 0.26f)),
            ("obsidian", "Obsidian Gold", 3000, "#17161C", "#0B0A0E", "#6B5A2A", "#E8C46A", new Color(1f, 0.45f, 0.40f), new Color(0.93f, 0.90f, 0.82f)),
            ("rose", "Rose Quartz", 3000, "#F9E3E9", "#EAC4CF", "#C0809A", "#B05A7A", new Color(0.74f, 0.10f, 0.30f), new Color(0.26f, 0.12f, 0.30f)),
            ("emerald", "Emerald Prestige", 3200, "#0F3B2A", "#0A2A1E", "#C9A55A", "#E6C878", new Color(1f, 0.56f, 0.50f), new Color(0.96f, 0.93f, 0.80f)),
            ("neon", "Neon Noir", 3500, "#0B0B14", "#05050A", "#FF2BD6", "#29F3FF", new Color(1f, 0.28f, 0.76f), new Color(0.32f, 0.95f, 1f))
        };

        private const float TableDensity = 1.2f;

        [MenuItem("Palace/Setup/Regenerate Premium Cosmetics", priority = 34)]
        public static void RegenerateMenu()
        {
            Generate();
            AssetDatabase.Refresh();
            Configure();
            var lib = AssetDatabase.LoadAssetAtPath<CosmeticLibrary>(LibraryPath);
            if (lib != null)
            {
                AddToLibrary(lib);
                EditorUtility.SetDirty(lib);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Palace] Premium cosmetics regenerated.");
        }

        public static void Generate()
        {
            PalacePremiumPatterns.Noise = PalacePubArt.Grain;
            try
            {
                foreach (var (id, _, _) in Backs)
                {
                    string pattern = id;
                    // The panel inside the gilt frame is 248 x 360.
                    PalacePubArt.BackPaint = (u, v) => PalacePremiumPatterns.Pattern(pattern, u * 248f / 360f, v);
                    Render("premium_back_" + id, 280, 392, PalacePubArt.CardBackColor);
                }
                PalacePubArt.BackPaint = null;

                foreach (var (id, _, _) in Tables)
                {
                    string pattern = id;
                    // The felt is about 1808 x 784.
                    PalacePubArt.FeltPaint = (u, v) => PalacePremiumPatterns.Pattern(pattern, u * 1808f / 784f * TableDensity, v * TableDensity);
                    Render("premium_table_" + id, 2048, 1024, PalacePubArt.TableColor);
                }
                PalacePubArt.FeltPaint = null;

                foreach (var d in Decks)
                    PalacePubArt.WithPaper(Hex(d.paper), Hex(d.shade), Hex(d.edge), Hex(d.ink),
                        () => Render("premium_face_" + d.id, 280, 392, PalacePubArt.CardFaceColor));
            }
            finally
            {
                PalacePubArt.BackPaint = null;
                PalacePubArt.FeltPaint = null;
            }
        }

        public static void Configure()
        {
            foreach (var (id, _, _) in Backs) ConfigureSprite("premium_back_" + id, 512);
            foreach (var (id, _, _) in Tables) ConfigureSprite("premium_table_" + id, 2048);
            foreach (var d in Decks) ConfigureSprite("premium_face_" + d.id, 512);
        }

        /// <summary>Adds the premium items right after the standard ones (before the long list of flags).</summary>
        public static void AddToLibrary(CosmeticLibrary lib)
        {
            lib.backs.RemoveAll(b => b.id.StartsWith("back_premium_"));
            lib.tables.RemoveAll(t => t.id.StartsWith("table_premium_"));
            lib.decks.RemoveAll(d => d.id.StartsWith("deck_premium_"));

            var backs = new System.Collections.Generic.List<BackSkin>();
            foreach (var (id, name, price) in Backs)
            {
                Sprite s = LoadSprite("premium_back_" + id);
                if (s != null) backs.Add(new BackSkin { id = "back_premium_" + id, displayName = name, price = price, back = s, preview = s, premium = true });
            }
            Insert(lib.backs, backs, "back_flag_");

            var tables = new System.Collections.Generic.List<TableSkin>();
            foreach (var (id, name, price) in Tables)
            {
                Sprite s = LoadSprite("premium_table_" + id);
                if (s != null) tables.Add(new TableSkin { id = "table_premium_" + id, displayName = name, price = price, table = s, preview = s, premium = true });
            }
            Insert(lib.tables, tables, "table_flag_");

            foreach (var d in Decks)
            {
                Sprite face = LoadSprite("premium_face_" + d.id);
                if (face == null) continue;
                lib.decks.Add(new DeckSkin
                {
                    id = "deck_premium_" + d.id, displayName = d.name, price = d.price, face = face, preview = face, premium = true,
                    hearts = LoadSprite("pub_suit_hearts"), diamonds = LoadSprite("pub_suit_diamonds"),
                    spades = LoadSprite("pub_suit_spades"), clubs = LoadSprite("pub_suit_clubs"),
                    jack = LoadSprite("pub_icon_jack"), queen = LoadSprite("pub_icon_queen"), king = LoadSprite("pub_icon_king"),
                    red = d.red, black = d.black
                });
            }
        }

        private static void Insert<T>(System.Collections.Generic.List<T> list, System.Collections.Generic.List<T> items, string beforePrefix) where T : CosmeticItem
        {
            int at = list.FindIndex(i => i.id.StartsWith(beforePrefix));
            list.InsertRange(at < 0 ? list.Count : at, items);
        }
    }
}
