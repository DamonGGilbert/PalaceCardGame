using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Small, friendly menu animations: screens pop open, buttons pop in one after another and
    /// squish when pressed, and the title and decorative cards bob gently.
    /// Everything respects Settings > Animations: with Off, nothing moves.
    /// </summary>
    public static class UIMotion
    {
        public static bool Enabled => GameSettings.AnimationScale > 0f;

        /// <summary>A screen opening: a quick fade and a soft "pop" from slightly smaller.</summary>
        public static IEnumerator Appear(Transform screen)
        {
            if (!Enabled || screen == null) yield break;
            // TryGetComponent, not "GetComponent ?? Add": in the editor a missing component comes back as a
            // fake-null object that ?? doesn't catch, which threw MissingComponentException.
            if (!screen.TryGetComponent(out CanvasGroup group)) group = screen.gameObject.AddComponent<CanvasGroup>();
            float t = 0f;
            const float duration = 0.24f;
            while (t < duration && screen != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                group.alpha = k;
                screen.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, BackOut(k));
                yield return null;
            }
            if (screen == null) yield break;
            group.alpha = 1f;
            screen.localScale = Vector3.one;
        }

        /// <summary>Buttons pop in one after another (a little bounce each).</summary>
        public static IEnumerator PopIn(IList<Transform> items, float gap = 0.05f)
        {
            if (!Enabled) yield break;
            foreach (var item in items) if (item != null) item.localScale = Vector3.zero;
            for (int i = 0; i < items.Count; i++)
            {
                Transform item = items[i];
                if (item != null) CoroutineHost.Run(Pop(item)); // outlives the coroutine that started it
                yield return new WaitForSecondsRealtime(gap);
            }
        }

        private static IEnumerator Pop(Transform item)
        {
            float t = 0f;
            const float duration = 0.32f;
            while (t < duration && item != null)
            {
                t += Time.unscaledDeltaTime;
                item.localScale = Vector3.one * BackOut(Mathf.Clamp01(t / duration));
                yield return null;
            }
            if (item != null) item.localScale = Vector3.one;
        }

        /// <summary>Ease-out with a small overshoot, for a bouncy feel.</summary>
        public static float BackOut(float k)
        {
            const float s = 1.9f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }
    }
}
