using System;
using UnityEngine;

namespace Palace.EditorTools
{
    /// <summary>
    /// The premium designs, as pure functions of position: (x, y) in "height units" (0..1 up the
    /// card or felt, 0..aspect across). Used for premium card backs and premium tables. Painted in
    /// code, so nothing is copied from anywhere. Noise comes in through <see cref="Noise"/>.
    /// </summary>
    public static class PalacePremiumPatterns
    {
        /// <summary>Layered noise, 0..1: (x, y, octaves, seed).</summary>
        public static Func<float, float, int, int, float> Noise;

        public static readonly string[] Ids = { "gold", "carbon", "neon", "damask", "marble", "galaxy", "dragon", "holo" };

        public static Color Pattern(string id, float x, float y)
        {
            switch (id)
            {
                case "gold": return GoldFiligree(x, y);
                case "carbon": return Carbon(x, y);
                case "neon": return NeonGrid(x, y);
                case "damask": return Damask(x, y);
                case "marble": return Marble(x, y);
                case "galaxy": return Galaxy(x, y);
                case "dragon": return Dragon(x, y);
                case "holo": return Holo(x, y);
                default: return Color.magenta;
            }
        }

        // ------------------------------------------------------------------ the designs

        /// <summary>Black lacquer with flowing gold filigree and a gold medallion in the middle.</summary>
        private static Color GoldFiligree(float x, float y)
        {
            float n = Noise(x * 6f, y * 6f, 3, 401);
            Color c = Lerp(Hex(0x120B04), Hex(0x2C1D0A), 0.4f + (n - 0.5f) * 0.8f);
            float f = Mathf.Sin(x * 15f + 2.2f * Mathf.Sin(y * 9f)) + Mathf.Sin(y * 15f + 2.2f * Mathf.Sin(x * 9f));
            float line = 1f - Smooth(0.02f, 0.14f, Mathf.Abs(f));
            Color gold = Lerp(Hex(0x8A6420), Hex(0xF6DC96), Noise(x * 9f, y * 9f, 2, 402) * 1.3f);
            c = Lerp(c, gold, line * 0.9f);
            return c;
        }

        /// <summary>Woven carbon fibre with a soft sheen.</summary>
        private static Color Carbon(float x, float y)
        {
            const float cell = 0.05f;
            int ix = Mathf.FloorToInt(x / cell), iy = Mathf.FloorToInt(y / cell);
            float fx = x / cell - ix, fy = y / cell - iy;
            bool across = ((ix + iy) & 1) == 0;
            float thread = Mathf.Sin((across ? fy : fx) * Mathf.PI);
            float edge = Mathf.Sin((across ? fx : fy) * Mathf.PI);
            float v = 0.07f + 0.13f * thread * (0.55f + 0.45f * edge);
            v += 0.05f * (0.5f + 0.5f * Mathf.Sin((x + y) * 4f)); // sheen across the weave
            return new Color(v, v * 1.02f, v * 1.1f, 1f);
        }

        /// <summary>Dark glass with a glowing neon grid: pink one way, cyan the other.</summary>
        private static Color NeonGrid(float x, float y)
        {
            const float step = 0.1f;
            float gx = Mathf.Abs(x / step - Mathf.Round(x / step)) * step;
            float gy = Mathf.Abs(y / step - Mathf.Round(y / step)) * step;
            Color c = Hex(0x07060F);
            c = Add(c, Hex(0xFF2BD6), Mathf.Exp(-gx * 140f) * 0.9f + Mathf.Exp(-gx * 30f) * 0.15f);
            c = Add(c, Hex(0x29F3FF), Mathf.Exp(-gy * 140f) * 0.9f + Mathf.Exp(-gy * 30f) * 0.15f);
            return Clamp(c);
        }

        /// <summary>Royal purple velvet with a repeating gold damask rosette.</summary>
        private static Color Damask(float x, float y)
        {
            float n = Noise(x * 8f, y * 8f, 3, 411);
            Color c = Lerp(Hex(0x24093A), Hex(0x4A1E63), 0.45f + (n - 0.5f) * 0.6f);
            const float tile = 0.26f;
            int row = Mathf.FloorToInt(y / tile);
            float sx = x / tile + ((row & 1) == 0 ? 0f : 0.5f);
            float fx = sx - Mathf.Floor(sx) - 0.5f, fy = y / tile - row - 0.5f;
            float r = Mathf.Sqrt(fx * fx + fy * fy), th = Mathf.Atan2(fy, fx);
            // A solid six-petal flower with a velvet ring inside it and a gold centre: a damask rosette.
            float flower = r - (0.2f + 0.1f * Mathf.Abs(Mathf.Cos(3f * th)));
            float ring = Mathf.Abs(r - 0.1f) - 0.03f;
            float centre = r - 0.045f;
            float tip = Mathf.Sqrt(fx * fx + (Mathf.Abs(fy) - 0.42f) * (Mathf.Abs(fy) - 0.42f)) - 0.03f; // little diamonds between rosettes
            Color gold = Lerp(Hex(0x9C7A3C), Hex(0xE8C77E), Noise(x * 10f, y * 10f, 2, 412));
            float cover = 1f - Smooth(-0.006f, 0.01f, Mathf.Min(flower, tip));
            cover *= Smooth(-0.006f, 0.01f, -ring) > 0.5f ? 0.35f : 1f;   // the velvet ring shows through
            cover = Mathf.Max(cover, 1f - Smooth(-0.006f, 0.01f, centre));
            c = Lerp(c, gold, cover * 0.85f);
            return c;
        }

