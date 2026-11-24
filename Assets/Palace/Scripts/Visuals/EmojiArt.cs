using UnityEngine;

namespace Palace
{
    /// <summary>
    /// The reaction emojis, painted in code when first needed (128 x 128, cached), so no emoji font
    /// or downloaded art is used. Shapes are signed-distance fields, which keeps the edges smooth.
    /// Order matters: online reactions send the index. Add new emojis at the end.
    /// </summary>
    public static class EmojiArt
    {
        public static readonly string[] Names =
            { "Smile", "Laughing", "Wink", "Wow", "Aww", "Cool", "Heart eyes", "Heart", "Fire", "Crown", "Star", "Cards" };

        public static int Count => Names.Length;

        private const int N = 128;
        private const float Px = 2f / N;
        private static readonly Sprite[] cache = new Sprite[12];

        public static Sprite Get(int index)
        {
            if (index < 0 || index >= Count) return null;
            if (cache[index] != null) return cache[index];
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "emoji_" + index };
            var pixels = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    pixels[y * N + x] = Paint(index, (x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return cache[index] = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------------ colours

        private static readonly Color Ink = new Color(0.36f, 0.20f, 0.06f);
        private static readonly Color FaceRim = new Color(0.78f, 0.45f, 0.08f);
        private static readonly Color Red = new Color(0.86f, 0.14f, 0.18f);
        private static readonly Color Tear = new Color(0.35f, 0.70f, 0.98f);
        private static readonly Color Gold = new Color(0.98f, 0.76f, 0.20f);
        private static readonly Color GoldDark = new Color(0.66f, 0.42f, 0.06f);

        // ------------------------------------------------------------------ the emojis

        private static Color Paint(int index, float u, float v)
        {
            Color c = Color.clear;
            var p = new Vector2(u, v);
            switch (index)
            {
                case 0: // smile
                    Face(ref c, p);
                    Eyes(ref c, p);
                    Layer(ref c, Ink, Arc(p, new Vector2(0f, 0.02f), 0.48f, 200f, 340f) - 0.065f);
                    break;
                case 1: // laughing, with happy tears
                    Face(ref c, p);
                    HappyEye(ref c, p, -0.32f);
                    HappyEye(ref c, p, 0.32f);
                    Layer(ref c, Ink, Mathf.Max(Circle(p, new Vector2(0f, -0.08f), 0.46f), p.y + 0.08f));
                    Layer(ref c, new Color(0.92f, 0.38f, 0.38f), Mathf.Max(Circle(p, new Vector2(0f, -0.62f), 0.2f), Circle(p, new Vector2(0f, -0.08f), 0.42f)));
                    Layer(ref c, Tear, Circle(p, new Vector2(-0.8f, 0.02f), 0.13f));
                    Layer(ref c, Tear, Circle(p, new Vector2(0.8f, 0.02f), 0.13f));
                    break;
                case 2: // wink
                    Face(ref c, p);
                    Layer(ref c, Ink, Circle(p, new Vector2(-0.32f, 0.22f), 0.11f));
                    HappyEye(ref c, p, 0.32f);
                    Layer(ref c, Ink, Arc(p, new Vector2(0f, 0.02f), 0.48f, 205f, 335f) - 0.065f);
                    break;
                case 3: // wow
                    Face(ref c, p);
                    Layer(ref c, Ink, Circle(p, new Vector2(-0.32f, 0.24f), 0.14f));
                    Layer(ref c, Ink, Circle(p, new Vector2(0.32f, 0.24f), 0.14f));
                    Layer(ref c, Ink, Circle(p, new Vector2(0f, -0.38f), 0.19f));
                    break;
                case 4: // aww (sympathetic)
                    Face(ref c, p);
                    Eyes(ref c, p);
                    Layer(ref c, Ink, Arc(p, new Vector2(0f, -0.72f), 0.42f, 40f, 140f) - 0.065f);
                    Layer(ref c, Tear, Drop(p, new Vector2(0.36f, -0.05f), 0.11f));
                    break;
                case 5: // cool
                    Face(ref c, p);
                    Layer(ref c, new Color(0.10f, 0.08f, 0.10f), Box(p - new Vector2(-0.31f, 0.2f), new Vector2(0.25f, 0.15f), 0.08f));
                    Layer(ref c, new Color(0.10f, 0.08f, 0.10f), Box(p - new Vector2(0.31f, 0.2f), new Vector2(0.25f, 0.15f), 0.08f));
                    Layer(ref c, new Color(0.10f, 0.08f, 0.10f), Segment(p, new Vector2(-0.1f, 0.26f), new Vector2(0.1f, 0.26f)) - 0.035f);
                    Layer(ref c, new Color(1f, 1f, 1f, 0.55f), Segment(p, new Vector2(-0.45f, 0.27f), new Vector2(-0.36f, 0.3f)) - 0.025f);
                    Layer(ref c, Ink, Arc(p, new Vector2(0.05f, 0.0f), 0.45f, 215f, 330f) - 0.065f);
                    break;
                case 6: // heart eyes
                    Face(ref c, p);
                    Layer(ref c, Red, Heart((p - new Vector2(-0.33f, 0.08f)) / 0.27f) * 0.27f);
                    Layer(ref c, Red, Heart((p - new Vector2(0.33f, 0.08f)) / 0.27f) * 0.27f);
                    Layer(ref c, Ink, Arc(p, new Vector2(0f, -0.05f), 0.42f, 205f, 335f) - 0.065f);
                    break;
                case 7: // heart
                {
                    float d = Heart((p + new Vector2(0f, 0.82f)) / 1.5f) * 1.5f;
                    Layer(ref c, new Color(0.62f, 0.06f, 0.10f), d);
                    Layer(ref c, Color.Lerp(Red, new Color(1f, 0.45f, 0.45f), Mathf.Clamp01(v * 0.6f + 0.3f)), d + 0.06f);
                    Layer(ref c, new Color(1f, 1f, 1f, 0.5f), Circle(p, new Vector2(-0.42f, 0.32f), 0.12f));
                    break;
                }
                case 8: // fire
                {
                    float outer = Polygon(p, Flame) - 0.04f;
                    Layer(ref c, new Color(0.80f, 0.20f, 0.06f), outer);
                    Layer(ref c, Color.Lerp(new Color(1f, 0.55f, 0.10f), new Color(0.95f, 0.25f, 0.08f), Mathf.Clamp01(v * 0.6f + 0.4f)), outer + 0.06f);
                    var q = new Vector2(p.x / 0.55f, (p.y + 0.42f) / 0.55f - 0.25f);
                    Layer(ref c, new Color(1f, 0.88f, 0.35f), (Polygon(q, Flame) - 0.04f) * 0.55f);
                    break;
                }
                case 9: // crown
                {
                    float d = Polygon(p, Crown);
                    Layer(ref c, GoldDark, d - 0.03f);
                    Layer(ref c, Color.Lerp(Gold, new Color(1f, 0.9f, 0.55f), Mathf.Clamp01(v * 0.5f + 0.4f)), d + 0.03f);
                    Layer(ref c, GoldDark, Mathf.Max(Mathf.Abs(p.y + 0.36f) - 0.025f, d + 0.03f)); // band across the crown
                    Layer(ref c, Red, Circle(p, new Vector2(-0.8f, 0.52f), 0.1f));
                    Layer(ref c, new Color(0.25f, 0.55f, 0.95f), Circle(p, new Vector2(0f, 0.68f), 0.11f));
                    Layer(ref c, Red, Circle(p, new Vector2(0.8f, 0.52f), 0.1f));
                    Layer(ref c, new Color(0.2f, 0.7f, 0.35f), Circle(p, new Vector2(0f, -0.15f), 0.1f));
                    break;
                }
                case 10: // star
                {
                    float d = Polygon(p, Star);
                    Layer(ref c, GoldDark, d - 0.04f);
                    Layer(ref c, Color.Lerp(Gold, new Color(1f, 0.92f, 0.6f), Mathf.Clamp01(v * 0.5f + 0.45f)), d + 0.03f);
                    break;
                }
                default: // cards
                {
                    Vector2 back = Rotate(p - new Vector2(-0.18f, 0.02f), 14f);
                    Layer(ref c, new Color(0.25f, 0.12f, 0.10f), Box(back, new Vector2(0.42f, 0.6f), 0.08f));
                    Layer(ref c, new Color(0.62f, 0.10f, 0.14f), Box(back, new Vector2(0.36f, 0.54f), 0.06f));
                    Vector2 front = Rotate(p - new Vector2(0.2f, -0.04f), -10f);
                    Layer(ref c, new Color(0.35f, 0.28f, 0.22f), Box(front, new Vector2(0.42f, 0.6f), 0.08f));
                    Layer(ref c, new Color(0.98f, 0.96f, 0.90f), Box(front, new Vector2(0.39f, 0.57f), 0.07f));
                    Layer(ref c, Red, Heart((front + new Vector2(0f, 0.22f)) / 0.42f) * 0.42f);
                    break;
                }
            }
            return c;
        }

        // ------------------------------------------------------------------ face parts

        internal static void Face(ref Color c, Vector2 p)
        {
            Layer(ref c, FaceRim, Circle(p, Vector2.zero, 0.92f));
            float light = Mathf.Clamp01(0.55f + p.y * 0.35f - p.x * 0.15f);
            Layer(ref c, Color.Lerp(new Color(1f, 0.70f, 0.16f), new Color(1f, 0.90f, 0.45f), light), Circle(p, Vector2.zero, 0.86f));
        }

        internal static void Eyes(ref Color c, Vector2 p)
        {
            Layer(ref c, Ink, Circle(p, new Vector2(-0.32f, 0.22f), 0.11f));
            Layer(ref c, Ink, Circle(p, new Vector2(0.32f, 0.22f), 0.11f));
        }

        private static void HappyEye(ref Color c, Vector2 p, float x) =>
            Layer(ref c, Ink, Arc(p, new Vector2(x, 0.14f), 0.15f, 20f, 160f) - 0.055f);

        // ------------------------------------------------------------------ shapes (distance: negative inside)

        internal static readonly Vector2[] Star = MakeStar();
        internal static readonly Vector2[] Crown =
        {
            new Vector2(-0.72f, -0.55f), new Vector2(0.72f, -0.55f), new Vector2(0.8f, 0.45f), new Vector2(0.38f, 0.02f),
            new Vector2(0f, 0.6f), new Vector2(-0.38f, 0.02f), new Vector2(-0.8f, 0.45f)
        };
        internal static readonly Vector2[] Flame =
        {
            new Vector2(0.02f, 0.92f), new Vector2(0.28f, 0.52f), new Vector2(0.56f, 0.18f), new Vector2(0.62f, -0.22f),
            new Vector2(0.48f, -0.6f), new Vector2(0.16f, -0.84f), new Vector2(-0.16f, -0.84f), new Vector2(-0.48f, -0.6f),
            new Vector2(-0.62f, -0.24f), new Vector2(-0.55f, 0.12f), new Vector2(-0.34f, 0.3f), new Vector2(-0.3f, 0.58f)
        };

        private static Vector2[] MakeStar()
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.9f : 0.4f;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 0.05f);
            }
            return pts;
        }

