using System;
using UnityEngine;
using static Palace.EditorTools.PalaceArtGenerator;

namespace Palace.EditorTools
{
    /// <summary>
    /// The "hand-painted pub" art style: warm walnut and brass, a painted felt surface, parchment
    /// cards with painted suits and illustrated court cards, and wooden menus with brass buttons.
    ///
    /// Everything is painted in code (layered noise for brush strokes, wood grain and paper fibre,
    /// slightly wobbly edges so nothing looks machine-perfect), so it is entirely original to Palace.
    /// Re-run Palace > Setup > 2 - Generate Art after changing any colour below.
    /// </summary>
    public static class PalacePubArt
    {
        // ------------------------------------------------------------------ palette
        private static readonly Color WoodDark = Hex("#24130A");
        private static readonly Color WoodMid = Hex("#5A3318");
        private static readonly Color WoodLight = Hex("#A06A3A");
        private static readonly Color Varnish = Hex("#FFD9A0");
        private static readonly Color Brass = Hex("#C99A4E");
        private static readonly Color BrassLight = Hex("#F6DC96");
        private static readonly Color BrassDark = Hex("#6E4A1E");
        private static readonly Color FeltDeep = Hex("#143A26");
        private static readonly Color FeltMid = Hex("#24583A");
        private static readonly Color FeltLight = Hex("#3F7D50");
        private static readonly Color Paper = Hex("#F7EDD6");
        private static readonly Color PaperShade = Hex("#E3D0A8");
        private static readonly Color PaperEdge = Hex("#B08E5E");
        private static readonly Color InkBrown = Hex("#5C3B22");
        private static readonly Color Oxblood = Hex("#741D17");
        private static readonly Color OxbloodDark = Hex("#3B0C09");
        private static readonly Color RoomDark = Hex("#0F0906");
        private static readonly Color RoomWarm = Hex("#3D2414");
        private static readonly Color Lamp = Hex("#FFB257");

        public static readonly Color RedInk = new Color(0.64f, 0.12f, 0.09f);
        public static readonly Color BlackInk = new Color(0.17f, 0.11f, 0.07f);

        private static Vector3[] lamps;

        public static void GenerateAll()
        {
            var rnd = new System.Random(5);
            lamps = new Vector3[16];
            for (int i = 0; i < lamps.Length; i++)
                lamps[i] = new Vector3((float)rnd.NextDouble() * 960f, 300f + (float)rnd.NextDouble() * 240f, 18f + (float)rnd.NextDouble() * 46f);

            Render("pub_table", 2048, 1024, Table);
            Render("pub_background", 960, 540, Background);
            Render("pub_card_face", 280, 392, CardFace);
            Render("pub_card_back", 280, 392, CardBack);
            Render("pub_suit_hearts", 256, 256, (x, y) => Painted(x, y, 256, Heart, 1.32f, 0.55f, null));
            Render("pub_suit_diamonds", 256, 256, (x, y) => Painted(x, y, 256, Diamond, 1.42f, 0f, null));
            Render("pub_suit_spades", 256, 256, (x, y) => Painted(x, y, 256, Spade, 1.52f, 0.70f, null));
            Render("pub_suit_clubs", 256, 256, (x, y) => Painted(x, y, 256, Club, 1.45f, 0.63f, null));
            Render("pub_icon_king", 256, 256, (x, y) => Painted(x, y, 256, Crown, 1.5f, 0.08f, CrownDetail));
            Render("pub_icon_queen", 256, 256, (x, y) => Painted(x, y, 256, Tiara, 1.5f, 0.12f, TiaraDetail));
            Render("pub_icon_jack", 256, 256, (x, y) => Painted(x, y, 256, Shield, 1.35f, 0f, ShieldDetail));
            Render("pub_panel", 96, 96, (x, y) => Plaque(x, y, false));
            Render("pub_button", 96, 96, (x, y) => Plaque(x, y, true));
        }