        /// <summary>White marble with grey veins and a few fine gold ones.</summary>
        private static Color Marble(float x, float y)
        {
            float cloud = Noise(x * 3f, y * 3f, 4, 421);
            Color c = Lerp(Hex(0xDCD8D2), Hex(0xF7F5F1), 0.5f + (cloud - 0.5f) * 0.9f);
            float warp = Noise(x * 2.5f, y * 2.5f, 5, 422) * 5f;
            float vein = Mathf.Abs(Mathf.Sin((x * 1.8f + y * 1.2f + warp) * Mathf.PI));
            c = Lerp(c, Hex(0xA9A4A8), (1f - Smooth(0.0f, 0.3f, vein)) * 0.45f);   // soft shadow round each vein
            c = Lerp(c, Hex(0x6E6A72), (1f - Smooth(0.0f, 0.07f, vein)) * 0.85f);  // the vein itself
            float warp2 = Noise(x * 4f + 7f, y * 4f, 4, 423) * 4f;
            float vein2 = Mathf.Abs(Mathf.Sin((x * 1.1f - y * 1.6f + warp2) * Mathf.PI));
            c = Lerp(c, Hex(0xC9A55A), (1f - Smooth(0.0f, 0.03f, vein2)) * 0.9f);
            return c;
        }

        /// <summary>Deep space: a purple and teal nebula full of stars.</summary>
        private static Color Galaxy(float x, float y)
        {
            Color c = Lerp(Hex(0x05040F), Hex(0x140A30), y);
            float neb = Noise(x * 2.2f, y * 2.2f, 5, 431);
            float neb2 = Noise(x * 3.1f + 5f, y * 3.1f, 4, 432);
            c = Lerp(c, Hex(0x6A2C96), Smooth(0.45f, 0.85f, neb) * 0.8f);
            c = Lerp(c, Hex(0x1C7A96), Smooth(0.5f, 0.9f, neb2) * 0.6f);
            c = Add(c, Star(x, y, 0.012f, 0.985f), 1f);
            c = Add(c, Star(x + 3.3f, y + 1.7f, 0.035f, 0.993f) * 1.4f, 1f);
            return Clamp(c);
        }

        /// <summary>Overlapping dragon scales in jewel green, each with a dark rim and a highlight.</summary>
        private static Color Dragon(float x, float y)
        {
            const float w = 0.075f, h = 0.06f;
            Color best = Hex(0x041A15);
            // A scale is a circle hanging from each cell; check this row and the one above (they overlap).
            for (int k = 0; k < 2; k++)
            {
                int row = Mathf.FloorToInt(y / h) + k;
                float offset = (row & 1) == 0 ? 0f : 0.5f;
                float cx = (Mathf.Round(x / w - offset) + offset) * w, cy = row * h;
                float dx = (x - cx) / w, dy = (y - cy) / h;
                float d = Mathf.Sqrt(dx * dx * 1.1f + dy * dy * 0.55f);
                if (d > 0.62f || dy > 0.1f) continue;
                float t = Mathf.Clamp01(-dy * 0.9f + 0.2f);
                Color scale = Lerp(Hex(0x0E4A3E), Hex(0x3FC79F), t);
                scale = Lerp(scale, Hex(0x031410), Smooth(0.5f, 0.62f, d));          // dark rim
                scale = Add(scale, Hex(0xC8FFE9), Mathf.Exp(-((dx + 0.15f) * (dx + 0.15f) + (dy + 0.25f) * (dy + 0.25f)) * 18f) * 0.35f);
                best = scale;
                break;
            }
            return Clamp(best);
        }

        /// <summary>Holographic foil: pastel rainbow that shifts across the surface, with glints.</summary>
        private static Color Holo(float x, float y)
        {
            float hue = Frac((x * 0.9f + y * 0.6f) + Noise(x * 2f, y * 2f, 3, 441) * 0.5f);
            Color c = Color.HSVToRGB(hue, 0.42f, 0.97f);
            float band = 0.5f + 0.5f * Mathf.Sin((x - y) * 18f + Noise(x * 4f, y * 4f, 2, 442) * 6f);
            c = Lerp(c, Color.white, band * band * 0.35f);
            c = Add(c, Star(x, y, 0.02f, 0.99f), 0.8f);
            return Clamp(c);
        }

        // ------------------------------------------------------------------ helpers

        private static Color Star(float x, float y, float cell, float threshold)
        {
            int ix = Mathf.FloorToInt(x / cell), iy = Mathf.FloorToInt(y / cell);
            float h = Hash(ix, iy);
            if (h < threshold) return Color.black;
            float px = (Hash(ix + 17, iy) - 0.5f) * 0.6f, py = (Hash(ix, iy + 31) - 0.5f) * 0.6f;
            float fx = x / cell - ix - 0.5f - px, fy = y / cell - iy - 0.5f - py;
            float b = Mathf.Exp(-(fx * fx + fy * fy) * 40f) * (0.6f + (h - threshold) / (1f - threshold) * 0.6f);
            return new Color(b, b, b * 1.05f, 0f);
        }

        private static float Hash(int x, int y)
        {
            float s = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        private static float Frac(float v) => v - Mathf.Floor(v);
        private static float Smooth(float a, float b, float v) { float t = Mathf.Clamp01((v - a) / (b - a)); return t * t * (3f - 2f * t); }
        private static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));
        private static Color Add(Color a, Color b, float k) => new Color(a.r + b.r * k, a.g + b.g * k, a.b + b.b * k, 1f);
        private static Color Clamp(Color c) => new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }
}
