using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>An ad network that can show a rewarded ad.</summary>
    public interface IRewardedAdBackend
    {
        bool IsReady { get; }
        void Show(Action<bool> finished); // true = watched to the end, give the reward
    }

    /// <summary>
    /// Optional rewarded ads: a player CHOOSES to watch a short ad for bonus coins (no forced ads,
    /// which suits a teen audience). Remove Ads turns this into an instant bonus with no ad.
    /// </summary>
    public static class RewardedAds
    {
        public const int Reward = 500;
        public const int DailyLimit = 5;

        private static IRewardedAdBackend backend;

        public static IRewardedAdBackend Backend
        {
            get
            {
                // The real ad network (Unity LevelPlay) plugs in here once its app key is set up.
                if (backend == null) backend = Application.isEditor || Debug.isDebugBuild ? new TestAdBackend() : (IRewardedAdBackend)new NoAdBackend();
                return backend;
            }
            set => backend = value;
        }

        /// <summary>Does this build show ads at all? False in release builds until an ad network is set up.</summary>
        public static bool Available => !(Backend is NoAdBackend);

        private static string TodayKey => "palace.ads." + DateTime.UtcNow.ToString("yyyyMMdd");
        public static int RemainingToday => Mathf.Max(0, DailyLimit - PlayerPrefs.GetInt(TodayKey, 0));
        public static bool CanClaim => RemainingToday > 0 && (Shop.HasAdFree || Backend.IsReady);

        public static void Claim(Action<bool> finished)
        {
            if (RemainingToday <= 0)
            {
                finished?.Invoke(false);
                return;
            }
            if (Shop.HasAdFree)
            {
                GiveReward();
                finished?.Invoke(true);
                return;
            }
            Backend.Show(watched =>
            {
                if (watched) GiveReward();
                finished?.Invoke(watched);
            });
        }

        private static void GiveReward()
        {
            PlayerPrefs.SetInt(TodayKey, PlayerPrefs.GetInt(TodayKey, 0) + 1);
            Cosmetics.AddCoins(Reward); // also saves
        }
    }

    /// <summary>Release builds before the ad network is set up: no ads offered.</summary>
    public sealed class NoAdBackend : IRewardedAdBackend
    {
        public bool IsReady => false;
        public void Show(Action<bool> finished) => finished?.Invoke(false);
    }

    /// <summary>Editor / development builds: a 3-second stand-in "ad" so the flow can be tested.</summary>
    public sealed class TestAdBackend : IRewardedAdBackend
    {
        public bool IsReady => true;

        public void Show(Action<bool> finished)
        {
            var go = new GameObject("TestAd");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
            var bg = UIBuild.StretchImage("Backdrop", go.transform, null, new Color(0.05f, 0.05f, 0.08f, 0.97f));
            bg.raycastTarget = true; // block taps while the "ad" plays
            var text = UIBuild.Text("Text", go.transform, "", 48f, Color.white, new Vector2(1400f, 300f), Vector2.zero,
                TextAlignmentOptions.Center, UIKit.Font, true);
            text.textWrappingMode = TextWrappingModes.Normal;
            go.AddComponent<TestAdRunner>().Run(text, go, finished);
        }
    }
}