        public static void ConfigureAll()
        {
            ConfigureSprite("pub_table", 2048);
            ConfigureSprite("pub_background", 1024);
            foreach (var n in new[] { "pub_card_face", "pub_card_back" }) ConfigureSprite(n, 512);
            foreach (var n in new[] { "pub_suit_hearts", "pub_suit_diamonds", "pub_suit_spades", "pub_suit_clubs",
                                      "pub_icon_king", "pub_icon_queen", "pub_icon_jack" }) ConfigureSprite(n, 256);
            // Plaques are 9-sliced; a higher pixels-per-unit keeps the rounded ends neat on small buttons.
            ConfigureSprite("pub_panel", 96, new Vector4(32f, 32f, 32f, 32f), 150f);
            ConfigureSprite("pub_button", 96, new Vector4(32f, 32f, 32f, 32f), 150f);
        }

        // ------------------------------------------------------------------ table

        private static Color Table(float x, float y)
        {
            const float cx = 1024f, cy = 512f, halfW = 980f, halfH = 468f, rail = 76f;
            float d = RoundRect(x, y, cx, cy, halfW, halfH, halfH);

            if (d > 0f)
            {
                float ds = RoundRect(x, y + 16f, cx, cy, halfW, halfH, halfH);
                var shadow = new Color(0f, 0f, 0f, 0.65f * (1f - Smooth(-4f, 48f, ds)));
                Color edge = WoodDark;
                edge.a = Coverage(d);
                return Over(shadow, edge);
            }

            Color c;
            if (d > -rail)
            {
                // Bent walnut: grain bands follow the rail, warped so they never look ruled.
                float t = -d / rail;
                float warp = Fbm(x * 0.003f, y * 0.003f, 3, 11) * 46f;
                float grain = Mathf.Sin((d + warp) * 0.5f) * 0.5f + 0.5f;
                float fine = Fbm(x * 0.025f + d * 0.04f, y * 0.025f, 3, 5);
                float wood = Mathf.Clamp01(0.32f + grain * 0.28f + (fine - 0.5f) * 0.55f);
                c = Ramp(WoodDark, WoodMid, WoodLight, wood);

                float bulge = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.6f);
                float top = (y - cy) / halfH;
                c *= 0.5f + 0.6f * bulge + 0.12f * top;
                float streak = Mathf.Exp(-Mathf.Pow((t - 0.33f) * 7f, 2f)) * Mathf.Clamp01(0.45f + 0.55f * top);
                c = Color.Lerp(c, Varnish, streak * (0.3f + 0.15f * fine));
                c *= Mathf.Lerp(0.5f, 1f, Smooth(0f, 7f, -d));
            }
            else
            {
                // Painted felt: long soft brush dabs whose direction drifts across the cloth.
                float e = new Vector2((x - cx) / (halfW - rail), (y - cy) / (halfH - rail)).magnitude;
                float angle = (Fbm(x * 0.0015f, y * 0.0015f, 2, 21) - 0.5f) * 1.6f;
                float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
                float u = x * ca - y * sa, v = x * sa + y * ca;
                float strokes = Fbm(u * 0.011f, v * 0.08f, 3, 31);
                float blotch = Fbm(x * 0.004f, y * 0.004f, 4, 41);
                float tone = 0.55f + (blotch - 0.5f) * 0.8f + (strokes - 0.5f) * 0.32f - e * e * 0.4f;
                c = Ramp(FeltDeep, FeltMid, FeltLight, Mathf.Clamp01(tone));
                c *= Mathf.Lerp(1f, 0.5f, Smooth(-rail - 70f, -rail, d));

                float wob = (Fbm(x * 0.01f, y * 0.01f, 2, 51) - 0.5f) * 6f;
                float track = Mathf.Abs(d + rail + 92f + wob) - 2.4f;
                c = Color.Lerp(c, PaperShade, Coverage(track) * 0.35f);
                float track2 = Mathf.Abs(d + rail + 104f + wob) - 1f;
                c = Color.Lerp(c, PaperShade, Coverage(track2) * 0.18f);
            }

            float bt = Mathf.Clamp01((d + rail + 4f) / 8f);
            c = Color.Lerp(c, Color.Lerp(BrassLight, BrassDark, bt), Coverage(Mathf.Abs(d + rail) - 3.4f));
            c.a = 1f;
            return c;
        }

