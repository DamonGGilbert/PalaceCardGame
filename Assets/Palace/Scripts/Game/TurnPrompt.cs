using System;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// Makes it obvious when it's your go. For as long as the game is waiting for you, a soft gold glow
    /// breathes round the edges of the screen (always in view, however the table is cropped). If you
    /// haven't started your go after a while, it asks the controller to nudge you again (chime, buzz,
    /// "YOUR TURN" and a little bounce of the cards you can play).
    /// </summary>
    public sealed class TurnPrompt : MonoBehaviour
    {
        private const float FirstNudge = 8f;    // seconds of waiting before the first reminder
        private const float NudgeEvery = 15f;   // then this often
        private const float FadeTime = 0.3f;
        private static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        /// <summary>True while the game is waiting for you to act.</summary>
        public Func<bool> IsWaiting;
        /// <summary>True while a reminder makes sense (you haven't picked a card, no menu is open).</summary>
        public Func<bool> CanNudge;
        public Action Nudge;

        private Image glow;
        private float fade;       // 0 hidden .. 1 fully shown
        private float waited;
        private float nextNudge = FirstNudge;

        public static TurnPrompt Create(RectTransform hud)
        {
            Image img = UIBuild.StretchImage("TurnPrompt", hud, FxSprites.EdgeGlow, new Color(Gold.r, Gold.g, Gold.b, 0f));
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.pixelsPerUnitMultiplier = 0.32f; // about 100 units of glow on each edge
            img.enabled = false;
            img.transform.SetAsFirstSibling();   // under the buttons, menus and banners
            var prompt = img.gameObject.AddComponent<TurnPrompt>();
            prompt.glow = img;
            return prompt;
        }

        private void Update()
        {
            bool waiting = IsWaiting != null && IsWaiting();
            fade = Mathf.MoveTowards(fade, waiting ? 1f : 0f, Time.unscaledDeltaTime / FadeTime);
            float breathe = GameSettings.AnimationScale > 0f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.4f) : 0.6f;
            glow.color = new Color(Gold.r, Gold.g, Gold.b, fade * Mathf.Lerp(0.4f, 0.9f, breathe));
            glow.enabled = fade > 0.001f;

            if (!waiting || (CanNudge != null && !CanNudge()))
            {
                waited = 0f;
                nextNudge = FirstNudge;
                return;
            }
            waited += Time.unscaledDeltaTime;
            if (waited < nextNudge) return;
            nextNudge += NudgeEvery;
            Nudge?.Invoke();
        }
    }
}
