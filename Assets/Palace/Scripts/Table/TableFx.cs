using System.Collections;
using System.Collections.Generic;
using Palace.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// Visual effects for the special cards: ripples, the skip symbol, the 7 arrow, the burn (cards
    /// spread out and are eaten from the bottom by a glowing edge, throwing off particles) and the
    /// win celebrations. Each burn style (Customise > Effects) has its own colours, particle shape
    /// and movement, and every player's burns use their own style, so friends see each other's
    /// effects online. Everything respects the Animations setting: "Off" skips the effects.
    /// </summary>
    public class TableFx : MonoBehaviour
    {
        private class Particle
        {
            public Image Image;
            public Vector2 Velocity;
            public float Life, MaxLife, Size, Gravity, Spin, Wobble, Aspect = 1f;
            public Color Hot, Cool;
        }

        /// <summary>How one burn style looks.</summary>
        private struct Look
        {
            public Color Edge, Ripple, Hot, Cool;
            public Sprite Sprite;
            public Vector2 SpeedX, SpeedY, Life, Size;
            public float Gravity, Wobble, Spin;
            public bool Rainbow;   // each particle gets its own bright colour (confetti)
        }

        /// <summary>Colours and particle shape of a burn style, for the Customise screen preview.</summary>
        public static (Sprite sprite, Color hot, Color cool, Color edge, bool rainbow) Palette(FxStyle style)
        {
            Look l = LookOf(style);
            return (l.Sprite, l.Hot, l.Cool, l.Edge, l.Rainbow);
        }

        public static Color BrightColour(int i) => Bright[((i % Bright.Length) + Bright.Length) % Bright.Length];

        private static Look LookOf(FxStyle style)
        {
            switch (style)
            {
                case FxStyle.Water: return new Look
                {
                    Edge = new Color(0.6f, 0.9f, 1f, 0.9f), Ripple = new Color(0.5f, 0.85f, 1f, 0.9f),
                    Hot = new Color(0.9f, 0.98f, 1f, 0.95f), Cool = new Color(0.35f, 0.75f, 1f, 0f), Sprite = FxSprites.Ring,
                    SpeedX = new Vector2(-25f, 25f), SpeedY = new Vector2(40f, 110f), Life = new Vector2(0.6f, 1.1f), Size = new Vector2(14f, 32f), Gravity = 40f, Wobble = 60f
                };
                case FxStyle.Neon: return new Look
                {
                    Edge = new Color(0.3f, 1f, 1f, 0.95f), Ripple = new Color(1f, 0.3f, 0.9f, 0.95f),
                    Hot = new Color(1f, 0.35f, 0.95f, 1f), Cool = new Color(0.2f, 0.95f, 1f, 0f), Sprite = FxSprites.Dot,
                    SpeedX = new Vector2(-70f, 70f), SpeedY = new Vector2(60f, 170f), Life = new Vector2(0.4f, 0.9f), Size = new Vector2(10f, 22f), Gravity = 30f, Wobble = 90f
                };
                case FxStyle.Frost: return new Look
                {
                    Edge = new Color(0.85f, 0.97f, 1f, 0.95f), Ripple = new Color(0.7f, 0.9f, 1f, 0.9f),
                    Hot = Color.white, Cool = new Color(0.55f, 0.8f, 1f, 0f), Sprite = FxSprites.Sparkle,
                    SpeedX = new Vector2(-35f, 35f), SpeedY = new Vector2(15f, 70f), Life = new Vector2(0.8f, 1.4f), Size = new Vector2(14f, 30f), Gravity = 10f, Wobble = 40f, Spin = 90f
                };
                case FxStyle.GoldRush: return new Look
                {
                    Edge = new Color(1f, 0.85f, 0.35f, 0.95f), Ripple = new Color(1f, 0.82f, 0.3f, 0.95f),
                    Hot = new Color(1f, 0.96f, 0.65f, 1f), Cool = new Color(0.95f, 0.6f, 0.1f, 0f), Sprite = FxSprites.Sparkle,
                    SpeedX = new Vector2(-55f, 55f), SpeedY = new Vector2(80f, 210f), Life = new Vector2(0.5f, 1f), Size = new Vector2(12f, 28f), Gravity = -60f, Wobble = 50f, Spin = 140f
                };
                case FxStyle.Galaxy: return new Look
                {
                    Edge = new Color(0.65f, 0.45f, 1f, 0.95f), Ripple = new Color(0.55f, 0.4f, 1f, 0.9f),
                    Hot = new Color(0.9f, 0.85f, 1f, 1f), Cool = new Color(0.25f, 0.2f, 0.85f, 0f), Sprite = FxSprites.Sparkle,
                    SpeedX = new Vector2(-55f, 55f), SpeedY = new Vector2(30f, 140f), Life = new Vector2(0.7f, 1.3f), Size = new Vector2(8f, 26f), Gravity = 20f, Wobble = 70f, Spin = 60f
                };
                case FxStyle.Toxic: return new Look
                {
                    Edge = new Color(0.55f, 1f, 0.35f, 0.95f), Ripple = new Color(0.5f, 1f, 0.4f, 0.9f),
                    Hot = new Color(0.8f, 1f, 0.45f, 1f), Cool = new Color(0.15f, 0.7f, 0.15f, 0f), Sprite = FxSprites.Ring,
                    SpeedX = new Vector2(-30f, 30f), SpeedY = new Vector2(30f, 100f), Life = new Vector2(0.7f, 1.2f), Size = new Vector2(12f, 30f), Gravity = 30f, Wobble = 80f
                };
                case FxStyle.Confetti: return new Look
                {
                    Edge = new Color(1f, 0.9f, 0.5f, 0.9f), Ripple = new Color(1f, 0.6f, 0.9f, 0.9f), Hot = Color.white, Cool = Color.white,
                    Sprite = null, Rainbow = true,
                    SpeedX = new Vector2(-130f, 130f), SpeedY = new Vector2(150f, 300f), Life = new Vector2(0.9f, 1.5f), Size = new Vector2(9f, 15f), Gravity = -420f, Wobble = 40f, Spin = 420f
                };
                case FxStyle.Lightning: return new Look
                {
                    Edge = new Color(1f, 1f, 0.75f, 1f), Ripple = new Color(0.85f, 0.9f, 1f, 1f),
                    Hot = new Color(1f, 1f, 0.85f, 1f), Cool = new Color(0.55f, 0.65f, 1f, 0f), Sprite = FxSprites.Dot,
                    SpeedX = new Vector2(-170f, 170f), SpeedY = new Vector2(100f, 320f), Life = new Vector2(0.15f, 0.35f), Size = new Vector2(6f, 14f), Gravity = 0f, Wobble = 400f
                };
                case FxStyle.Shadow: return new Look
                {
                    Edge = new Color(0.5f, 0.25f, 0.7f, 0.9f), Ripple = new Color(0.35f, 0.15f, 0.5f, 0.9f),
                    Hot = new Color(0.45f, 0.28f, 0.65f, 0.9f), Cool = new Color(0.05f, 0.02f, 0.1f, 0f), Sprite = FxSprites.Dot,
                    SpeedX = new Vector2(-30f, 30f), SpeedY = new Vector2(25f, 90f), Life = new Vector2(0.8f, 1.4f), Size = new Vector2(22f, 46f), Gravity = 15f, Wobble = 50f
                };
                default: return new Look // Classic: fire and embers
                {
                    Edge = new Color(1f, 0.6f, 0.15f, 0.9f), Ripple = new Color(1f, 0.55f, 0.15f, 0.9f),
                    Hot = new Color(1f, 0.92f, 0.55f, 1f), Cool = new Color(0.85f, 0.22f, 0.05f, 0f), Sprite = FxSprites.Dot,
                    SpeedX = new Vector2(-40f, 40f), SpeedY = new Vector2(70f, 190f), Life = new Vector2(0.35f, 0.8f), Size = new Vector2(10f, 24f), Gravity = 40f, Wobble = 60f
                };
            }
        }

        private static readonly Color[] Bright =
        {
            new Color(1f, 0.3f, 0.35f), new Color(1f, 0.75f, 0.2f), new Color(0.35f, 0.85f, 0.4f),
            new Color(0.3f, 0.6f, 1f), new Color(0.85f, 0.4f, 1f), new Color(1f, 0.55f, 0.85f)
        };

        /// <summary>Your own effect theme (used for the direction ring); each burn passes the burner's style.</summary>
        public FxStyle Style = FxStyle.Classic;
        private const int MaxParticles = 320;

        private TableView table;
        private readonly List<Particle> live = new List<Particle>();
        private readonly Stack<Particle> spare = new Stack<Particle>();

        private static bool Enabled => GameSettings.AnimationScale > 0f;

        public static TableFx Attach(TableView table)
        {
            var fx = table.gameObject.AddComponent<TableFx>();
            fx.table = table;
            return fx;
        }

        private RectTransform Layer => table.FxRoot;

        // ------------------------------------------------------------------ simple effects

        /// <summary>An expanding, fading ring.</summary>
        public void Ripple(Vector2 at, Color color, float delay = 0f)
        {
            if (Enabled) StartCoroutine(RippleRoutine(at, color, delay));
        }

        private IEnumerator RippleRoutine(Vector2 at, Color color, float delay)
        {
            yield return Tween.Wait(delay);
            var img = UIBuild.Image("Ripple", Layer, FxSprites.Ring, color, new Vector2(200f, 200f), at);
            var rt = img.rectTransform;
            yield return Tween.Run(Mathf.Max(0.35f, Tween.Scaled(0.6f)), t =>
            {
                rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 2.4f, t);
                img.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
            });
            Destroy(img.gameObject);
        }

        /// <summary>The "skipped" symbol popping up over a seat.</summary>
        public void SkipIcon(Vector2 at, float delay)
        {
            if (Enabled) StartCoroutine(SkipRoutine(at, delay));
        }

        private IEnumerator SkipRoutine(Vector2 at, float delay)
        {
            yield return Tween.Wait(delay);
            var color = new Color(0.55f, 0.82f, 1f, 1f);
            var img = UIBuild.Image("Skip", Layer, FxSprites.NoEntry, color, new Vector2(120f, 120f), at);
            var rt = img.rectTransform;
            AudioManager.Play(Sfx.Skip, 0.6f);
            yield return Tween.Run(0.25f, t => rt.localScale = Vector3.one * (t < 0.7f ? Mathf.Lerp(0f, 1.25f, t / 0.7f) : Mathf.Lerp(1.25f, 1f, (t - 0.7f) / 0.3f)), x => x);
            yield return Tween.Wait(0.45f);
            yield return Tween.Run(0.3f, t =>
            {
                img.color = new Color(color.r, color.g, color.b, 1f - t);
                rt.localScale = Vector3.one * (1f + 0.3f * t);
            });
            Destroy(img.gameObject);
        }

        /// <summary>A big arrow over the pile bouncing up (HIGHER) or down (LOWER).</summary>
        public void SevenArrow(Vector2 at, bool higher)
        {
            if (Enabled) StartCoroutine(SevenRoutine(at, higher));
        }

        private IEnumerator SevenRoutine(Vector2 at, bool higher)
        {
            var color = new Color(0.98f, 0.8f, 0.3f, 1f);
            var arrows = new List<Image>();
            for (int i = 0; i < 2; i++)
            {
                var img = UIBuild.Image("SevenArrow", Layer, FxSprites.Chevron, color, new Vector2(150f, 150f), at);
                img.rectTransform.localEulerAngles = new Vector3(0f, 0f, higher ? 90f : -90f);
                arrows.Add(img);
            }
            float dir = higher ? 1f : -1f;
            yield return Tween.Run(Mathf.Max(0.6f, Tween.Scaled(1.0f)), t =>
            {
                for (int i = 0; i < arrows.Count; i++)
                {
                    float phase = Mathf.Repeat(t * 2f + i * 0.5f, 1f);
                    arrows[i].rectTransform.anchoredPosition = at + new Vector2(0f, dir * Mathf.Lerp(-60f, 90f, phase));
                    arrows[i].color = new Color(color.r, color.g, color.b, Mathf.Sin(phase * Mathf.PI) * (1f - t * 0.6f));
                }
            }, x => x);
            foreach (var a in arrows) Destroy(a.gameObject);
        }

        // ------------------------------------------------------------------ burn

        /// <summary>The pile spreads into a row and burns away from the bottom up, in the burner's style.</summary>
        public IEnumerator Burn(IReadOnlyList<Card> cards, Vector2 pile, float scale, FxStyle style)
        {
            if (!Enabled || cards.Count == 0) yield break;
            Look look = LookOf(style);

            int n = cards.Count;
            float w = CardView.Width * scale, h = CardView.Height * scale;
            float spacing = n > 1 ? Mathf.Min(w * 0.8f, 520f / (n - 1)) : 0f;

            var holders = new List<RectTransform>();
            var targets = new List<Vector2>();
            Vector2 start = pile + new Vector2(0f, h * 0.5f); // holders are pinned at their top edge
            for (int i = 0; i < n; i++)
            {
                RectTransform holder = UIBuild.Rect("Burning", Layer, new Vector2(w, h), start);
                holder.pivot = new Vector2(0.5f, 1f);
                holder.anchoredPosition = start;
                holder.gameObject.AddComponent<RectMask2D>();

                CardView card = CardFactory.Spawn(table.CardPrefab, table.CardTheme, holder);
                card.SetInteractable(false);
                card.ShowCard(cards[i], true);
                var crt = (RectTransform)card.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
                crt.pivot = new Vector2(0.5f, 1f);
                crt.anchoredPosition = Vector2.zero;
                crt.localScale = Vector3.one * scale;

                holders.Add(holder);
                targets.Add(start + new Vector2((i - (n - 1) * 0.5f) * spacing, 50f));
            }

            // 1. Lift and fan out
            yield return Tween.Run(Mathf.Max(0.15f, Tween.Scaled(0.28f)), t =>
            {
                for (int i = 0; i < n; i++) holders[i].anchoredPosition = Vector2.LerpUnclamped(start, targets[i], t);
            });
            Ripple(pile + new Vector2(0f, 50f), look.Ripple);
            if (style == FxStyle.Lightning) Ripple(pile + new Vector2(0f, 50f), new Color(1f, 1f, 1f, 0.8f), 0.08f);

            // 2. Burn from the bottom up
            var flames = new List<Image>();
            foreach (var _ in holders)
                flames.Add(UIBuild.Image("Edge", Layer, FxSprites.Dot, look.Edge, new Vector2(w * 1.4f, 56f), Vector2.zero));

            float duration = Mathf.Max(0.55f, Tween.Scaled(1.0f));
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                float remaining = h * (1f - t * t * (3f - 2f * t));

                for (int i = 0; i < n; i++)
                {
                    holders[i].sizeDelta = new Vector2(w, remaining);
                    Vector2 edge = holders[i].anchoredPosition - new Vector2(0f, remaining);
                    var flameRt = flames[i].rectTransform;
                    flameRt.anchoredPosition = edge;
                    float flicker = style == FxStyle.Lightning
                        ? (Random.value < 0.3f ? 1.4f : 0.6f)
                        : 0.75f + 0.25f * Mathf.Sin(time * 38f + i * 1.7f);
                    flameRt.localScale = new Vector3(flicker, 0.8f + 0.4f * flicker, 1f);
                    Color e = look.Edge;
                    flames[i].color = new Color(e.r, e.g, e.b, e.a * Mathf.Clamp01(0.75f + 0.25f * flicker) * (1f - t * 0.3f));

                    Spawn(look, edge + new Vector2(Random.Range(-w * 0.5f, w * 0.5f), 0f));
                    if (Random.value < 0.5f) Spawn(look, edge + new Vector2(Random.Range(-w * 0.5f, w * 0.5f), 0f));
                }
                yield return null;
            }

            foreach (var holder in holders) Destroy(holder.gameObject);
            foreach (var flame in flames) Destroy(flame.gameObject);
        }

        // ------------------------------------------------------------------ win celebrations

        /// <summary>Plays a win celebration around a point (usually the winner's seat).</summary>
        public void Celebrate(Vector2 at, CelebrationStyle style)
        {
            if (Enabled) StartCoroutine(CelebrateRoutine(at, style));
        }

        private IEnumerator CelebrateRoutine(Vector2 at, CelebrationStyle style)
        {
            switch (style)
            {
                case CelebrationStyle.Fireworks:
                    for (int burst = 0; burst < 5; burst++)
                    {
                        Vector2 p = at + new Vector2(Random.Range(-320f, 320f), Random.Range(60f, 300f));
                        Color c = Bright[Random.Range(0, Bright.Length)];
                        Ripple(p, new Color(c.r, c.g, c.b, 0.7f));
                        AudioManager.Play(Sfx.Burn, 0.25f);
                        for (int i = 0; i < 36; i++)
                        {
                            float a = i / 36f * Mathf.PI * 2f;
                            float speed = Random.Range(180f, 300f);
                            Emit(p, FxSprites.Dot, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, Random.Range(0.8f, 1.2f), Random.Range(10f, 18f),
                                Color.Lerp(Color.white, c, 0.4f), new Color(c.r, c.g, c.b, 0f), -160f, 0f, 20f);
                        }
                        yield return new WaitForSecondsRealtime(0.28f);
                    }
                    break;

                case CelebrationStyle.CoinShower:
                    for (int i = 0; i < 70; i++)
                    {
                        Vector2 p = at + new Vector2(Random.Range(-420f, 420f), Random.Range(260f, 420f));
                        Emit(p, FxSprites.Coin, new Vector2(Random.Range(-40f, 40f), Random.Range(-60f, 0f)), Random.Range(1.4f, 2f), Random.Range(26f, 40f),
                            new Color(1f, 0.85f, 0.3f, 1f), new Color(1f, 0.7f, 0.15f, 0f), -520f, Random.Range(-200f, 200f), 10f, 0.55f);
                        if (i % 6 == 0) AudioManager.Play(Sfx.CardDraw, 0.2f);
                        yield return new WaitForSecondsRealtime(0.02f);
                    }
                    break;

                case CelebrationStyle.Starburst:
                    Ripple(at, new Color(1f, 0.85f, 0.4f, 0.9f));
                    Ripple(at, new Color(1f, 1f, 1f, 0.7f), 0.12f);
                    for (int ring = 0; ring < 2; ring++)
                    {
                        for (int i = 0; i < 28; i++)
                        {
                            float a = (i + ring * 0.5f) / 28f * Mathf.PI * 2f;
                            float speed = ring == 0 ? 340f : 220f;
                            Emit(at, FxSprites.Sparkle, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, 1f, ring == 0 ? 30f : 22f,
                                new Color(1f, 0.97f, 0.7f, 1f), new Color(1f, 0.7f, 0.2f, 0f), 0f, 180f, 0f);
                        }
                        yield return new WaitForSecondsRealtime(0.15f);
                    }
                    break;

                case CelebrationStyle.Bubbles:
                    for (int i = 0; i < 60; i++)
                    {
                        Vector2 p = at + new Vector2(Random.Range(-380f, 380f), Random.Range(-140f, 40f));
                        Emit(p, FxSprites.Ring, new Vector2(Random.Range(-20f, 20f), Random.Range(60f, 140f)), Random.Range(1.3f, 2.2f), Random.Range(16f, 44f),
                            new Color(0.9f, 0.98f, 1f, 0.9f), new Color(0.5f, 0.85f, 1f, 0f), 30f, 0f, 70f);
                        yield return new WaitForSecondsRealtime(0.025f);
                    }
                    break;

                default: // confetti
                    for (int i = 0; i < 130; i++)
                    {
                        Color c = Bright[Random.Range(0, Bright.Length)];
                        float a = Random.Range(0.15f, 0.85f) * Mathf.PI;
                        Emit(at, null, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(250f, 560f), Random.Range(1.3f, 2.1f), Random.Range(10f, 16f),
                            c, new Color(c.r, c.g, c.b, 0f), -600f, Random.Range(-500f, 500f), 50f, 0.55f);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ particles

        private void Spawn(Look look, Vector2 at)
        {
            Color hot = look.Hot, cool = look.Cool;
            if (look.Rainbow)
            {
                hot = Bright[Random.Range(0, Bright.Length)];
                cool = new Color(hot.r, hot.g, hot.b, 0f);
            }
            Emit(at, look.Sprite, new Vector2(Random.Range(look.SpeedX.x, look.SpeedX.y), Random.Range(look.SpeedY.x, look.SpeedY.y)),
                Random.Range(look.Life.x, look.Life.y), Random.Range(look.Size.x, look.Size.y), hot, cool, look.Gravity,
                look.Spin > 0f ? Random.Range(-look.Spin, look.Spin) : 0f, look.Wobble, look.Rainbow ? 0.55f : 1f);
        }

        private void Emit(Vector2 at, Sprite sprite, Vector2 velocity, float life, float size, Color hot, Color cool,
            float gravity, float spin, float wobble, float aspect = 1f)
        {
            if (live.Count >= MaxParticles) return;
            Particle p = null;
            while (p == null && spare.Count > 0)
            {
                p = spare.Pop();
                if (p.Image == null) p = null; // destroyed when a new game cleared the effects layer
            }
            if (p == null) p = new Particle { Image = UIBuild.Image("Particle", Layer, null, Color.white, Vector2.one * 16f, Vector2.zero) };
            p.Image.gameObject.SetActive(true);
            p.Image.sprite = sprite; // null = a plain square (confetti)
            var rt = p.Image.rectTransform;
            rt.SetAsLastSibling();
            rt.anchoredPosition = at;
            rt.localEulerAngles = new Vector3(0f, 0f, sprite == FxSprites.Dot ? 0f : Random.Range(0f, 360f));
            p.Velocity = velocity;
            p.MaxLife = p.Life = life;
            p.Size = size;
            p.Hot = hot;
            p.Cool = cool;
            p.Gravity = gravity;
            p.Spin = spin;
            p.Wobble = wobble;
            p.Aspect = aspect;
            live.Add(p);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Particle p = live[i];
                p.Life -= dt;
                if (p.Life <= 0f || p.Image == null)
                {
                    if (p.Image != null) p.Image.gameObject.SetActive(false);
                    live.RemoveAt(i);
                    if (p.Image != null) spare.Push(p);
                    continue;
                }

                float age = 1f - p.Life / p.MaxLife;
                p.Velocity += new Vector2(Random.Range(-p.Wobble, p.Wobble), p.Gravity) * dt; // drift, and rise or fall
                var rt = p.Image.rectTransform;
                rt.anchoredPosition += p.Velocity * dt;
                if (p.Spin != 0f) rt.localEulerAngles += new Vector3(0f, 0f, p.Spin * dt);
                bool keepsSize = p.Aspect < 1f; // confetti and coins don't shrink, they fade at the end
                float s = p.Size * (keepsSize ? 1f : 1f - age * 0.6f);
                rt.sizeDelta = new Vector2(s, s * p.Aspect);
                p.Image.color = Color.Lerp(p.Hot, p.Cool, keepsSize ? Mathf.Max(0f, age * 2f - 1f) : age);
            }
        }
    }
}