        private static Color Background(float x, float y)
        {
            float e = new Vector2((x - 480f) / 480f, (y - 270f) / 270f).magnitude;
            Color c = Color.Lerp(RoomWarm, RoomDark, Smooth(0.05f, 1.25f, e));

            // Wood panelling on the walls
            float pv = Mathf.Repeat(x + 30f, 120f);
            c *= 0.9f + 0.1f * Smooth(0f, 5f, pv) * Smooth(120f, 114f, pv);
            if (y < 190f) c *= 0.85f + 0.15f * Smooth(180f, 190f, y);

            // Soft out-of-focus lamps
            foreach (var l in lamps)
            {
                float dist = new Vector2(x - l.x, y - l.y).magnitude;
                float glow = 1f - Smooth(l.z * 0.5f, l.z, dist);
                c += Lamp * glow * 0.12f;
            }
            float n = (Fbm(x * 0.05f, y * 0.05f, 2, 81) - 0.5f) * 0.03f;
            return new Color(c.r + n, c.g + n, c.b + n, 1f);
        }

        // ------------------------------------------------------------------ cards

        private static Color CardFace(float x, float y)
        {
            float wob = (Fbm(x * 0.04f, y * 0.04f, 2, 91) - 0.5f) * 2f;
            float d = RoundRect(x, y, 140f, 196f, 138f, 194f, 22f) + wob * 0.5f;

            float n = Fbm(x * 0.03f, y * 0.03f, 3, 61);
            float fibre = Fbm(x * 0.3f, y * 0.05f, 2, 62);
            Color c = Color.Lerp(PaperShade, Paper, 0.72f + (n - 0.5f) * 0.6f);
            c *= 0.96f + fibre * 0.06f;
            c = Color.Lerp(c, PaperEdge, Smooth(-22f, 0f, d) * 0.45f);   // aged edges

            float b1 = Mathf.Abs(d + 11f + wob) - 1.2f;
            float b2 = Mathf.Abs(d + 15.5f + wob) - 0.6f;
            c = Color.Lerp(c, InkBrown, Coverage(b1) * 0.42f + Coverage(b2) * 0.22f);
            c = Color.Lerp(c, PaperEdge * 0.75f, Mathf.Clamp01(d + 2.5f));
            c.a = Coverage(d);
            return c;
        }

        private static Color CardBack(float x, float y)
        {
            float wob = (Fbm(x * 0.04f, y * 0.04f, 2, 93) - 0.5f) * 2f;
            float outer = RoundRect(x, y, 140f, 196f, 138f, 194f, 22f);
            float inner = RoundRect(x, y, 140f, 196f, 124f, 180f, 14f) + wob * 0.4f;

            Color margin = Color.Lerp(PaperShade, Paper, 0.7f + (Fbm(x * 0.03f, y * 0.03f, 3, 63) - 0.5f) * 0.6f);
            margin = Color.Lerp(margin, PaperEdge * 0.75f, Mathf.Clamp01(outer + 2.5f));

            float rad = new Vector2((x - 140f) / 140f, (y - 196f) / 196f).magnitude;
            Color panel = Color.Lerp(OxbloodDark, Oxblood, Mathf.Clamp01(0.75f + (Fbm(x * 0.02f, y * 0.02f, 4, 64) - 0.5f) * 0.8f - rad * 0.45f));

            // Gilt decoration
            float gilt = 0f;
            gilt = Mathf.Max(gilt, Coverage(Mathf.Abs(inner + 10f) - 1.7f));
            gilt = Mathf.Max(gilt, Coverage(Mathf.Abs(inner + 17f) - 0.8f) * 0.8f);

            float lu = (x + y) / 30f, lv = (x - y) / 30f;
            float lattice = Mathf.Min(Mathf.Abs(lu - Mathf.Round(lu)), Mathf.Abs(lv - Mathf.Round(lv))) * 30f / 1.414f;
            if (inner < -20f) gilt = Mathf.Max(gilt, Mathf.Clamp01(1.1f - lattice) * 0.22f);

            float px = x - 140f, py = y - 196f;
            float r = Mathf.Sqrt(px * px + py * py);
            float th = Mathf.Atan2(py, px);
            float petal = Mathf.Abs(r - (40f + 18f * Mathf.Abs(Mathf.Cos(4f * th)))) - 2.2f;
            float ring = Mathf.Abs(r - 74f) - 1.6f;
            float ring2 = Mathf.Abs(r - 22f) - 2f;
            float centre = r - 8f;
            float dots = 99f;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                dots = Mathf.Min(dots, new Vector2(px - Mathf.Cos(a) * 86f, py - Mathf.Sin(a) * 86f).magnitude - 3.6f);
            }
            float rosette = Mathf.Min(Mathf.Min(petal, ring), Mathf.Min(Mathf.Min(ring2, centre), dots));
            if (r < 92f) panel = Color.Lerp(panel, OxbloodDark, 0.25f * (1f - r / 92f));
            gilt = Mathf.Max(gilt, Coverage(rosette));

