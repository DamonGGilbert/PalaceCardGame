using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Palace.EditorTools
{
    /// <summary>
    /// Draws all Stage 1 art (cards, suits, table, UI shapes) as original PNG files, so the
    /// project has no third-party art licensing to worry about. Re-run any time; it overwrites
    /// the PNGs and updates the CardTheme / TableTheme assets.
    ///
    /// Menu: Palace > Setup > 2 - Generate Art
    /// </summary>
    public static class PalaceArtGenerator
    {
        public const string ArtFolder = "Assets/Palace/Art/Generated";
        public const string SettingsFolder = "Assets/Palace/Settings";
        public const string CardThemePath = SettingsFolder + "/CardTheme.asset";
        public const string TableThemePath = SettingsFolder + "/TableTheme.asset";

        // Palette (see the style guide in Docs/Stage1-Guide.md)
        private static readonly Color Ivory = Hex("#FFFDF8");
        private static readonly Color IvoryShade = Hex("#EFE8DA");
        private static readonly Color CardEdge = Hex("#C9BFAC");
        private static readonly Color Gold = Hex("#D4AF5A");

        // Colour variants (Settings > Table / Card back). Set before each Render call.
        private static Color Burgundy = Hex("#7E1828");
        private static Color BurgundyDark = Hex("#4E0C17");
        private static Color FeltLight = Hex("#17784F");
        private static Color FeltDark = Hex("#0A4530");

        private static readonly (string file, string light, string dark)[] Felts =
        {
            ("table", "#17784F", "#0A4530"),        // green (default)
            ("table_blue", "#1D5F8C", "#0B2E4A"),
            ("table_red", "#8C2A2A", "#4A1010")
        };

        private static readonly (string file, string main, string dark)[] Backs =
        {
            ("card_back", "#7E1828", "#4E0C17"),    // burgundy (default)
            ("card_back_navy", "#1E3566", "#0D1A38")
        };
        private static readonly Color RailLight = Hex("#5A3620");
        private static readonly Color RailDark = Hex("#1E110A");
        private static readonly Color RoomCentre = Hex("#1D2924");
        private static readonly Color RoomEdge = Hex("#050807");

        [MenuItem("Palace/Setup/2 - Generate Art", priority = 2)]
        public static void GenerateAll()
        {
            EnsureFolder(ArtFolder);
            EnsureFolder(SettingsFolder);

            // Cards are drawn at 2x their on-screen size (140x196) for crisp edges on high-DPI phones.
            Render("card_face", 280, 392, CardFace);
            foreach (var b in Backs)
            {
                Burgundy = Hex(b.main);
                BurgundyDark = Hex(b.dark);
                Render(b.file, 280, 392, CardBack);
            }
            Render("card_outline", 280, 392, CardOutline);
            Render("card_shadow", 328, 440, CardShadow);
            Render("card_glow", 360, 472, CardGlow);

            Render("suit_hearts", 256, 256, (x, y) => SuitPixel(x, y, 256, Heart, 1.32f, 0.55f));
            Render("suit_diamonds", 256, 256, (x, y) => SuitPixel(x, y, 256, Diamond, 1.42f, 0f));
            Render("suit_spades", 256, 256, (x, y) => SuitPixel(x, y, 256, Spade, 1.52f, 0.70f));
            Render("suit_clubs", 256, 256, (x, y) => SuitPixel(x, y, 256, Club, 1.45f, 0.63f));

            foreach (var f in Felts)
            {
                FeltLight = Hex(f.light);
                FeltDark = Hex(f.dark);
                Render(f.file, 2048, 1024, Table);
            }
            Render("background", 960, 540, Background);
            Render("panel", 64, 64, (x, y) => Shape(RoundRect(x, y, 32f, 32f, 31f, 31f, 22f)));
            Render("circle", 128, 128, (x, y) => Shape(Circle(x, y, 64f, 64f, 62f)));
            Render("ring", 128, 128, (x, y) => Shape(Mathf.Abs(Circle(x, y, 64f, 64f, 57f)) - 4f));
            Render("soft_glow", 256, 256, SoftGlow);
            PalacePubArt.GenerateAll(); // the hand-painted pub style (the default look)
            PalacePubArt.GenerateCosmetics();
            PalaceFlagArt.Generate();         // country flag tables, card backs and banners
            PalacePremiumArt.Generate();      // premium card backs, decks and tables

            AssetDatabase.Refresh();
            PalacePubArt.ConfigureAll();
            PalacePubArt.ConfigureCosmetics();
            PalaceFlagArt.Configure();
            PalacePremiumArt.Configure();

            ConfigureSprite("card_face", 512);
            foreach (var b in Backs) ConfigureSprite(b.file, 512);
            ConfigureSprite("card_outline", 512);
            ConfigureSprite("card_shadow", 512);
            ConfigureSprite("card_glow", 512);
            ConfigureSprite("suit_hearts", 256);
            ConfigureSprite("suit_diamonds", 256);
            ConfigureSprite("suit_spades", 256);
            ConfigureSprite("suit_clubs", 256);
            foreach (var f in Felts) ConfigureSprite(f.file, 2048);
            ConfigureSprite("background", 1024);
            ConfigureSprite("panel", 64, new Vector4(22f, 22f, 22f, 22f));
            ConfigureSprite("circle", 128);
            ConfigureSprite("ring", 128);
            ConfigureSprite("soft_glow", 256);

            UpdateThemes();
            Debug.Log("[Palace] Art generated in " + ArtFolder + " and themes updated in " + SettingsFolder);
        }

        // ------------------------------------------------------------------
        // Pixel "shaders". x, y are pixel centres with y pointing UP.
        // ------------------------------------------------------------------

        private static Color CardFace(float x, float y)
        {
            float d = RoundRect(x, y, 140f, 196f, 138f, 194f, 22f);
            Color fill = Color.Lerp(IvoryShade, Ivory, y / 392f);
            Color c = Color.Lerp(fill, CardEdge, Mathf.Clamp01(d + 4f));
            c.a = Coverage(d);
            return c;
        }

        private static Color CardBack(float x, float y)
        {
            float outer = RoundRect(x, y, 140f, 196f, 138f, 194f, 22f);
            float inner = RoundRect(x, y, 140f, 196f, 124f, 180f, 12f);

            Color c = Color.Lerp(Ivory, CardEdge, Mathf.Clamp01(outer + 4f));

            // Burgundy panel with a fine gold lattice
            float radial = Mathf.Clamp01(new Vector2((x - 140f) / 140f, (y - 196f) / 196f).magnitude);
            Color panel = Color.Lerp(Burgundy, BurgundyDark, radial * radial);
            float u = (x + y) / 26f, v = (x - y) / 26f;
            float line = Mathf.Min(Mathf.Abs(u - Mathf.Round(u)), Mathf.Abs(v - Mathf.Round(v))) * 26f / 1.414f;
            panel = Color.Lerp(panel, Gold, Mathf.Clamp01(1.3f - line) * 0.30f);

            // Inner gold frame
            panel = Color.Lerp(panel, Gold, Coverage(Mathf.Abs(inner + 11f) - 1.6f));

            // Centre emblem: gold diamond with a burgundy core
            float emblem = Rhombus(x - 140f, y - 196f, 40f, 62f);
            float core = Rhombus(x - 140f, y - 196f, 26f, 44f);
            panel = Color.Lerp(panel, Gold, Coverage(emblem));
            panel = Color.Lerp(panel, BurgundyDark, Coverage(core));
            panel = Color.Lerp(panel, Gold, Coverage(Rhombus(x - 140f, y - 196f, 9f, 15f)));

            c = Color.Lerp(c, panel, Coverage(inner));
            c.a = Coverage(outer);
            return c;
        }

        private static Color CardOutline(float x, float y)
        {
            float d = RoundRect(x, y, 140f, 196f, 136f, 192f, 20f);
            return Shape(Mathf.Abs(d + 2.5f) - 2.5f);
        }

        private static Color CardShadow(float x, float y)
        {
            float d = RoundRect(x, y, 164f, 220f, 138f, 194f, 24f);
            float a = 0.55f * (1f - Smooth(-10f, 26f, d));
            return new Color(0f, 0f, 0f, a);
        }

        private static Color CardGlow(float x, float y)
        {
            float d = RoundRect(x, y, 180f, 236f, 140f, 196f, 24f);
            float a = 1f - Smooth(-4f, 34f, d);
            return new Color(1f, 1f, 1f, a * a);
        }

        private static Color SoftGlow(float x, float y)
        {
            float r = new Vector2(x - 128f, y - 128f).magnitude / 128f;
            float a = 1f - Smooth(0f, 1f, r);
            return new Color(1f, 1f, 1f, a * a);
        }

        private static Color Background(float x, float y)
        {
            float e = new Vector2((x - 480f) / 480f, (y - 270f) / 270f).magnitude;
            return Color.Lerp(RoomCentre, RoomEdge, Smooth(0.15f, 1.35f, e));
        }

        private static Color Table(float x, float y)
        {
            const float cx = 1024f, cy = 512f, halfW = 980f, halfH = 468f;
            const float rail = 62f;

            float d = RoundRect(x, y, cx, cy, halfW, halfH, halfH);

            // Soft drop shadow outside the rail (slightly offset downwards)
            if (d > 0f)
            {
                float ds = RoundRect(x, y + 12f, cx, cy, halfW, halfH, halfH);
                float shadow = 0.6f * (1f - Smooth(-4f, 40f, ds));
                var s = new Color(0f, 0f, 0f, shadow);
                Color railEdge = RailDark;
                railEdge.a = Coverage(d);
                return Over(s, railEdge);
            }

            Color c;
            if (d > -rail)
            {
                // Padded leather rail: lighter in the middle, lit from above
                float t = -d / rail;
                float bulge = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.7f);
                float light = 0.85f + 0.25f * ((y - cy) / halfH);
                c = Color.Lerp(RailDark, RailLight, bulge) * light;
                float stitch = Mathf.Abs(t - 0.5f) * rail;
                c = Color.Lerp(c, c * 1.25f, Mathf.Clamp01(1f - stitch) * 0.5f);
            }
            else
            {
                // Felt: radial gradient, gentle noise, darker near the rail
                float e = new Vector2((x - cx) / (halfW - rail), (y - cy) / (halfH - rail)).magnitude;
                c = Color.Lerp(FeltLight, FeltDark, Smooth(0.05f, 1.1f, e));
                float noise = (Hash(x, y) - 0.5f) * 0.035f;
                c = new Color(c.r + noise, c.g + noise, c.b + noise);
                float innerShadow = Smooth(-rail - 40f, -rail, d);
                c = Color.Lerp(c, c * 0.6f, innerShadow);

                // Faint racetrack line printed on the felt, like a real poker table
                float track = Mathf.Abs(d + rail + 84f) - 1.6f;
                c = Color.Lerp(c, Gold, Coverage(track) * 0.28f);
            }

            // Brass trim between rail and felt
            c = Color.Lerp(c, Gold * 0.9f, Coverage(Mathf.Abs(d + rail) - 2.4f));
            c.a = 1f;
            return c;
        }

        // ------------------------------------------------------------------
        // Suit shapes as signed distance functions in "suit units".
        // ------------------------------------------------------------------

        private static Color SuitPixel(float x, float y, int size, Func<Vector2, float> sdf, float span, float centreY)
        {
            float unitsPerPixel = span / size;
            var p = new Vector2((x - size * 0.5f) * unitsPerPixel, (y - size * 0.5f) * unitsPerPixel + centreY);
            return Shape(sdf(p) / unitsPerPixel);
        }

        internal static float Heart(Vector2 p)
        {
            p.x = Mathf.Abs(p.x);
            if (p.y + p.x > 1f)
                return (p - new Vector2(0.25f, 0.75f)).magnitude - Mathf.Sqrt(2f) / 4f;
            float a = (p - new Vector2(0f, 1f)).sqrMagnitude;
            float b = (p - 0.5f * Mathf.Max(p.x + p.y, 0f) * Vector2.one).sqrMagnitude;
            return Mathf.Sqrt(Mathf.Min(a, b)) * Mathf.Sign(p.x - p.y);
        }

        internal static float Spade(Vector2 p)
        {
            float body = Heart(new Vector2(p.x, 1.4036f - p.y));
            float stem = Polygon(p, new[]
            {
                new Vector2(-0.30f, 0f), new Vector2(0.30f, 0f), new Vector2(0.06f, 0.48f), new Vector2(-0.06f, 0.48f)
            });
            return Mathf.Min(body, stem);
        }

        internal static float Club(Vector2 p)
        {
            float d = (p - new Vector2(0f, 0.95f)).magnitude - 0.30f;
            d = Mathf.Min(d, (p - new Vector2(-0.33f, 0.56f)).magnitude - 0.30f);
            d = Mathf.Min(d, (p - new Vector2(0.33f, 0.56f)).magnitude - 0.30f);
            d = Mathf.Min(d, (p - new Vector2(0f, 0.66f)).magnitude - 0.17f);
            float stem = Polygon(p, new[]
            {
                new Vector2(-0.28f, 0f), new Vector2(0.28f, 0f), new Vector2(0.06f, 0.52f), new Vector2(-0.06f, 0.52f)
            });
            return Mathf.Min(d, stem);
        }

        internal static float Diamond(Vector2 p) => Rhombus(p.x, p.y, 0.43f, 0.63f);

        // ------------------------------------------------------------------
        // Distance helpers (negative = inside)
        // ------------------------------------------------------------------

        internal static float RoundRect(float x, float y, float cx, float cy, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(x - cx) - halfW + radius;
            float qy = Mathf.Abs(y - cy) - halfH + radius;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        internal static float Circle(float x, float y, float cx, float cy, float r) => new Vector2(x - cx, y - cy).magnitude - r;

        internal static float Rhombus(float x, float y, float halfW, float halfH)
        {
            return (Mathf.Abs(x) / halfW + Mathf.Abs(y) / halfH - 1f) * (halfW * halfH / Mathf.Sqrt(halfW * halfW + halfH * halfH));
        }

        internal static float Polygon(Vector2 p, Vector2[] v)
        {
            float d = (p - v[0]).sqrMagnitude;
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, b.sqrMagnitude);
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        internal static float Coverage(float distancePx) => Mathf.Clamp01(0.5f - distancePx);

        internal static Color Shape(float distancePx) => new Color(1f, 1f, 1f, Coverage(distancePx));

        internal static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        internal static Color Over(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0f) return new Color(0f, 0f, 0f, 0f);
            Color rgb = (src * src.a + dst * dst.a * (1f - src.a)) / a;
            rgb.a = a;
            return rgb;
        }

        internal static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        // ------------------------------------------------------------------
        // File + asset handling
        // ------------------------------------------------------------------

        internal static void Render(string fileName, int width, int height, Func<float, float, Color> pixel)
        {
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x + 0.5f, y + 0.5f);

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(ArtFolder, fileName + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        internal static void ConfigureSprite(string fileName, int maxSize, Vector4 border = default, float pixelsPerUnit = 100f)
        {
            string path = ArtFolder + "/" + fileName + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        internal static Sprite LoadSprite(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + fileName + ".png");
        }

        private static void UpdateThemes()
        {
            // Default look: the hand-painted pub style. The classic sprites stay available as variants.
            var cards = LoadOrCreate<CardTheme>(CardThemePath);
            cards.face = LoadSprite("pub_card_face");
            cards.back = LoadSprite("pub_card_back");
            var backs = new System.Collections.Generic.List<Sprite> { LoadSprite("pub_card_back") };
            backs.AddRange(Array.ConvertAll(Backs, b => LoadSprite(b.file)));
            cards.backVariants = backs.ToArray();
            cards.outline = LoadSprite("card_outline");
            cards.shadow = LoadSprite("card_shadow");
            cards.glow = LoadSprite("card_glow");
            cards.hearts = LoadSprite("pub_suit_hearts");
            cards.diamonds = LoadSprite("pub_suit_diamonds");
            cards.spades = LoadSprite("pub_suit_spades");
            cards.clubs = LoadSprite("pub_suit_clubs");
            cards.kingIcon = LoadSprite("pub_icon_king");
            cards.queenIcon = LoadSprite("pub_icon_queen");
            cards.jackIcon = LoadSprite("pub_icon_jack");
            cards.redSuit = PalacePubArt.RedInk;
            cards.blackSuit = PalacePubArt.BlackInk;
            cards.selectedGlow = new Color(1f, 0.78f, 0.38f, 1f);
            EditorUtility.SetDirty(cards);

            var table = LoadOrCreate<TableTheme>(TableThemePath);
            table.table = LoadSprite("pub_table");
            var felts = new System.Collections.Generic.List<Sprite> { LoadSprite("pub_table") };
            felts.AddRange(Array.ConvertAll(Felts, f => LoadSprite(f.file)));
            table.tableVariants = felts.ToArray();
            table.background = LoadSprite("pub_background");
            table.panel = LoadSprite("pub_panel");
            table.buttonPrimary = LoadSprite("pub_button");
            table.buttonSecondary = LoadSprite("pub_panel");
            table.circle = LoadSprite("circle");
            table.ring = LoadSprite("ring");
            table.softGlow = LoadSprite("soft_glow");
            table.panelColor = new Color(0.9f, 0.85f, 0.8f, 0.97f);       // wooden name plates
            table.panelTurnColor = new Color(1f, 0.8f, 0.42f, 1f);         // glowing amber on your turn
            table.textColor = new Color(0.98f, 0.93f, 0.82f);
            table.mutedTextColor = new Color(0.86f, 0.76f, 0.58f);
            table.accent = new Color(0.98f, 0.8f, 0.42f);
            table.turnGlow = new Color(1f, 0.72f, 0.3f, 0.55f);
            table.uiTextColor = new Color(0.98f, 0.93f, 0.82f);
            table.uiMutedColor = new Color(0.84f, 0.74f, 0.56f);
            table.uiAccentColor = new Color(0.98f, 0.8f, 0.42f);
            table.uiPanelColor = Color.white;                              // the wood texture shows through
            table.uiInkColor = new Color(0.2f, 0.11f, 0.04f);
            EditorUtility.SetDirty(table);

            TMPro.TMP_FontAsset serif = CreateSerifFont();
            if (serif != null)
            {
                table.font = serif;   // titles, buttons, names
                cards.rankFont = serif;  // card corner indices
                EditorUtility.SetDirty(table);
                EditorUtility.SetDirty(cards);
            }

            BuildCosmeticLibrary();

            AssetDatabase.SaveAssets();
        }

        public const string SerifFontPath = "Assets/Palace/Fonts/Alegreya.ttf";
        public const string SerifAssetPath = "Assets/Palace/Fonts/Alegreya SDF.asset";

        /// <summary>
        /// Builds a TextMeshPro font from Alegreya (SIL Open Font License, see Fonts/Alegreya-OFL.txt).
        /// Dynamic atlas: glyphs are added as text needs them. The default font is a fallback for symbols.
        /// </summary>
        private static TMPro.TMP_FontAsset CreateSerifFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(SerifAssetPath);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(SerifFontPath);
            if (source == null) return null;

            var asset = TMPro.TMP_FontAsset.CreateFontAsset(source, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, TMPro.AtlasPopulationMode.Dynamic, true);
            if (asset == null) return null;
            asset.name = "Alegreya SDF";
            AssetDatabase.CreateAsset(asset, SerifAssetPath);
            asset.material.name = "Alegreya SDF Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var atlas in asset.atlasTextures)
            {
                atlas.name = "Alegreya SDF Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            if (TMPro.TMP_Settings.instance != null && TMPro.TMP_Settings.defaultFontAsset != null)
                asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMPro.TMP_FontAsset> { TMPro.TMP_Settings.defaultFontAsset };
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        public const string LibraryFolder = "Assets/Palace/Resources";
        public const string LibraryPath = LibraryFolder + "/CosmeticLibrary.asset";

        /// <summary>
        /// The Customise screen's catalogue: every item, its name and its price in coins.
        /// Price 0 = owned from the start. The game multiplies every price by Cosmetics.PriceMultiplier
        /// (currently 10), so the numbers here are tenths of what players pay.
        /// </summary>
        private static void BuildCosmeticLibrary()
        {
            EnsureFolder(LibraryFolder);
            var lib = LoadOrCreate<CosmeticLibrary>(LibraryPath);

            TableSkin T(string id, string name, int price, string file, bool water = false) =>
                new TableSkin { id = id, displayName = name, price = price, table = LoadSprite(file), preview = LoadSprite(file), animatedWater = water };
            lib.tables = new System.Collections.Generic.List<TableSkin>
            {
                T("table_pub", "Pub Green", 0, "pub_table"),
                T("table_casino", "Casino Green", 0, "table"),
                T("table_crimson", "Crimson Velvet", 150, "pub_table_crimson"),
                T("table_navy", "Midnight Blue", 150, "pub_table_navy"),
                T("table_casino_blue", "Casino Blue", 100, "table_blue"),
                T("table_casino_red", "Casino Red", 100, "table_red"),
                T("table_lagoon", "Lagoon (animated)", 400, "pub_table_lagoon", true)
            };

            DeckSkin D(string id, string name, int price, string face, string suitPrefix, bool pictures, Color red, Color black) => new DeckSkin
            {
                id = id, displayName = name, price = price, face = LoadSprite(face), preview = LoadSprite(face),
                hearts = LoadSprite(suitPrefix + "suit_hearts"), diamonds = LoadSprite(suitPrefix + "suit_diamonds"),
                spades = LoadSprite(suitPrefix + "suit_spades"), clubs = LoadSprite(suitPrefix + "suit_clubs"),
                jack = pictures ? LoadSprite("pub_icon_jack") : null, queen = pictures ? LoadSprite("pub_icon_queen") : null,
                king = pictures ? LoadSprite("pub_icon_king") : null, red = red, black = black
            };
            lib.decks = new System.Collections.Generic.List<DeckSkin>
            {
                D("deck_parlour", "Parlour", 0, "pub_card_face", "pub_", true, PalacePubArt.RedInk, PalacePubArt.BlackInk),
                D("deck_casino", "Casino", 0, "card_face", "", false, new Color(0.78f, 0.07f, 0.17f), new Color(0.09f, 0.09f, 0.11f)),
                D("deck_midnight", "Midnight Gold", 300, "pub_card_face_midnight", "pub_", true, PalacePubArt.MidnightRed, PalacePubArt.MidnightBlack)
            };

            BackSkin B(string id, string name, int price, string file) =>
                new BackSkin { id = id, displayName = name, price = price, back = LoadSprite(file), preview = LoadSprite(file) };
            lib.backs = new System.Collections.Generic.List<BackSkin>
            {
                B("back_oxblood", "Oxblood Rosette", 0, "pub_card_back"),
                B("back_burgundy", "Casino Burgundy", 0, "card_back"),
                B("back_navy", "Casino Navy", 100, "card_back_navy"),
                B("back_emerald", "Emerald Rosette", 150, "pub_card_back_emerald"),
                B("back_midnight", "Midnight Rosette", 200, "pub_card_back_midnight")
            };

            BannerSkin N(string id, string name, int price, string file, Color tint, Color text) =>
                new BannerSkin { id = id, displayName = name, price = price, plate = LoadSprite(file), preview = LoadSprite(file), tint = tint, text = text };
            lib.banners = new System.Collections.Generic.List<BannerSkin>
            {
                N("banner_walnut", "Walnut", 0, "pub_panel", new Color(0.9f, 0.85f, 0.8f, 0.97f), new Color(0.98f, 0.93f, 0.82f)),
                N("banner_slate", "Slate", 100, "panel", new Color(0.08f, 0.1f, 0.12f, 0.95f), Color.white),
                N("banner_leather", "Stitched Leather", 150, "pub_plate_leather", Color.white, new Color(0.98f, 0.9f, 0.75f)),
                N("banner_brass", "Polished Brass", 200, "pub_button", Color.white, new Color(0.2f, 0.11f, 0.04f))
            };
            // The top-end banner: priced high so it means something (x10 in game = 8,000 coins).
            lib.banners.Add(N("banner_supporter", "Royal Gold", 800, "pub_button", new Color(1f, 0.86f, 0.5f), new Color(0.32f, 0.08f, 0.32f)));

            NameStyle S(string id, string name, int price, Color face, Color outline, float width) =>
                new NameStyle { id = id, displayName = name, price = price, face = face, outline = outline, outlineWidth = width };
            lib.nameStyles = new System.Collections.Generic.List<NameStyle>
            {
                S("name_plain", "Plain", 0, new Color(0.98f, 0.93f, 0.82f), Color.black, 0f),
                S("name_gold", "Gilded", 150, new Color(1f, 0.84f, 0.42f), new Color(0.3f, 0.16f, 0.02f), 0.2f),
                S("name_ice", "Frost", 150, Color.white, new Color(0.45f, 0.8f, 1f), 0.22f),
                S("name_neon", "Neon", 200, new Color(0.8f, 1f, 1f), new Color(0f, 0.85f, 1f), 0.28f),
                S("name_fire", "Blaze", 200, new Color(1f, 0.93f, 0.4f), new Color(0.95f, 0.3f, 0.05f), 0.3f)
            };
            lib.nameStyles.Add(S("name_royal", "Royal", 600, new Color(1f, 0.88f, 0.45f), new Color(0.45f, 0.1f, 0.55f), 0.3f)); // 6,000 coins

            lib.effects = new System.Collections.Generic.List<EffectSkin>
            {
                new EffectSkin { id = "fx_classic", displayName = "Fire & Embers", price = 0, style = FxStyle.Classic, preview = LoadSprite("fx_preview_classic") },
                new EffectSkin { id = "fx_water", displayName = "Water", price = 500, style = FxStyle.Water, preview = LoadSprite("fx_preview_water") }
            };

            lib.waterCaustics = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/water_caustics.png");
            lib.feltMask = LoadSprite("felt_mask");
            PalaceFlagArt.AddToLibrary(lib);
            PalacePremiumArt.AddToLibrary(lib);
            PalaceNameFonts.AddToLibrary(lib);
            EditorUtility.SetDirty(lib);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
