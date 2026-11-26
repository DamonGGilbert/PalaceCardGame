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
    /// A dark band sweeps across the screen with accent stripes racing in from both sides, the player's name
    /// above, and the title slamming in with a little shake. It holds for a moment, then whooshes away.
    /// Doesn't block the game, and doesn't catch taps. One at a time: LAST CARD banners queue, and a WINNER
    /// banner cuts any banner still showing short (and drops queued LAST CARD ones, which are old news).
    /// </summary>
    public static class LastCardBanner
    {
        private static GameObject current; // the banner on screen (null once it's gone, even if a coroutine was killed)
        private static int generation;     // goes up when a winner banner takes over

        /// <summary>The LAST CARD banner. Gold for you, red for everyone else.</summary>
        public static IEnumerator Play(RectTransform parent, string playerName, bool isYou)
        {
            int token = generation;
            while (current != null) yield return null; // one at a time
            if (token != generation) yield break;     // a winner banner came along: this one is old news
            yield return Run(parent, playerName, isYou, false, token);
        }

        /// <summary>The WINNER banner, for the first player out. Gold, bigger, and it holds a little longer.</summary>
        public static IEnumerator PlayWin(RectTransform parent, string playerName, bool isYou)
        {
            int token = ++generation; // cuts short whatever is showing
            while (current != null) yield return null;
            if (token != generation) yield break;
            yield return Run(parent, playerName, isYou, true, token);
        }

        private static IEnumerator Run(RectTransform parent, string playerName, bool isYou, bool win, int token)
        {
            if (parent == null) yield break;
            Color accent = win || isYou ? new Color(1f, 0.78f, 0.25f) : new Color(1f, 0.22f, 0.18f);
            float bandH = win ? 220f : 190f;
            const float y = 70f;

            var root = UIBuild.Stretch(win ? "WinnerBanner" : "LastCardBanner", parent);
            root.SetAsLastSibling();
            GameObject go = root.gameObject;
            current = go;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var holder = UIBuild.Rect("Holder", root, new Vector2(2600f, bandH + 40f), new Vector2(0f, y));

            // The band, two accent stripes and a light glint.
            var band = UIBuild.Image("Band", holder, null, new Color(0.04f, 0.04f, 0.06f, 0.9f), new Vector2(0f, bandH), Vector2.zero);
            float stripeH = win ? 9f : 7f;
            var stripeTop = UIBuild.Image("StripeTop", holder, null, accent, new Vector2(2600f, stripeH), new Vector2(-2600f, bandH * 0.5f));
            var stripeBottom = UIBuild.Image("StripeBottom", holder, null, accent, new Vector2(2600f, stripeH), new Vector2(2600f, -bandH * 0.5f));
            band.gameObject.AddComponent<RectMask2D>(); // the glint only shines across the band
            var glint = UIBuild.Image("Glint", band.transform, null, new Color(1f, 1f, 1f, win ? 0.22f : 0.16f), new Vector2(90f, bandH * 1.6f), new Vector2(-1500f, 0f));
            glint.rectTransform.localEulerAngles = new Vector3(0f, 0f, -22f);
            foreach (var img in new[] { band, stripeTop, stripeBottom, glint }) img.raycastTarget = false;

            string who = isYou ? Loc.T("YOU") : (playerName ?? "").ToUpperInvariant();
            var nameText = UIBuild.Text("Name", holder, who, win ? 40f : 36f, new Color(1f, 1f, 1f, 0.92f), new Vector2(1400f, 54f),
                new Vector2(0f, win ? 60f : 50f), TextAlignmentOptions.Center, UIKit.Font, true, false);
            nameText.alpha = 0f;
            string titleText = win ? Loc.T(isYou ? "WIN!" : "WINS!") : Loc.T("LAST CARD");
            var title = UIBuild.Text("Title", holder, titleText, win ? 112f : 96f, accent, new Vector2(1600f, 140f),
                new Vector2(0f, win ? -22f : -18f), TextAlignmentOptions.Center, UIKit.Font, true, false);
            title.outlineWidth = 0.2f;
            title.outlineColor = new Color32(0, 0, 0, 220);
            title.alpha = 0f;

            Func<bool> cut = () => token != generation || go == null;

            if (UIMotion.Enabled)
            {
                if (!win) AudioManager.Play(Sfx.Reveal, 0.9f);
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
                // 3. The title slams in from big to normal, with a short shake on landing.
                var titleRt = title.rectTransform;
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
                band.rectTransform.sizeDelta = new Vector2(2600f, bandH);
                stripeTop.rectTransform.anchoredPosition = new Vector2(0f, bandH * 0.5f);
                stripeBottom.rectTransform.anchoredPosition = new Vector2(0f, -bandH * 0.5f);
                nameText.alpha = 1f;
                title.alpha = 1f;
                float time = 0f, hold = win ? 2f : 1.6f;
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