            Color giltColor = Color.Lerp(BrassDark, BrassLight, 0.45f + (Fbm(x * 0.06f, y * 0.06f, 2, 65) - 0.5f) * 0.9f);
            panel = Color.Lerp(panel, giltColor, gilt);

            Color c = Color.Lerp(margin, panel, Coverage(inner));
            c.a = Coverage(outer);
            return c;
        }

        // ------------------------------------------------------------------ painted icons (white, tinted in game)

        /// <summary>A shape painted in greys: lit from the top-left, darker rim, brush texture, wobbly edge.</summary>
        private static Color Painted(float x, float y, int size, Func<Vector2, float> sdf, float span, float centreY,
            Func<Vector2, float> detail)
        {
            float upp = span / size;
            var p = new Vector2((x - size * 0.5f) * upp, (y - size * 0.5f) * upp + centreY);
            float wobble = (Fbm(x * 0.05f, y * 0.05f, 2, 7) - 0.5f) * 2.4f;
            float d = sdf(p) / upp + wobble;

            float hx = x / size, hy = y / size;
            float shade = 0.74f + 0.2f * Mathf.Clamp01(0.5f - (hx - 0.5f) + (hy - 0.5f));
            float strokes = (Fbm(x * 0.09f, y * 0.014f, 3, 3) - 0.5f) * 0.14f;
            float v = Mathf.Clamp01(shade + strokes);
            v *= Mathf.Lerp(1f, 0.6f, Smooth(-7f, -1f, d));                              // darker painted rim
            float highlight = 1f - Smooth(0f, 0.16f, new Vector2(hx - 0.37f, hy - 0.66f).magnitude);
            v = Mathf.Clamp01(v + highlight * 0.22f);
            if (detail != null) v *= Mathf.Lerp(1f, 0.62f, Coverage(detail(p) / upp));     // engraved details

            return new Color(v, v, v, Coverage(d));
        }

        private static float Crown(Vector2 p)
        {
            float body = Polygon(p, new[]
            {
                new Vector2(-0.56f, -0.36f), new Vector2(0.56f, -0.36f), new Vector2(0.56f, 0.34f), new Vector2(0.3f, 0.06f),
                new Vector2(0f, 0.44f), new Vector2(-0.3f, 0.06f), new Vector2(-0.56f, 0.34f)
            });
            float balls = Mathf.Min(Mathf.Min((p - new Vector2(-0.56f, 0.4f)).magnitude, (p - new Vector2(0.56f, 0.4f)).magnitude),
                (p - new Vector2(0f, 0.52f)).magnitude) - 0.08f;
            return Mathf.Min(body, balls);
        }

        private static float CrownDetail(Vector2 p)
        {
            float band = Mathf.Abs(p.y + 0.14f) - 0.025f;
            float jewels = Mathf.Min(Mathf.Min((p - new Vector2(-0.3f, -0.25f)).magnitude, (p - new Vector2(0.3f, -0.25f)).magnitude),
                (p - new Vector2(0f, -0.25f)).magnitude) - 0.06f;
            return Mathf.Min(band, jewels);
        }

        private static float Tiara(Vector2 p)
        {
            float body = Polygon(p, new[]
            {
                new Vector2(-0.62f, -0.26f), new Vector2(0.62f, -0.26f), new Vector2(0.62f, -0.02f), new Vector2(0.36f, 0.14f),
                new Vector2(0.16f, 0.04f), new Vector2(0f, 0.36f), new Vector2(-0.16f, 0.04f), new Vector2(-0.36f, 0.14f),
                new Vector2(-0.62f, -0.02f)
            });
            float jewel = (p - new Vector2(0f, 0.46f)).magnitude - 0.11f;
            float side = Mathf.Min((p - new Vector2(-0.36f, 0.22f)).magnitude, (p - new Vector2(0.36f, 0.22f)).magnitude) - 0.065f;
            return Mathf.Min(body, Mathf.Min(jewel, side));
        }

        private static float TiaraDetail(Vector2 p)
        {
            float pearls = 99f;
            for (int i = -3; i <= 3; i++) pearls = Mathf.Min(pearls, (p - new Vector2(i * 0.16f, -0.15f)).magnitude - 0.04f);
            return pearls;
        }

        private static float Shield(Vector2 p)
        {
            return Polygon(p, new[]
            {
                new Vector2(-0.46f, 0.5f), new Vector2(0.46f, 0.5f), new Vector2(0.46f, 0.06f), new Vector2(0.32f, -0.3f),
                new Vector2(0f, -0.56f), new Vector2(-0.32f, -0.3f), new Vector2(-0.46f, 0.06f)
            });
        }

        private static float ShieldDetail(Vector2 p)
        {
            // A chevron across the shield
            float a = SegmentDistance(p, new Vector2(-0.46f, 0.0f), new Vector2(0f, 0.3f));
            float b = SegmentDistance(p, new Vector2(0f, 0.3f), new Vector2(0.46f, 0.0f));
            return Mathf.Min(a, b) - 0.075f;
        }

        // ------------------------------------------------------------------ UI plaques

        private static Color Plaque(float x, float y, bool brass)
        {
            float d = RoundRect(x, y, 48f, 48f, 47f, 47f, 26f);
            Color c;
            if (brass)
            {
                float n = Fbm(x * 0.08f, y * 0.08f, 2, 101);
                c = Color.Lerp(BrassDark, BrassLight, Mathf.Clamp01(0.35f + y / 96f * 0.55f + (n - 0.5f) * 0.25f));
            }
            else
            {
                float g = Mathf.Sin(y * 0.55f + Fbm(x * 0.03f, y * 0.08f, 3, 71) * 9f) * 0.5f + 0.5f;
                float n = Fbm(x * 0.1f, y * 0.02f, 2, 72);
                c = Ramp(WoodDark, WoodMid, WoodLight, Mathf.Clamp01(0.22f + g * 0.22f + (n - 0.5f) * 0.3f));
            }
            float rim = Smooth(-9f, -1f, d);
            c = Color.Lerp(c, y > 48f ? (brass ? Color.white : WoodLight) : WoodDark * 0.6f, rim * 0.5f);
            c = Color.Lerp(c, brass ? BrassDark : Brass, Coverage(Mathf.Abs(d + 3.6f) - 1.1f) * (brass ? 0.6f : 0.75f));
            c.a = Coverage(d);
            return c;
        }

        // ------------------------------------------------------------------ helpers

        private static Color Ramp(Color a, Color b, Color c, float t)
        {
            return t < 0.5f ? Color.Lerp(a, b, t * 2f) : Color.Lerp(b, c, (t - 0.5f) * 2f);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        /// <summary>Smooth value noise, 0..1.</summary>
        private static float Noise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float tx = x - xi, ty = y - yi;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Lattice(xi, yi, seed), b = Lattice(xi + 1, yi, seed);
            float c = Lattice(xi, yi + 1, seed), d = Lattice(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Lattice(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967295f;
            }
        }

        /// <summary>Layered noise (fractal Brownian motion), 0..1.</summary>
        private static float Fbm(float x, float y, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y, seed + i * 17) * amp;
                norm += amp;
                x *= 2.03f;
                y *= 2.03f;
                amp *= 0.5f;
            }
            return sum / norm;
        }
    }
}
