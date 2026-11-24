using UnityEngine;
using static Palace.EmojiArt;

namespace Palace
{
    /// <summary>
    /// Profile pictures: simple round logos painted in code (no downloaded art). The first six are
    /// free; the rest unlock as you level up, which gives levels something to show for themselves.
    /// Chosen on the Customise screen and shown in your seat's circle instead of your initial.
    /// Order matters (online play sends the number). Add new pictures at the end.
    /// </summary>
    public static class AvatarArt
    {
        public static readonly (string name, int level, Color background)[] Pictures =
        {
            ("Spade", 1, new Color(0.16f, 0.20f, 0.38f)),
            ("Heart", 1, new Color(0.62f, 0.12f, 0.16f)),
            ("Diamond", 1, new Color(0.80f, 0.42f, 0.10f)),
            ("Club", 1, new Color(0.14f, 0.42f, 0.24f)),
            ("Star", 1, new Color(0.38f, 0.20f, 0.52f)),
            ("Smile", 1, new Color(0.10f, 0.45f, 0.55f)),
            ("Flame", 2, new Color(0.30f, 0.10f, 0.08f)),
            ("Lightning", 3, new Color(0.18f, 0.18f, 0.24f)),
            ("Moon", 4, new Color(0.10f, 0.14f, 0.32f)),
            ("Sun", 5, new Color(0.30f, 0.62f, 0.86f)),
            ("Cat", 6, new Color(0.55f, 0.36f, 0.62f)),
            ("Ghost", 7, new Color(0.22f, 0.22f, 0.34f)),
            ("Clover", 8, new Color(0.20f, 0.52f, 0.26f)),
            ("Cards", 10, new Color(0.12f, 0.34f, 0.22f)),
            ("Crown", 12, new Color(0.45f, 0.10f, 0.20f)),
            ("Rainbow", 15, new Color(0.32f, 0.62f, 0.90f))
        };

        public static int Count => Pictures.Length;

        public static bool Unlocked(int index) => index >= 0 && index < Count && Progress.Level >= Pictures[index].level;

        private const int N = 128;
        private static readonly Sprite[] cache = new Sprite[16];

