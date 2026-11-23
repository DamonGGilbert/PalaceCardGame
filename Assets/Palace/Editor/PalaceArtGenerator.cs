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

            AssetDatabase.Refresh();

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

        private static float Heart(Vector2 p)
        {
            p.x = Mathf.Abs(p.x);
            if (p.y + p.x > 1f)
                return (p - new Vector2(0.25f, 0.75f)).magnitude - Mathf.Sqrt(2f) / 4f;
            float a = (p - new Vector2(0f, 1f)).sqrMagnitude;
            float b = (p - 0.5f * Mathf.Max(p.x + p.y, 0f) * Vector2.one).sqrMagnitude;
            return Mathf.Sqrt(Mathf.Min(a, b)) * Mathf.Sign(p.x - p.y);
        }

        private static float Spade(Vector2 p)
        {
            float body = Heart(new Vector2(p.x, 1.4036f - p.y));
            float stem = Polygon(p, new[]
            {
                new Vector2(-0.30f, 0f), new Vector2(0.30f, 0f), new Vector2(0.06f, 0.48f), new Vector2(-0.06f, 0.48f)
            });
            return Mathf.Min(body, stem);
        }

        private static float Club(Vector2 p)
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

        private static float Diamond(Vector2 p) => Rhombus(p.x, p.y, 0.43f, 0.63f);

        // ------------------------------------------------------------------
        // Distance helpers (negative = inside)
        // ------------------------------------------------------------------

        private static float RoundRect(float x, float y, float cx, float cy, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(x - cx) - halfW + radius;
            float qy = Mathf.Abs(y - cy) - halfH + radius;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static float Circle(float x, float y, float cx, float cy, float r) => new Vector2(x - cx, y - cy).magnitude - r;

        private static float Rhombus(float x, float y, float halfW, float halfH)
        {
            return (Mathf.Abs(x) / halfW + Mathf.Abs(y) / halfH - 1f) * (halfW * halfH / Mathf.Sqrt(halfW * halfW + halfH * halfH));
        }

        private static float Polygon(Vector2 p, Vector2[] v)
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

        private static float Coverage(float distancePx) => Mathf.Clamp01(0.5f - distancePx);

        private static Color Shape(float distancePx) => new Color(1f, 1f, 1f, Coverage(distancePx));

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private static Color Over(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0f) return new Color(0f, 0f, 0f, 0f);
            Color rgb = (src * src.a + dst * dst.a * (1f - src.a)) / a;
            rgb.a = a;
            return rgb;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        // ------------------------------------------------------------------
        // File + asset handling
        // ------------------------------------------------------------------

        private static void Render(string fileName, int width, int height, Func<float, float, Color> pixel)
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

        private static void ConfigureSprite(string fileName, int maxSize, Vector4 border = default)
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
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        private static Sprite LoadSprite(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + fileName + ".png");
        }

        private static void UpdateThemes()
        {
            var cards = LoadOrCreate<CardTheme>(CardThemePath);
            cards.face = LoadSprite("card_face");
            cards.back = LoadSprite("card_back");
            cards.backVariants = Array.ConvertAll(Backs, b => LoadSprite(b.file));
            cards.outline = LoadSprite("card_outline");
            cards.shadow = LoadSprite("card_shadow");
            cards.glow = LoadSprite("card_glow");
            cards.hearts = LoadSprite("suit_hearts");
            cards.diamonds = LoadSprite("suit_diamonds");
            cards.spades = LoadSprite("suit_spades");
            cards.clubs = LoadSprite("suit_clubs");
            EditorUtility.SetDirty(cards);

            var table = LoadOrCreate<TableTheme>(TableThemePath);
            table.table = LoadSprite("table");
            table.tableVariants = Array.ConvertAll(Felts, f => LoadSprite(f.file));
            table.background = LoadSprite("background");
            table.panel = LoadSprite("panel");
            table.circle = LoadSprite("circle");
            table.ring = LoadSprite("ring");
            table.softGlow = LoadSprite("soft_glow");
            EditorUtility.SetDirty(table);

            AssetDatabase.SaveAssets();
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
