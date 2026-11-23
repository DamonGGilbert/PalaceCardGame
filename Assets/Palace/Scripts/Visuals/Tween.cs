using System;
using System.Collections;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Minimal tweening for UI animations, using coroutines (no third-party library).
    /// Durations are multiplied by the player's animation setting; "Off" makes them instant.
    /// </summary>
    public static class Tween
    {
        /// <summary>Temporary fast-forward (2x / 4x) while waiting for computer players to finish.</summary>
        public static float SpeedMultiplier = 1f;

        public static float Scaled(float seconds) => seconds * GameSettings.AnimationScale / Mathf.Max(1f, SpeedMultiplier);

        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float EaseInOut(float t) => t * t * (3f - 2f * t);

        /// <summary>Runs step(t) for t going 0 -> 1 over the (already scaled) duration.</summary>
        public static IEnumerator Run(float duration, Action<float> step, Func<float, float> ease = null)
        {
            ease = ease ?? EaseOutCubic;
            if (duration <= 0f)
            {
                step(1f);
                yield break;
            }

            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                step(ease(Mathf.Clamp01(time / duration)));
                yield return null;
            }
        }

        public static IEnumerator Move(RectTransform rt, Vector2 from, Vector2 to, float duration,
            float fromScale = 1f, float toScale = 1f, float fromAngle = 0f, float toAngle = 0f)
        {
            return Run(duration, t =>
            {
                if (rt == null) return;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, toScale, t);
                rt.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(fromAngle, toAngle, t));
            });
        }

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            return Run(duration, t => { if (group != null) group.alpha = Mathf.Lerp(from, to, t); }, EaseInOut);
        }

        public static IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f) yield break;
            float time = 0f;
            while (time < seconds)
            {
                time += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
