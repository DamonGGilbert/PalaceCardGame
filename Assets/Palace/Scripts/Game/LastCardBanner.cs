using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// The "LAST CARD" announcement: when a player is down to their very last card (hand and table),
    /// a dark band sweeps across the screen with accent stripes racing in from both sides, their name
    /// above, and LAST CARD slamming in with a little shake. It holds for a moment, then whooshes away.
    /// Doesn't block the game, and doesn't catch taps.
    /// </summary>
    public static class LastCardBanner
    {
        private static bool showing;

        public static IEnumerator Play(RectTransform parent, string playerName, bool isYou)
        {
            while (showing) yield return null; // one at a time
            if (parent == null) yield break;
            showing = true;
            try
            {
                yield return Run(parent, playerName, isYou);
            }
            finally
            {
                showing = false;
            }
        }

        private static IEnumerator Run(RectTransform parent, string playerName, bool isYou)
        {
            Color accent = isYou ? new Color(1f, 0.78f, 0.25f) : new Color(1f, 0.22f, 0.18f);
            const float bandH = 190f, y = 70f;

            var root = UIBuild.Stretch("LastCardBanner", parent);
            root.SetAsLastSibling();
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var holder = UIBuild.Rect("Holder", root, new Vector2(2600f, bandH + 40f), new Vector2(0f, y));

            // The band, two accent stripes and a light glint.
            var band = UIBuild.Image("Band", holder, null, new Color(0.04f, 0.04f, 0.06f, 0.9f), new Vector2(0f, bandH), Vector2.zero);
            var stripeTop = UIBuild.Image("StripeTop", holder, null, accent, new Vector2(2600f, 7f), new Vector2(-2600f, bandH * 0.5f));
            var stripeBottom = UIBuild.Image("StripeBottom", holder, null, accent, new Vector2(2600f, 7f), new Vector2(2600f, -bandH * 0.5f));
            band.gameObject.AddComponent<RectMask2D>(); // the glint only shines across the band
            var glint = UIBuild.Image("Glint", band.transform, null, new Color(1f, 1f, 1f, 0.16f), new Vector2(90f, bandH * 1.6f), new Vector2(-1500f, 0f));
            glint.rectTransform.localEulerAngles = new Vector3(0f, 0f, -22f);
            foreach (var img in new[] { band, stripeTop, stripeBottom, glint }) img.raycastTarget = false;

            string who = isYou ? Loc.T("YOU") : (playerName ?? "").ToUpperInvariant();
            var nameText = UIBuild.Text("Name", holder, who, 36f, new Color(1f, 1f, 1f, 0.92f), new Vector2(1400f, 50f),
                new Vector2(0f, 50f), TextAlignmentOptions.Center, UIKit.Font, true, false);
            nameText.alpha = 0f;
            var title = UIBuild.Text("Title", holder, Loc.T("LAST CARD"), 96f, accent, new Vector2(1600f, 120f),
                new Vector2(0f, -18f), TextAlignmentOptions.Center, UIKit.Font, true, false);
            title.outlineWidth = 0.2f;
            title.outlineColor = new Color32(0, 0, 0, 220);
            title.alpha = 0f;

            bool animate = UIMotion.Enabled;
            AudioManager.Play(Sfx.Reveal, 0.9f);

            if (animate)
            {
                // 1. The band opens from the middle while the stripes race in from opposite sides.
                yield return Tween.Run(0.28f, t =>
                {
                    band.rectTransform.sizeDelta = new Vector2(2600f * t, bandH * Mathf.Lerp(0.4f, 1f, t));
                    stripeTop.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-2600f, 0f, t), bandH * 0.5f);
                    stripeBottom.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(2600f, 0f, t), -bandH * 0.5f);
                });
                // 2. The name slides in with wide letter spacing that tightens up.
                yield return Tween.Run(0.22f, t =>
                {
                    nameText.alpha = t;
                    nameText.characterSpacing = Mathf.Lerp(40f, 12f, t);
                });
                // 3. LAST CARD slams in from big to normal, with a short shake on landing.
                AudioManager.Play(Sfx.Burn, 0.6f);
                var titleRt = title.rectTransform;
                yield return Tween.Run(0.24f, t =>
                {
                    title.alpha = Mathf.Clamp01(t * 2f);
                    titleRt.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, t);
                }, x => x * x);
                float shake = 0f;
                var holderRt = holder;
                while (shake < 0.18f)
                {
                    shake += Time.unscaledDeltaTime;
                    float k = 1f - shake / 0.18f;
                    holderRt.anchoredPosition = new Vector2(Random.Range(-9f, 9f) * k, y + Random.Range(-6f, 6f) * k);
                    yield return null;
                }
                holderRt.anchoredPosition = new Vector2(0f, y);
                // 4. A light glint sweeps across while it holds.
                yield return Tween.Run(0.55f, t => glint.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-1500f, 1500f, t), 0f), x => x);
                yield return Tween.Wait(0.9f);
                // 5. Whoosh away to the right.
                yield return Tween.Run(0.3f, t =>
                {
                    holderRt.anchoredPosition = new Vector2(Mathf.Lerp(0f, 900f, t * t), y);
                    group.alpha = 1f - t;
                }, x => x);
            }
            else
            {
                // Animations off: just show it for a moment.
                band.rectTransform.sizeDelta = new Vector2(2600f, bandH);
                stripeTop.rectTransform.anchoredPosition = new Vector2(0f, bandH * 0.5f);
                stripeBottom.rectTransform.anchoredPosition = new Vector2(0f, -bandH * 0.5f);
                nameText.alpha = 1f;
                title.alpha = 1f;
                yield return new WaitForSecondsRealtime(1.6f);
            }
            if (root != null) Object.Destroy(root.gameObject);
        }
    }
}
