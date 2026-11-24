using System;
using UnityEditor;
using UnityEngine;
using static Palace.EditorTools.PalaceArtGenerator;

namespace Palace.EditorTools
{
    /// <summary>
    /// Country flag cosmetics: for each country a painted flag table, a flag card back in a gilt frame,
    /// and a flag name banner with a brass rim. All painted in code in the pub style (brush texture,
    /// slightly wobbly edges), so nothing is copied from anywhere.
    /// Add a country by adding a line to Countries and a case to Flag().
    /// </summary>
    public static class PalaceFlagArt
    {
        private static readonly (string id, string name)[] Countries =
        {
            ("england", "England"), ("scotland", "Scotland"), ("uk", "United Kingdom"), ("ireland", "Ireland"),
            ("poland", "Poland"), ("spain", "Spain"), ("france", "France"), ("germany", "Germany"),
            ("italy", "Italy"), ("usa", "USA")
        };

        public const int TablePrice = 250, BackPrice = 150, BannerPrice = 100;

        [MenuItem("Palace/Setup/Regenerate Flag Cosmetics", priority = 30)]
        public static void RegenerateMenu()
        {
            Generate();
            AssetDatabase.Refresh();
            Configure();
            RebuildLibrary();
            AssetDatabase.SaveAssets();
            Debug.Log("[Palace] Flag cosmetics regenerated.");
        }

        public static void Generate()
        {
            foreach (var (id, _) in Countries)
            {
                string code = id;
                // Table: the felt area is about 1808 x 784 pixels.
                PalacePubArt.FeltPaint = (u, v) => Sample(code, u, v, 1808f / 784f, 1f / 1808f, 1f / 784f);
                Render("flag_table_" + id, 2048, 1024, PalacePubArt.TableColor);
                PalacePubArt.FeltPaint = null;

                // Card back: the panel is 248 x 360.
                PalacePubArt.BackPaint = (u, v) => Sample(code, u, v, 248f / 360f, 1f / 248f, 1f / 360f);
                Render("flag_back_" + id, 280, 392, PalacePubArt.CardBackColor);
                PalacePubArt.BackPaint = null;

                Render("flag_banner_" + id, 384, 96, (x, y) => Banner(code, x, y));
            }
        }

        public static void Configure()
        {
            foreach (var (id, _) in Countries)
            {
                ConfigureSprite("flag_table_" + id, 2048);
                ConfigureSprite("flag_back_" + id, 512);
                ConfigureSprite("flag_banner_" + id, 384, new Vector4(32f, 32f, 32f, 32f), 150f);
            }
        }

        /// <summary>Adds every flag item to the cosmetic library (called by Generate Art).</summary>
        public static void AddToLibrary(CosmeticLibrary lib)
        {
            lib.tables.RemoveAll(t => t.id.StartsWith("table_flag_"));
            lib.backs.RemoveAll(b => b.id.StartsWith("back_flag_"));
            lib.banners.RemoveAll(b => b.id.StartsWith("banner_flag_"));
            foreach (var (id, name) in Countries)
            {
                Sprite table = LoadSprite("flag_table_" + id), back = LoadSprite("flag_back_" + id), banner = LoadSprite("flag_banner_" + id);
                if (table != null)
                    lib.tables.Add(new TableSkin { id = "table_flag_" + id, displayName = name, price = TablePrice, table = table, preview = table });
                if (back != null)
                    lib.backs.Add(new BackSkin { id = "back_flag_" + id, displayName = name, price = BackPrice, back = back, preview = back });
                if (banner != null)
                    lib.banners.Add(new BannerSkin
                    {
                        id = "banner_flag_" + id, displayName = name, price = BannerPrice, plate = banner, preview = banner,
                        tint = Color.white, text = Color.white, textOutline = 0.28f
                    });
            }
        }

        private static void RebuildLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<CosmeticLibrary>(LibraryPath);
            if (lib == null) return;
            AddToLibrary(lib);
            EditorUtility.SetDirty(lib);
        }

        // ------------------------------------------------------------------ banner

        private static Color Banner(string code, float x, float y)
        {
            float d = RoundRect(x, y, 192f, 48f, 191f, 47f, 26f);
            float wob = (PalacePubArt.Grain(x * 0.05f, y * 0.05f, 2, 301) - 0.5f) * 1.6f;
            Color flag = Sample(code, (x - 6f) / 372f, (y - 6f) / 84f, 372f / 84f, 1f / 372f, 1f / 84f);
            // Painted cloth: brush texture, lit from the top, darker towards the rim.
            float brush = PalacePubArt.Grain(x * 0.04f, y * 0.12f, 3, 302);
            flag *= 0.8f + (brush - 0.5f) * 0.18f + (y / 96f) * 0.12f;
            flag = Color.Lerp(flag, flag * 0.55f, Smooth(-14f, -5f, d) * 0.6f);
            // Brass rim
            float rimT = Mathf.Clamp01(0.3f + y / 96f * 0.6f + (PalacePubArt.Grain(x * 0.08f, y * 0.08f, 2, 303) - 0.5f) * 0.3f);
            Color c = Color.Lerp(flag, PalacePubArt.BrassAt(rimT), Coverage(-(d + 5f + wob)));
            c = Color.Lerp(c, PalacePubArt.WoodAt(0.1f), Coverage(Mathf.Abs(d + 5.5f + wob) - 0.8f) * 0.6f);
            c.a = Coverage(d);
            return c;
        }