        internal static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        internal static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        /// <summary>Distance to a circular arc (angles in degrees, counter-clockwise from +x).</summary>
        internal static float Arc(Vector2 p, Vector2 c, float r, float from, float to)
        {
            float best = float.MaxValue;
            const int steps = 16;
            Vector2 prev = c + new Vector2(Mathf.Cos(from * Mathf.Deg2Rad), Mathf.Sin(from * Mathf.Deg2Rad)) * r;
            for (int i = 1; i <= steps; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
                Vector2 next = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                best = Mathf.Min(best, Segment(p, prev, next));
                prev = next;
            }
            return best;
        }

        internal static float Box(Vector2 p, Vector2 half, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(radius, radius);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        /// <summary>A heart with its point at (0, 0) and about 1.1 tall.</summary>
        internal static float Heart(Vector2 p)
        {
            p.x = Mathf.Abs(p.x);
            if (p.y + p.x > 1f) return (p - new Vector2(0.25f, 0.75f)).magnitude - Mathf.Sqrt(2f) / 4f;
            float a = (p - new Vector2(0f, 1f)).sqrMagnitude;
            float b = (p - 0.5f * Mathf.Max(p.x + p.y, 0f) * Vector2.one).sqrMagnitude;
            return Mathf.Sqrt(Mathf.Min(a, b)) * Mathf.Sign(p.x - p.y);
        }

        internal static float Drop(Vector2 p, Vector2 c, float r)
        {
            Vector2 q = p - c;
            float round = q.magnitude - r;
            float tip = Segment(q, new Vector2(0f, r * 1.9f), new Vector2(0f, 0f)) - r * (0.9f - Mathf.Clamp01(q.y / (r * 1.9f)) * 0.9f);
            return Mathf.Min(round, tip);
        }

        internal static float Polygon(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        internal static Vector2 Rotate(Vector2 p, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            return new Vector2(p.x * cs + p.y * sn, -p.x * sn + p.y * cs);
        }

        /// <summary>Paints a shape over what's there, anti-aliased by its distance field.</summary>
        internal static void Layer(ref Color dst, Color src, float distance)
        {
            float a = Mathf.Clamp01(0.5f - distance / Px) * src.a;
            if (a <= 0f) return;
            float outA = a + dst.a * (1f - a);
            Color rgb = (src * a + dst * dst.a * (1f - a)) / Mathf.Max(outA, 0.0001f);
            dst = new Color(rgb.r, rgb.g, rgb.b, outA);
        }
    }
}
