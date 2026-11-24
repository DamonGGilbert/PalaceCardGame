using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Short phone buzzes for the big moments: your turn, burns, skips and pick-ups.
    /// Does nothing on PC, in the editor, or when Settings > Vibration is off.
    /// </summary>
    public static class Haptics
    {
        private static float lastBuzz = -10f;

        public static void Buzz()
        {
            if (!GameSettings.Vibration || AutoPlayHarness.Active) return;
            if (Time.unscaledTime - lastBuzz < 0.35f) return; // a burn straight after a play shouldn't double-buzz
            lastBuzz = Time.unscaledTime;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
