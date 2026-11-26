using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Palace
{
    /// <summary>
    /// Big sweep-across announcements:
    ///  - LAST CARD: when a player is down to their very last card (hand and table).
    ///  - WINNER: when the first player goes out.
    ///  - SHEDHEAD: when the last player is left holding cards.
    /// A dark band sweeps across the screen with accent stripes racing in from both sides, the player's name
    /// above, and the title arriving below. It holds for a moment, then whooshes away.
    /// Doesn't block the game, and doesn't catch taps. One at a time: LAST CARD banners queue, and a WINNER or
    /// SHEDHEAD banner cuts any banner still showing short (and drops queued LAST CARD ones, which are old news).
    /// </summary>
    public static class LastCardBanner
    {
        private enum Kind { LastCard, Win, Shedhead }

        private static GameObject current; // the banner on screen (null once it's gone, even if a coroutine was killed)
        private static int generation;     // goes up when a winner or Shedhead banner takes over

        /// <summary>The LAST CARD banner. Gold for you, red for everyone else.</summary>
        public static IEnumerator Play(RectTransform parent, string playerName, bool isYou)
        {
            int token = generation;
            while (current != null) yield return null; // one at a time
            if (token != generation) yield break;     // a bigger banner came along: this one is old news
            yield return Run(parent, playerName, isYou, Kind.LastCard, token);
        }

        /// <summary>The WINNER banner, for the first player out. Gold, bigger, with a couple of proud pulses.</summary>
        public static IEnumerator PlayWin(RectTransform parent, string playerName, bool isYou) =>
            TakeOver(parent, playerName, isYou, Kind.Win);

        /// <summary>The SHEDHEAD banner, for the player left holding cards. Purple; the title drops in and droops.</summary>
        public static IEnumerator PlayShedhead(RectTransform parent, string playerName, bool isYou) =>
            TakeOver(parent, playerName, isYou, Kind.Shedhead);

        private static IEnumerator TakeOver(RectTransform parent, string playerName, bool isYou, Kind kind)
        {
            int token = ++generation; // cuts short whatever is showing
            while (current != null) yield return null;
            if (token != generation) yield break;
            yield return Run(parent, playerName, isYou, kind, token);
        }

        private static IEnumerator Run(RectTransform parent, string playerName, bool isYou, Kind kind, int token)
        {
            if (parent == null) yield break;
            bool win = kind == Kind.Win, shed = kind == Kind.Shedhead, big = win || shed;
            Color accent = shed ? new Color(0.74f, 0.5f, 1f)
                : win || isYou ? new Color(1f, 0.78f, 0.25f) : new Color(1f, 0.22f, 0.18f);
            float bandH = big ? 220f : 190f;
            const float y = 70f;

            var root = UIBuild.Stretch(kind + "Banner", parent);
            root.SetAsLastSibling();
            GameObject go = root.gameObject;
            current = go;
            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var holder = UIBuild.Rect("Holder", root, new Vector2(2600f, bandH + 40f), new Vector2(0f, y));

            // The band, two accent stripes and a light glint.
            var band = UIBuild.Image("Band", holder, null, new Color(0.04f, 0.04f, 0.06f, 0.9f), new Vector2(0f, bandH), Vector2.zero);
            float stripeH = big ? 9f : 7f;
            var stripeTop = UIBuild.Image("StripeTop", holder, null, accent, new Vector2(2600f, stripeH), new Vector2(-2600f, bandH * 0.5f));
            var stripeBottom = UIBuild.Image("StripeBottom", holder, null, accent, new Vector2(2600f, stripeH), new Vector2(2600f, -bandH * 0.5f));
            band.gameObject.AddComponent<RectMask2D>(); // the glint only shines across the band
            var glint = UIBuild.Image("Glint", band.transform, null, new Color(1f, 1f, 1f, win ? 0.22f : 0.16f), new Vector2(90f, bandH * 1.6f), new Vector2(-1500f, 0f));
            glint.rectTransform.localEulerAngles = new Vector3(0f, 0f, -22f);
            foreach (var img in new[] { band, stripeTop, stripeBottom, glint }) img.raycastTarget = false;

            string name = (playerName ?? "").ToUpperInvariant();
            string who = shed ? (isYou ? Loc.T("YOU'RE THE") : Loc.F("{0} IS THE", name))
                : isYou ? Loc.T("YOU") : name;
            var nameText = UIBuild.Text("Name", holder, who, big ? 40f : 36f, new Color(1f, 1f, 1f, 0.92f), new Vector2(1400f, 54f),
                new Vector2(0f, big ? 60f : 50f), TextAlignmentOptions.Center, UIKit.Font, true, false);
            nameText.alpha = 0f;
            string titleText = win ? Loc.T(isYou ? "WIN!" : "WINS!") : shed ? Loc.T("SHEDHEAD!") : Loc.T("LAST CARD");
            var title = UIBuild.Text("Title", holder, titleText, big ? 112f : 96f, accent, new Vector2(1600f, 140f),
                new Vector2(0f, big ? -22f : -18f), TextAlignmentOptions.Center, UIKit.Font, true, false);
            title.outlineWidth = 0.2f;
            title.outlineColor = new Color32(0, 0, 0, 220);
            title.alpha = 0f;
            var titleRt = title.rectTransform;
            Vector2 titleHome = titleRt.anchoredPosition;

            Func<bool> cut = () => token != generation || go == null;

            if (UIMotion.Enabled)
            {
                if (kind == Kind.LastCard) AudioManager.Play(Sfx.Reveal, 0.9f);
                // 1. The band opens from the middle while the stripes race in from opposite sides.
                yield return Step(Tween.Run(0.28f, t =>
                {
                    band.rectTransform.sizeDelta = new Vector2(2600f * t, bandH * Mathf.Lerp(0.4f, 1f, t));
                    stripeTop.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-2600f, 0f, t), bandH * 0.5f);
                    stripeBottom.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(2600f, 0f, t), -bandH * 0.5f);
                }), cut);
                // 2. The name slides in with wide letter spacing that tightens up.
                if (!cut()) yield return Step(Tween.Run(0.22f, t =>
                {
                    nameText.alpha = t;
                    nameText.characterSpacing = Mathf.Lerp(40f, 12f, t);
                }), cut);

                if (shed)
                {
                    // 3. SHEDHEAD! drops in from above and bounces, then droops to one side.
                    if (!cut())
                    {
                        AudioManager.Play(Sfx.PickUp, 0.8f);
                        yield return Step(Tween.Run(0.42f, t =>
                        {
                            title.alpha = Mathf.Clamp01(t * 3f);
                            titleRt.anchoredPosition = titleHome + new Vector2(0f, Mathf.LerpUnclamped(260f, 0f, UIMotion.BackOut(t)));
                        }, x => x), cut);
                    }
                    if (!cut()) yield return Step(Tween.Run(0.5f, t =>
                        titleRt.localEulerAngles = new Vector3(0f, 0f, -6f * t + 3f * Mathf.Sin(t * Mathf.PI * 3f) * (1f - t))), cut);
                    if (!cut()) yield return Step(Tween.Run(0.55f, t => glint.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-1500f, 1500f, t), 0f), x => x), cut);
                    if (!cut()) yield return Step(Tween.Wait(0.9f), cut);
                }
                else
                {
                    // 3. The title slams in from big to normal, with a short shake on landing.
                    if (!cut())
                    {
                        if (win) AudioManager.Play(Sfx.Win, 0.85f);
                        else AudioManager.Play(Sfx.Burn, 0.6f);
                        yield return Step(Tween.Run(0.24f, t =>
                        {
                            title.alpha = Mathf.Clamp01(t * 2f);
                            titleRt.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, t);
                        }, x => x * x), cut);
                    }
                    float shake = 0f, shakeTime = win ? 0.24f : 0.18f;
                    while (!cut() && shake < shakeTime)
                    {
                        shake += Time.unscaledDeltaTime;
                        float k = 1f - shake / shakeTime;
                        holder.anchoredPosition = new Vector2(Random.Range(-9f, 9f) * k, y + Random.Range(-6f, 6f) * k);
                        yield return null;
                    }
                    if (!cut()) holder.anchoredPosition = new Vector2(0f, y);
                    // 4. A light glint sweeps across while it holds. The winner's title gives a couple of proud pulses.
                    if (!cut()) yield return Step(Tween.Run(0.55f, t => glint.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-1500f, 1500f, t), 0f), x => x), cut);
                    if (win && !cut())
                        yield return Step(Tween.Run(0.7f, t => titleRt.localScale = Vector3.one * (1f + 0.07f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f))), x => x), cut);
                    if (!cut()) yield return Step(Tween.Wait(win ? 0.8f : 0.9f), cut);
                }
                // 5. Whoosh away to the right.
                if (!cut()) yield return Step(Tween.Run(0.3f, t =>
                {
                    holder.anchoredPosition = new Vector2(Mathf.Lerp(0f, 900f, t * t), y);
                    group.alpha = 1f - t;
                }, x => x), cut);
            }
            else
            {
                // Animations off: just show it for a moment.
                if (win) AudioManager.Play(Sfx.Win, 0.85f);
                if (shed) AudioManager.Play(Sfx.PickUp, 0.8f);
                band.rectTransform.sizeDelta = new Vector2(2600f, bandH);
                stripeTop.rectTransform.anchoredPosition = new Vector2(0f, bandH * 0.5f);
                stripeBottom.rectTransform.anchoredPosition = new Vector2(0f, -bandH * 0.5f);
                nameText.alpha = 1f;
                title.alpha = 1f;
                float time = 0f, hold = big ? 2f : 1.6f;
                while (!cut() && time < hold)
                {
                    time += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            if (go != null) UnityEngine.Object.Destroy(go);
            if (current == go) current = null;
        }

        /// <summary>Runs an animation step, stopping early if the banner has been cut short.</summary>
        private static IEnumerator Step(IEnumerator inner, Func<bool> cut)
        {
            while (!cut() && inner.MoveNext()) yield return inner.Current;
        }
    }
}
