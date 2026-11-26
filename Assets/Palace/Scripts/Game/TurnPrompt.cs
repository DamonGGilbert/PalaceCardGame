using System;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Makes it obvious when it's your go. For as long as the game is waiting for you, the pile glows
    /// softly (Glow is called every frame with true or false). If you haven't started your go after a
    /// while, it asks the controller to nudge you again (chime, buzz, "YOUR TURN" and a little bounce
    /// of the cards you can play).
    /// </summary>
    public sealed class TurnPrompt : MonoBehaviour
    {
        private const float FirstNudge = 8f;    // seconds of waiting before the first reminder
        private const float NudgeEvery = 15f;   // then this often

        /// <summary>True while the game is waiting for you to act.</summary>
        public Func<bool> IsWaiting;
        /// <summary>True while a reminder makes sense (you haven't picked a card, no menu is open).</summary>
        public Func<bool> CanNudge;
        public Action Nudge;
        public Action<bool> Glow;

        private float waited;
        private float nextNudge = FirstNudge;

        public static TurnPrompt Create(Transform parent)
        {
            var go = new GameObject("TurnPrompt");
            go.transform.SetParent(parent, false);
            return go.AddComponent<TurnPrompt>();
        }

        private void Update()
        {
            bool waiting = IsWaiting != null && IsWaiting();
            Glow?.Invoke(waiting);

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