        public static Sprite Get(int index)
        {
            if (index < 0 || index >= Count) return null;
            if (cache[index] != null) return cache[index];
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "avatar_" + index };
            var pixels = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    pixels[y * N + x] = Paint(index, (x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return cache[index] = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }

        private static readonly Color White = new Color(0.98f, 0.96f, 0.90f);
        private static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
        private static readonly Vector2[] DiamondShape = { new Vector2(0f, 0.62f), new Vector2(0.42f, 0f), new Vector2(0f, -0.62f), new Vector2(-0.42f, 0f) };
        private static readonly Vector2[] Bolt =
        {
            new Vector2(0.12f, 0.66f), new Vector2(-0.34f, -0.04f), new Vector2(-0.02f, -0.04f),
            new Vector2(-0.14f, -0.66f), new Vector2(0.34f, 0.08f), new Vector2(0.02f, 0.08f)
        };

        private static Color Paint(int index, float u, float v)
        {
            var p = new Vector2(u, v);
            Color c = Color.clear;
            Color bg = Pictures[index].background;

            // Round badge: a light rim and a softly lit background.
            Layer(ref c, new Color(0.96f, 0.86f, 0.60f), Circle(p, Vector2.zero, 0.98f));
            Layer(ref c, Color.Lerp(bg * 0.8f, bg * 1.25f, Mathf.Clamp01(v * 0.5f + 0.5f)), Circle(p, Vector2.zero, 0.9f));

            switch (index)
            {
                case 0: // spade: an upside-down heart and a little stem
                    Layer(ref c, White, Mathf.Min(Heart(new Vector2(p.x, -(p.y - 0.62f)) / 0.95f) * 0.95f,
                        Polygon(p, new[] { new Vector2(0f, -0.05f), new Vector2(0.2f, -0.6f), new Vector2(-0.2f, -0.6f) })));
                    break;
                case 1: Layer(ref c, White, Heart((p + new Vector2(0f, 0.55f)) / 1.0f)); break;
                case 2: Layer(ref c, White, Polygon(p, DiamondShape) - 0.02f); break;
                case 3: // club: three balls and a stem
                {
                    float d = Mathf.Min(Circle(p, new Vector2(0f, 0.3f), 0.25f), Mathf.Min(Circle(p, new Vector2(-0.26f, -0.06f), 0.25f), Circle(p, new Vector2(0.26f, -0.06f), 0.25f)));
                    d = Mathf.Min(d, Polygon(p, new[] { new Vector2(0f, 0.05f), new Vector2(0.18f, -0.6f), new Vector2(-0.18f, -0.6f) }));
                    Layer(ref c, White, d);
                    break;
                }
                case 4: Layer(ref c, Gold, Polygon(p / 0.78f, Star) * 0.78f); break;
                case 5: // smile
                    Layer(ref c, Gold, Circle(p, Vector2.zero, 0.62f));
                    Layer(ref c, new Color(0.36f, 0.2f, 0.06f), Circle(p, new Vector2(-0.22f, 0.16f), 0.08f));
                    Layer(ref c, new Color(0.36f, 0.2f, 0.06f), Circle(p, new Vector2(0.22f, 0.16f), 0.08f));
                    Layer(ref c, new Color(0.36f, 0.2f, 0.06f), Arc(p, new Vector2(0f, 0.02f), 0.34f, 205f, 335f) - 0.05f);
                    break;
                case 6: // flame
                {
                    Vector2 q = p / 0.72f;
                    Layer(ref c, new Color(1f, 0.5f, 0.12f), (Polygon(q, Flame) - 0.04f) * 0.72f);
                    Vector2 r = new Vector2(q.x / 0.55f, (q.y + 0.42f) / 0.55f - 0.25f);
                    Layer(ref c, new Color(1f, 0.88f, 0.35f), (Polygon(r, Flame) - 0.04f) * 0.72f * 0.55f);
                    break;
                }
                case 7: Layer(ref c, Gold, Polygon(p, Bolt) - 0.02f); break;
                case 8: // crescent moon and a star
                    Layer(ref c, new Color(1f, 0.92f, 0.62f), Mathf.Max(Circle(p, new Vector2(-0.06f, 0f), 0.55f), -Circle(p, new Vector2(0.18f, 0.12f), 0.48f)));
                    Layer(ref c, White, Polygon((p - new Vector2(0.38f, -0.32f)) / 0.18f, Star) * 0.18f);
                    break;
                case 9: // sun
                {
                    float rays = float.MaxValue;
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        rays = Mathf.Min(rays, Segment(p, dir * 0.48f, dir * 0.72f) - 0.06f);
                    }
                    Layer(ref c, Gold, rays);
                    Layer(ref c, new Color(1f, 0.86f, 0.3f), Circle(p, Vector2.zero, 0.36f));
                    break;
                }
                case 10: // cat
                {
                    float head = Circle(p, new Vector2(0f, -0.08f), 0.5f);
                    float ears = Mathf.Min(Polygon(p, new[] { new Vector2(-0.46f, 0.12f), new Vector2(-0.42f, 0.62f), new Vector2(-0.08f, 0.36f) }),
                        Polygon(p, new[] { new Vector2(0.46f, 0.12f), new Vector2(0.08f, 0.36f), new Vector2(0.42f, 0.62f) }));
                    Layer(ref c, White, Mathf.Min(head, ears));
                    Color ink = new Color(0.3f, 0.2f, 0.3f);
                    Layer(ref c, ink, Circle(p, new Vector2(-0.18f, -0.02f), 0.07f));
                    Layer(ref c, ink, Circle(p, new Vector2(0.18f, -0.02f), 0.07f));
                    Layer(ref c, new Color(0.95f, 0.5f, 0.6f), Polygon(p, new[] { new Vector2(-0.07f, -0.16f), new Vector2(0.07f, -0.16f), new Vector2(0f, -0.24f) }));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Layer(ref c, ink, Segment(p, new Vector2(s * 0.14f, -0.24f), new Vector2(s * 0.42f, -0.18f)) - 0.012f);
                        Layer(ref c, ink, Segment(p, new Vector2(s * 0.14f, -0.28f), new Vector2(s * 0.42f, -0.32f)) - 0.012f);
                    }
                    break;
                }
                case 11: // friendly ghost
                {
                    float body = Mathf.Min(Circle(p, new Vector2(0f, 0.12f), 0.42f), Box(p - new Vector2(0f, -0.2f), new Vector2(0.42f, 0.32f), 0f));
                    for (int i = 0; i < 3; i++) body = Mathf.Max(body, -Circle(p, new Vector2(-0.28f + i * 0.28f, -0.56f), 0.11f));
                    Layer(ref c, White, body);
                    Layer(ref c, new Color(0.2f, 0.2f, 0.3f), Circle(p, new Vector2(-0.15f, 0.14f), 0.07f));
                    Layer(ref c, new Color(0.2f, 0.2f, 0.3f), Circle(p, new Vector2(0.15f, 0.14f), 0.07f));
                    Layer(ref c, new Color(0.95f, 0.55f, 0.6f, 0.8f), Circle(p, new Vector2(0f, -0.04f), 0.06f));
                    break;
                }
                case 12: // four-leaf clover
                {
                    float d = float.MaxValue;
                    for (int i = 0; i < 4; i++)
                    {
                        float a = Mathf.PI / 4f + i * Mathf.PI / 2f;
                        d = Mathf.Min(d, Circle(p, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.24f + new Vector2(0f, 0.08f), 0.22f));
                    }
                    d = Mathf.Min(d, Segment(p, new Vector2(0f, 0.08f), new Vector2(0.16f, -0.6f)) - 0.05f);
                    Layer(ref c, new Color(0.62f, 0.92f, 0.55f), d);
                    break;
                }
                case 13: // a card with a spade
                {
                    Vector2 q = Rotate(p, -8f);
                    Layer(ref c, new Color(0.3f, 0.24f, 0.18f), Box(q, new Vector2(0.4f, 0.58f), 0.08f));
                    Layer(ref c, White, Box(q, new Vector2(0.37f, 0.55f), 0.07f));
                    Layer(ref c, new Color(0.12f, 0.12f, 0.16f), Mathf.Min(Heart(new Vector2(q.x, -(q.y - 0.3f)) / 0.5f) * 0.5f,
                        Polygon(q, new[] { new Vector2(0f, 0.02f), new Vector2(0.1f, -0.3f), new Vector2(-0.1f, -0.3f) })));
                    break;
                }
                case 14: // crown
                {
                    Vector2 q = p / 0.72f;
                    Layer(ref c, Gold, Polygon(q, Crown) * 0.72f);
                    Layer(ref c, new Color(0.9f, 0.2f, 0.25f), Circle(q, new Vector2(0f, 0.66f), 0.1f) * 0.72f);
                    break;
                }
                default: // rainbow
                {
                    Color[] bands = { new Color(0.92f, 0.25f, 0.25f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.88f, 0.3f), new Color(0.35f, 0.8f, 0.4f), new Color(0.3f, 0.55f, 0.95f) };
                    for (int i = 0; i < bands.Length; i++)
                        Layer(ref c, bands[i], Mathf.Max(Mathf.Abs(Circle(p, new Vector2(0f, -0.3f), 0.68f - i * 0.1f)) - 0.05f, -(p.y + 0.3f)));
                    Layer(ref c, White, Mathf.Min(Circle(p, new Vector2(-0.48f, -0.32f), 0.14f), Circle(p, new Vector2(-0.32f, -0.34f), 0.12f)));
                    Layer(ref c, White, Mathf.Min(Circle(p, new Vector2(0.48f, -0.32f), 0.14f), Circle(p, new Vector2(0.32f, -0.34f), 0.12f)));
                    break;
                }
            }
            return c;
        }
    }
}
