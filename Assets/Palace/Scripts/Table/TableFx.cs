using System.Collections;
using System.Collections.Generic;
using Palace.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// Visual effects for the special cards: ripples, the skip symbol, the 7 arrow, and the
    /// burn (cards spread out and are eaten from the bottom by a glowing flame edge, throwing
    /// off embers). Everything respects the Animations setting; "Off" skips the effects.
    /// </summary>
    public class TableFx : MonoBehaviour
    {
        private class Particle
        {
            public Image Image;
            public Vector2 Velocity;
            public float Life, MaxLife, Size;
        }

        private static readonly Color EmberHot = new Color(1f, 0.92f, 0.55f, 1f);
        private static readonly Color EmberCool = new Color(0.85f, 0.22f, 0.05f, 0f);
        private static readonly Color BubbleHot = new Color(0.9f, 0.98f, 1f, 0.95f);
        private static readonly Color BubbleCool = new Color(0.35f, 0.75f, 1f, 0f);

        /// <summary>Effect theme from the Customise screen: fire and embers, or water.</summary>
        public FxStyle Style = FxStyle.Classic;
        private bool IsWater => Style == FxStyle.Water;
        private const int MaxParticles = 260;

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

        /// <summary>The pile spreads into a row and burns away from the bottom up.</summary>
        public IEnumerator Burn(IReadOnlyList<Card> cards, Vector2 pile, float scale)
        {
            if (!Enabled || cards.Count == 0) yield break;

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
            Ripple(pile + new Vector2(0f, 50f), IsWater ? new Color(0.5f, 0.85f, 1f, 0.9f) : new Color(1f, 0.55f, 0.15f, 0.9f));

            // 2. Burn from the bottom up
            var flames = new List<Image>();
            foreach (var _ in holders)
                flames.Add(UIBuild.Image(IsWater ? "Wave" : "Flame", Layer, FxSprites.Dot,
                    IsWater ? new Color(0.6f, 0.9f, 1f, 0.9f) : new Color(1f, 0.6f, 0.15f, 0.9f), new Vector2(w * 1.4f, 56f), Vector2.zero));

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
                    float flicker = 0.75f + 0.25f * Mathf.Sin(time * 38f + i * 1.7f);
                    flameRt.localScale = new Vector3(flicker, 0.8f + 0.4f * flicker, 1f);
                    flames[i].color = IsWater
                        ? new Color(0.55f + 0.2f * flicker, 0.88f, 1f, 0.9f * (1f - t * 0.3f))
                        : new Color(1f, 0.55f + 0.2f * flicker, 0.15f, 0.9f * (1f - t * 0.3f));

                    Spawn(edge + new Vector2(Random.Range(-w * 0.5f, w * 0.5f), 0f));
                    if (Random.value < 0.5f) Spawn(edge + new Vector2(Random.Range(-w * 0.5f, w * 0.5f), 0f));
                }
                yield return null;
            }

            foreach (var holder in holders) Destroy(holder.gameObject);
            foreach (var flame in flames) Destroy(flame.gameObject);
        }

        // ------------------------------------------------------------------ embers

        private void Spawn(Vector2 at)
        {
            if (live.Count >= MaxParticles) return;
            Particle p = null;
            while (p == null && spare.Count > 0)
            {
                p = spare.Pop();
                if (p.Image == null) p = null; // destroyed when a new game cleared the effects layer
            }
            if (p == null)
            {
                p = new Particle { Image = UIBuild.Image("Ember", Layer, FxSprites.Dot, EmberHot, Vector2.one * 16f, Vector2.zero) };
            }
            p.Image.gameObject.SetActive(true);
            p.Image.sprite = IsWater ? FxSprites.Ring : FxSprites.Dot; // bubbles are hollow rings
            p.Image.rectTransform.SetAsLastSibling();
            p.Image.rectTransform.anchoredPosition = at;
            // Embers fly up fast and small; bubbles drift up slower and bigger.
            p.Velocity = IsWater ? new Vector2(Random.Range(-25f, 25f), Random.Range(40f, 110f)) : new Vector2(Random.Range(-40f, 40f), Random.Range(70f, 190f));
            p.MaxLife = p.Life = IsWater ? Random.Range(0.6f, 1.1f) : Random.Range(0.35f, 0.8f);
            p.Size = IsWater ? Random.Range(14f, 32f) : Random.Range(10f, 24f);
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
                p.Velocity += new Vector2(Random.Range(-60f, 60f), 40f) * dt; // drift and rise
                var rt = p.Image.rectTransform;
                rt.anchoredPosition += p.Velocity * dt;
                rt.sizeDelta = Vector2.one * p.Size * (1f - age * 0.6f);
                p.Image.color = IsWater ? Color.Lerp(BubbleHot, BubbleCool, age) : Color.Lerp(EmberHot, EmberCool, age);
            }
        }
    }
}
