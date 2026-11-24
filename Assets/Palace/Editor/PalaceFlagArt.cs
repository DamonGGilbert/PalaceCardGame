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
            ("italy", "Italy"), ("usa", "USA"),
            // Round 2: flags made of stripes, crosses, stars and circles (no detailed emblems).
            ("netherlands", "Netherlands"), ("belgium", "Belgium"), ("luxembourg", "Luxembourg"), ("austria", "Austria"),
            ("switzerland", "Switzerland"), ("sweden", "Sweden"), ("norway", "Norway"), ("denmark", "Denmark"),
            ("finland", "Finland"), ("iceland", "Iceland"), ("greece", "Greece"), ("czechia", "Czechia"),
            ("hungary", "Hungary"), ("romania", "Romania"), ("bulgaria", "Bulgaria"), ("ukraine", "Ukraine"),
            ("russia", "Russia"), ("lithuania", "Lithuania"), ("latvia", "Latvia"), ("estonia", "Estonia"),
            ("armenia", "Armenia"), ("turkey", "Turkey"), ("japan", "Japan"), ("vietnam", "Vietnam"),
            ("india", "India"), ("nigeria", "Nigeria"), ("ivorycoast", "Ivory Coast"), ("mali", "Mali"),
            ("jamaica", "Jamaica"), ("colombia", "Colombia"), ("peru", "Peru"), ("chile", "Chile")
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

                // ---- horizontal stripes (top to bottom)
                case "netherlands": return Bands(v, "#AE1C28", "#F4F1EA", "#21468B");
                case "luxembourg": return Bands(v, "#EA141D", "#F4F1EA", "#51ADDA");
                case "austria": return Bands(v, "#C8102E", "#F4F1EA", "#C8102E");
                case "hungary": return Bands(v, "#CE2939", "#F4F1EA", "#477050");
                case "bulgaria": return Bands(v, "#F4F1EA", "#00966E", "#D62612");
                case "russia": return Bands(v, "#F4F1EA", "#0039A6", "#D52B1E");
                case "lithuania": return Bands(v, "#FDB913", "#006A44", "#C1272D");
                case "estonia": return Bands(v, "#0072CE", "#1A1A1A", "#F4F1EA");
                case "armenia": return Bands(v, "#D90012", "#0033A0", "#F2A800");
                case "ukraine": return Hex(v > 0.5f ? "#0057B7" : "#FFD700");
                case "latvia": return Hex(v > 0.4f && v < 0.6f ? "#F4F1EA" : "#9E3039");
                case "colombia": return Hex(v > 0.5f ? "#FCD116" : v > 0.25f ? "#003893" : "#CE1126");

                // ---- vertical stripes (hoist to fly)
                case "belgium": return Thirds(u, "#2D2926", "#FDDA24", "#EF3340");
                case "romania": return Thirds(u, "#002B7F", "#FCD116", "#CE1126");
                case "nigeria": return Thirds(u, "#008751", "#F4F1EA", "#008751");
                case "ivorycoast": return Thirds(u, "#F77F00", "#F4F1EA", "#009E60");
                case "mali": return Thirds(u, "#14B53A", "#FCD116", "#CE1126");
                case "peru": return Thirds(u, "#D91023", "#F4F1EA", "#D91023");

                // ---- Nordic crosses: the upright bar sits towards the hoist
                case "sweden": return Nordic(x, y, a, "#006AA7", "#FECC00", 0.2f, null, 0f);
                case "denmark": return Nordic(x, y, a, "#C8102E", "#F4F1EA", 0.14f, null, 0f);
                case "finland": return Nordic(x, y, a, "#F4F1EA", "#002F6C", 0.27f, null, 0f);
                case "norway": return Nordic(x, y, a, "#BA0C2F", "#F4F1EA", 0.25f, "#00205B", 0.125f);
                case "iceland": return Nordic(x, y, a, "#02529C", "#F4F1EA", 0.22f, "#DC1E35", 0.11f);

                case "switzerland":
                {
                    float cx = Mathf.Abs(x - a * 0.5f), cy = Mathf.Abs(y - 0.5f);
                    bool plus = (cx < 0.1f && cy < 0.3f) || (cy < 0.1f && cx < 0.3f);
                    return Hex(plus ? "#F4F1EA" : "#DA291C");
                }
                case "greece":
                {
                    // 9 blue and white stripes; a blue canton (5 stripes tall) with a white cross.
                    const float stripe = 1f / 9f;
                    float cantonW = Mathf.Min(5f * stripe, a * 0.6f); // narrower on tall card backs
                    if (y > 1f - 5f * stripe && x < cantonW)
                    {
                        float mx = cantonW * 0.5f, my = 1f - 2.5f * stripe;
                        bool cross = Mathf.Abs(x - mx) < stripe * 0.5f || Mathf.Abs(y - my) < stripe * 0.5f;
                        return Hex(cross ? "#F4F1EA" : "#0D5EAF");
                    }
                    int band = Mathf.Clamp(Mathf.FloorToInt((1f - y) / stripe), 0, 8);
                    return Hex(band % 2 == 0 ? "#0D5EAF" : "#F4F1EA");
                }
                case "czechia":
                {
                    float apex = Mathf.Min(a * 0.5f, 0.75f);
                    bool triangle = x < apex * (1f - Mathf.Abs(y - 0.5f) * 2f);
                    return Hex(triangle ? "#11457E" : v > 0.5f ? "#F4F1EA" : "#D7141A");
                }
                case "jamaica":
                {
                    if (Saltire(x, y, a) < 0.09f) return Hex("#FED100");
                    float nx = Mathf.Abs(x - a * 0.5f) / (a * 0.5f), ny = Mathf.Abs(y - 0.5f) / 0.5f;
                    return Hex(nx > ny ? "#1A1A1A" : "#009B3A");
                }
                case "japan":
                    return Hex(new Vector2(x - a * 0.5f, y - 0.5f).magnitude < 0.3f ? "#BC002D" : "#F4F1EA");
                case "vietnam":
                    return Hex(InStar(x, y, a * 0.5f, 0.5f, 0.3f) ? "#FFCD00" : "#DA251D");
                case "chile":
                {
                    // White over red, with a blue square canton holding a white star.
                    float side = Mathf.Min(0.5f, a * 0.45f); // the canton stays square-ish on tall card backs
                    if (y > 0.5f && x < side) return Hex(InStar(x, y, side * 0.5f, 0.75f, Mathf.Min(0.13f, side * 0.3f)) ? "#F4F1EA" : "#0039A6");
                    return Hex(y > 0.5f ? "#F4F1EA" : "#D52B1E");
                }
                case "turkey":
                {
                    // Crescent and star shrink and centre on tall card backs so the star isn't cut off.
                    float s = Mathf.Min(1f, a / 0.95f);
                    float cx = a < 0.95f ? a * 0.5f - 0.1025f * s : Mathf.Min(a * 0.38f, 0.75f);
                    bool outer = new Vector2(x - cx, y - 0.5f).magnitude < 0.25f * s;
                    bool inner = new Vector2(x - cx - 0.0625f * s, y - 0.5f).magnitude < 0.2f * s;
                    bool star = InStar(x, y, cx + 0.33f * s, 0.5f, 0.125f * s, 90f + 18f);
                    return Hex((outer && !inner) || star ? "#F4F1EA" : "#E30A17");
                }
                case "india":
                {
                    if (v > 2f / 3f) return Hex("#FF9933");
                    if (v < 1f / 3f) return Hex("#138808");
                    // The Ashoka Chakra: a navy wheel with 24 spokes.
                    var d = new Vector2(x - a * 0.5f, y - 0.5f);
                    float r = d.magnitude;
                    bool rim = r < 0.15f && r > 0.13f;
                    float ang = Mathf.Atan2(d.y, d.x) / (Mathf.PI * 2f) * 24f;
                    bool spoke = r < 0.14f && Mathf.Abs(ang - Mathf.Round(ang)) * (Mathf.PI * 2f / 24f) * r < 0.006f;
                    bool hub = r < 0.025f;
                    return Hex(rim || spoke || hub ? "#000080" : "#F4F1EA");
                }
                default: return Color.magenta;
            }
        }

        /// <summary>Three equal horizontal bands, top to bottom.</summary>
        private static Color Bands(float v, string top, string middle, string bottom) =>
            Hex(v > 2f / 3f ? top : v > 1f / 3f ? middle : bottom);

        /// <summary>A Nordic cross, optionally with a narrower cross of another colour inside it.</summary>
        private static Color Nordic(float x, float y, float a, string field, string cross, float width, string inner, float innerWidth)
        {
            float bar = Mathf.Min(a * 0.36f, 0.68f); // the upright sits about a third of the way along
            float dx = Mathf.Abs(x - bar), dy = Mathf.Abs(y - 0.5f);
            float d = Mathf.Min(dx, dy);
            if (inner != null && d < innerWidth * 0.5f) return Hex(inner);
            return Hex(d < width * 0.5f ? cross : field);
        }

        private static readonly System.Collections.Generic.Dictionary<float, Vector2[]> stars =
            new System.Collections.Generic.Dictionary<float, Vector2[]>();

        /// <summary>Is (x, y) inside a five-pointed star centred on (cx, cy) with outer radius r?</summary>
        private static bool InStar(float x, float y, float cx, float cy, float r, float pointAngle = 90f)
        {
            if (Mathf.Abs(x - cx) > r || Mathf.Abs(y - cy) > r) return false;
            if (!stars.TryGetValue(pointAngle, out var points))
            {
                points = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float ang = (pointAngle + i * 36f) * Mathf.Deg2Rad;
                    float rr = i % 2 == 0 ? 1f : 0.38f;
                    points[i] = new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr);
                }
                stars[pointAngle] = points;
            }
            return Polygon(new Vector2((x - cx) / r, (y - cy) / r), points) < 0f;
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
