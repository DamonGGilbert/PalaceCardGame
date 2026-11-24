using System;
using System.Collections;
using UnityEngine;
#if PALACE_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif
#if PALACE_NOTIFICATIONS && UNITY_IOS && !UNITY_EDITOR
using Unity.Notifications.iOS;
#endif

namespace Palace
{
    /// <summary>
    /// Optional daily streak reminder: one friendly phone notification the next day at 6pm,
    /// e.g. "Keep your 4-day streak going! Play today for 50 coins." It is off until the player
    /// turns it on (Settings, or the offer on the daily reward pop-up), and is moved to the next
    /// day every time the game is opened, so it never nags someone who already played.
    ///
    /// Needs the free Mobile Notifications package (Window > Package Manager > Unity Registry);
    /// installing it switches this on automatically (PALACE_NOTIFICATIONS). Without it, or in
    /// the editor and on PC, everything here quietly does nothing.
    /// </summary>
    public static class StreakReminder
    {
        private const string ChannelId = "palace_streak";
        private const int NotificationId = 4071;
        private const int ReminderHour = 18;

#if PALACE_NOTIFICATIONS && (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        public static bool Supported => true;
#else
        public static bool Supported => false;
#endif

        /// <summary>Cancel any pending reminder and, if enabled, schedule tomorrow's.</summary>
        public static void Refresh(bool enabled)
        {
            if (!Supported) return;
            try
            {
                Cancel();
                if (enabled) Schedule();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Palace] Streak reminder: " + e.Message);
            }
        }

        /// <summary>Ask the phone for permission to show notifications, then report the answer.</summary>
        public static void RequestPermission(Action<bool> done)
        {
            if (!Supported)
            {
                done?.Invoke(false);
                return;
            }
            Runner.Get().StartCoroutine(Ask(done));
        }

        private static string Title => "Palace";

        private static string Body()
        {
            int next = Progress.DayStreak + 1;
            return next <= 1
                ? Loc.T("Your daily reward is waiting. Fancy a quick game?")
                : Loc.F("Keep your streak going! Day {0} pays {1} coins.", next, Progress.StreakCoins(next));
        }

        private static DateTime FireTime => DateTime.Now.Date.AddDays(1).AddHours(ReminderHour);

#if PALACE_NOTIFICATIONS && UNITY_ANDROID && !UNITY_EDITOR
        private static bool channelMade;

        private static void Schedule()
        {
            if (!channelMade)
            {
                AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
                {
                    Id = ChannelId, Name = "Daily streak", Importance = Importance.Default,
                    Description = "A reminder the day after you play, so you keep your streak."
                });
                channelMade = true;
            }
            var n = new AndroidNotification { Title = Title, Text = Body(), FireTime = FireTime };
            AndroidNotificationCenter.SendNotificationWithExplicitID(n, ChannelId, NotificationId);
        }

        private static void Cancel() => AndroidNotificationCenter.CancelNotification(NotificationId);

        private static IEnumerator Ask(Action<bool> done)
        {
            var request = new PermissionRequest(); // Android 13+ asks; older phones allow straight away
            while (request.Status == PermissionStatus.RequestPending) yield return null;
            done?.Invoke(request.Status == PermissionStatus.Allowed);
        }
#elif PALACE_NOTIFICATIONS && UNITY_IOS && !UNITY_EDITOR
        private static void Schedule()
        {
            TimeSpan wait = FireTime - DateTime.Now;
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = ChannelId, Title = Title, Body = Body(), ShowInForeground = false,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = wait, Repeats = false }
            });
        }

        private static void Cancel() => iOSNotificationCenter.RemoveScheduledNotification(ChannelId);

        private static IEnumerator Ask(Action<bool> done)
        {
            using (var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Sound, false))
            {
                while (!req.IsFinished) yield return null;
                done?.Invoke(req.Granted);
            }
        }
#else
        private static void Schedule() { }
        private static void Cancel() { }

        private static IEnumerator Ask(Action<bool> done)
        {
            done?.Invoke(false);
            yield break;
        }
#endif

        /// <summary>Runs the permission request (coroutines need a GameObject).</summary>
        private sealed class Runner : MonoBehaviour
        {
            private static Runner instance;

            public static Runner Get()
            {
                if (instance != null) return instance;
                var go = new GameObject("StreakReminder");
                UnityEngine.Object.DontDestroyOnLoad(go);
                instance = go.AddComponent<Runner>();
                return instance;
            }
        }
    }
}
