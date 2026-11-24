using UnityEngine;

namespace Palace
{
    /// <summary>
    /// App-wide runtime settings applied once at start-up.
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        private static bool cloudSynced;
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool keepScreenAwake = true;

        private void Awake()
        {
            // Mobile defaults to 30 FPS; card movement looks much smoother at 60.
            Application.targetFrameRate = targetFrameRate;
            // A card game has long thinking pauses; don't let the phone dim mid-game.
            if (keepScreenAwake) Screen.sleepTimeout = SleepTimeout.NeverSleep;
            // Connect to the store early so unfinished purchases are delivered and prices are ready.
            Shop.Initialize();
            // Once per launch: merge in progress saved in the cloud (no-op without the Cloud Save package).
            if (!cloudSynced)
            {
                cloudSynced = true;
                CloudProgress.SyncNow();
            }
        }
    }
}