        // ------------------------------------------------------------------ flags

        /// <summary>
        /// The flag colour at (u, v), both 0..1 with v going up, over an area 'aspect' times wider than tall.
        /// Four samples per pixel keep diagonals smooth; a little wobble makes the edges look hand-painted.
        /// </summary>
        private static Color Sample(string code, float u, float v, float aspect, float du, float dv)
        {
            float wu = (PalacePubArt.Grain(u * 37f, v * 37f, 2, 311) - 0.5f) * 0.006f;
            float wv = (PalacePubArt.Grain(u * 37f, v * 37f, 2, 312) - 0.5f) * 0.006f * aspect;
            Color sum = Color.clear;
            for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                    sum += Flag(code, u + wu + (sx - 0.5f) * du * 0.5f, v + wv + (sy - 0.5f) * dv * 0.5f, aspect);
            Color c = sum * 0.25f;
            c.a = 1f;
            return c;
        }

        private static Color Flag(string code, float u, float v, float a)
        {
            float x = u * a, y = v;   // x in "flag heights"
            switch (code)
            {
                case "england":
                {
                    bool cross = Mathf.Abs(x - a * 0.5f) < 0.1f || Mathf.Abs(y - 0.5f) < 0.1f;
                    return cross ? Hex("#CE1124") : Hex("#F4F1EA");
                }
                case "scotland":
                    return Saltire(x, y, a) < 0.1f ? Hex("#F4F1EA") : Hex("#0065BD");
                case "uk":
                {
                    float s = Saltire(x, y, a);
                    float cx = Mathf.Abs(x - a * 0.5f), cy = Mathf.Abs(y - 0.5f);
                    float cross = Mathf.Min(cx, cy);
                    if (cross < 0.1f) return Hex("#C8102E");
                    if (cross < 1f / 6f) return Hex("#F4F1EA");
                    if (s < 0.034f) return Hex("#C8102E");
                    if (s < 0.1f) return Hex("#F4F1EA");
                    return Hex("#012169");
                }
                case "ireland": return Thirds(u, "#169B62", "#F4F1EA", "#FF883E");
                case "france": return Thirds(u, "#0055A4", "#F4F1EA", "#EF4135");
                case "italy": return Thirds(u, "#009246", "#F4F1EA", "#CE2B37");
                case "poland": return v > 0.5f ? Hex("#F4F1EA") : Hex("#DC143C");
                case "spain": return v > 0.25f && v < 0.75f ? Hex("#F1BF00") : Hex("#AA151B");
                case "germany": return v > 2f / 3f ? Hex("#1A1A1A") : v > 1f / 3f ? Hex("#DD0000") : Hex("#FFCE00");
                case "usa": return Usa(x, y, a);
                default: return Color.magenta;
            }
        }

        private static Color Thirds(float u, string a, string b, string c) => Hex(u < 1f / 3f ? a : u < 2f / 3f ? b : c);

        /// <summary>Distance (in flag heights) to the nearer diagonal of a corner-to-corner X.</summary>
        private static float Saltire(float x, float y, float a)
        {
            float len = Mathf.Sqrt(1f + a * a);
            float d1 = Mathf.Abs(x - y * a) / len;
            float d2 = Mathf.Abs(x + y * a - a) / len;
            return Mathf.Min(d1, d2);
        }

        private static Vector2[] star;

        private static Color Usa(float x, float y, float a)
        {
            const float cantonH = 7f / 13f;
            float cantonW = Mathf.Min(0.76f, a * 0.62f);
            if (y > 1f - cantonH && x < cantonW)
            {
                if (star == null)
                {
                    star = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float ang = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                        float r = i % 2 == 0 ? 1f : 0.38f;
                        star[i] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                    }
                }
                // 50 stars: 9 rows alternating 6 and 5. Only the nearest few stars are tested.
                float cw = cantonW / 12f, ch = cantonH / 10f;
                float top = 1f;
                float radius = Mathf.Min(cw, ch) * 0.8f;
                int nearRow = Mathf.RoundToInt((top - y) / ch) - 1;
                for (int row = nearRow - 1; row <= nearRow + 1; row++)
                {
                    if (row < 0 || row > 8) continue;
                    float sy = top - (row + 1) * ch;
                    int count = row % 2 == 0 ? 6 : 5;
                    for (int col = 0; col < count; col++)
                    {
                        float sx = row % 2 == 0 ? (2 * col + 1) * cw : (2 * col + 2) * cw;
                        if (Mathf.Abs(x - sx) > radius || Mathf.Abs(y - sy) > radius) continue;
                        if (Polygon(new Vector2((x - sx) / radius, (y - sy) / radius), star) < 0f) return Hex("#F4F1EA");
                    }
                }
                return Hex("#3C3B6E");
            }
            int stripe = Mathf.Clamp(Mathf.FloorToInt((1f - y) * 13f), 0, 12);
            return stripe % 2 == 0 ? Hex("#B22234") : Hex("#F4F1EA");
        }
    }
}
