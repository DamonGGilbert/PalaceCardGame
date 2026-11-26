using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// The story behind Shedhead, shown once after the splash on the first launch (and from Credits >
    /// Our story). Each line types itself out slowly and gently fades up, a page at a time.
    /// Tap to hurry a line along; Skip ends it straight away.
    /// </summary>
    public static class IntroStory
    {
        // Each page's lines appear one after another; the last line of the last page is the signature.
        private static readonly string[][] Pages =
        {
            new[]
            {
                "Back in college, my friends pulled out a battered deck of cards between lessons.",
                "They taught me a game: three cards face down, three face up, and one rule above all. Don't be the last one holding cards.",
                "We played it every break. Loud, silly and fiercely competitive. Someone always ended up picking up the whole pile."
            },
            new[]
            {
                "Those games became some of my favourite memories.",
                "So I made Shedhead, to keep that table going.",
                "To my friends who dealt me in: thank you.",
                "I hope you have as much fun playing as we did back then.",
                "Damon"
            }
        };

        private const float CharsPerSecond = 34f;   // typing speed
        private const float LineHold = 1.1f;        // pause after each line
        private const float PageHold = 2.6f;        // pause at the end of a page

        /// <summary>Plays the story on a dark full-screen layer, then calls done (also after Skip).</summary>
        public static IEnumerator Play(Transform layer, Action done)
        {
            var root = UIBuild.Stretch("Story", layer);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            bool skip = false, hurry = false;
            var tap = UIBuild.StretchImage("Tap", root, null, new Color(0f, 0f, 0f, 0.001f));
            tap.raycastTarget = true;
            var tapButton = tap.gameObject.AddComponent<Button>();
            tapButton.transition = Selectable.Transition.None;
            tapButton.onClick.AddListener(() => hurry = true);
            var skipButton = UIKit.Button(root, "Skip", new Vector2(180f, 60f), new Vector2(800f, 450f), () => skip = true, false, 24f);

            yield return Tween.Fade(group, 0f, 1f, 0.6f);

            for (int page = 0; page < Pages.Length && !skip; page++)
            {
                string[] lines = Pages[page];
                var pageRoot = UIBuild.Stretch("Page", root);
                pageRoot.SetSiblingIndex(1); // above the tap catcher, below Skip
                var pageGroup = pageRoot.gameObject.AddComponent<CanvasGroup>();
                pageGroup.blocksRaycasts = false;

                float gap = page == 0 ? 165f : 120f;
                float top = (lines.Length - 1) * gap * 0.5f;
                for (int i = 0; i < lines.Length && !skip; i++)
                {
                    bool signature = page == Pages.Length - 1 && i == lines.Length - 1;
                    string text = signature ? "- " + lines[i] : Loc.T(lines[i]);
                    var label = UIBuild.Text("Line", pageRoot, text, signature ? 44f : 40f, signature ? UIKit.Gold : UIKit.Text,
                        new Vector2(1350f, gap), new Vector2(0f, top - i * gap), TextAlignmentOptions.Center, UIKit.Font, signature, false);
                    label.textWrappingMode = TextWrappingModes.Normal;
                    if (signature) label.fontStyle |= FontStyles.Italic;
                    yield return TypeLine(label, () => skip, () => { bool h = hurry; hurry = false; return h; });
                    yield return Wait(signature ? 0.4f : LineHold, () => skip, () => { bool h = hurry; hurry = false; return h; });
                }

                yield return Wait(PageHold, () => skip, () => { bool h = hurry; hurry = false; return h; });
                if (!skip) yield return Tween.Fade(pageGroup, 1f, 0f, 0.7f);
                UnityEngine.Object.Destroy(pageRoot.gameObject);
            }

            skipButton.interactable = false;
            yield return Tween.Fade(group, group.alpha, 0f, 0.5f);
            UnityEngine.Object.Destroy(root.gameObject);
            done?.Invoke();
        }

        /// <summary>Types a line out character by character while it fades and drifts up into place.</summary>
        private static IEnumerator TypeLine(TextMeshProUGUI label, Func<bool> skip, Func<bool> hurry)
        {
            label.ForceMeshUpdate();
            int total = label.textInfo.characterCount;
            var rt = label.rectTransform;
            Vector2 end = rt.anchoredPosition;
            label.maxVisibleCharacters = 0;
            label.alpha = 0f;

            float t = 0f, duration = Mathf.Max(0.6f, total / CharsPerSecond);
            if (!UIMotion.Enabled) duration = 0f; // Settings > Animations Off: show it at once
            while (t < duration)
            {
                if (skip() || hurry()) break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                label.maxVisibleCharacters = Mathf.CeilToInt(total * k);
                label.alpha = Mathf.Clamp01(t / 0.5f);
                rt.anchoredPosition = end + new Vector2(0f, -18f * (1f - Mathf.Clamp01(t / 0.8f)));
                yield return null;
            }
            label.maxVisibleCharacters = total;
            label.alpha = 1f;
            rt.anchoredPosition = end;
        }

        private static IEnumerator Wait(float seconds, Func<bool> skip, Func<bool> hurry)
        {
            float t = 0f;
            while (t < seconds && !skip() && !hurry())
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
