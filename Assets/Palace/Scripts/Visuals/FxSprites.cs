using System;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Small white shapes for effects (arrows, rings, sparks), drawn in code the first time
    /// they're needed. White so they can be tinted any colour.
    /// </summary>
    public static class FxSprites
    {
        private static Sprite chevron, ring, noEntry, dot;

        /// <summary>A chevron pointing right (rotate it to point anywhere).</summary>
        public static Sprite Chevron => chevron != null ? chevron : chevron = Make("fx_chevron", 128, (x, y) =>
            Mathf.Min(Segment(x, y, 38, 20, 94, 64), Segment(x, y, 94, 64, 38, 108)) - 13f);

        public static Sprite Ring => ring != null ? ring : ring = Make("fx_ring", 128, (x, y) =>
            Mathf.Abs(Length(x - 64, y - 64) - 56f) - 5f);

        /// <summary>Circle with a diagonal bar: the "skipped" symbol.</summary>
        public static Sprite NoEntry => noEntry != null ? noEntry : noEntry = Make("fx_no_entry", 128, (x, y) =>
            Mathf.Min(Mathf.Abs(Length(x - 64, y - 64) - 50f) - 9f, Segment(x, y, 30, 98, 98, 30) - 9f));

        private static Sprite sparkle, coin;

        /// <summary>A four-pointed twinkle (frost, gold, galaxy, starburst).</summary>
        public static Sprite Sparkle => sparkle != null ? sparkle : sparkle = MakeAlpha("fx_sparkle", 64, (x, y) =>
        {
            float dx = Mathf.Abs(x - 32f) / 32f, dy = Mathf.Abs(y - 32f) / 32f;
            float star = 1f - Mathf.Clamp01(Mathf.Sqrt(dx) + Mathf.Sqrt(dy));          // pinched four-point shape
            float core = Mathf.Clamp01(1f - Length(x - 32f, y - 32f) / 9f);
            return Mathf.Clamp01(star * 2.2f + core);
        });

        /// <summary>A coin: a solid disc with a lighter rim (tint it gold).</summary>
        public static Sprite Coin => coin != null ? coin : coin = MakeAlpha("fx_coin", 64, (x, y) =>
        {
            float r = Length(x - 32f, y - 32f);
            float disc = Mathf.Clamp01(29.5f - r);
            float groove = Mathf.Abs(r - 22f) < 1.6f ? 0.55f : 1f;   // the inner ring reads as a coin edge
            return disc * groove;
        });

        /// <summary>Soft round glow, used for embers and flames.</summary>
        public static Sprite Dot
        {
            get
            {
                if (dot != null) return dot;
                dot = MakeAlpha("fx_dot", 64, (x, y) =>
                {
                    float r = Mathf.Clamp01(Length(x - 32, y - 32) / 32f);
                    return (1f - r) * (1f - r);
                });
                return dot;
            }
        }

        // ------------------------------------------------------------------

        private static float Length(float x, float y) => Mathf.Sqrt(x * x + y * y);

        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            return Length(pax - bax * h, pay - bay * h);
        }

        private static Sprite Make(string name, int size, Func<float, float, float> distance)
        {
            return MakeAlpha(name, size, (x, y) => Mathf.Clamp01(0.5f - distance(x, y)));
        }

        private static Sprite MakeAlpha(string name, int size, Func<float, float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha(x + 0.5f, y + 0.5f) * 255f));
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
