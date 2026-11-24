using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
#if PALACE_CLOUDSAVE
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.CloudSave;
using Unity.Services.Core;
#endif

namespace Palace
{
    /// <summary>
    /// Backs up coins, unlocked cosmetics, equipped items and purchases to Unity Cloud Save,
    /// and restores them on another device after signing in with a (free) Unity account.
    ///
    /// Merging never loses anything paid for or unlocked: ownership is combined from both copies.
    /// Coins and equipped items come from whichever copy was changed most recently.
    /// Active only when the Cloud Save package is installed (PALACE_CLOUDSAVE is set automatically)
    /// and the project is linked to Unity Cloud.
    /// </summary>
    public static class CloudProgress
    {
        private const string CloudKey = "palace_progress";
        private const string SavedAtKey = "palace.cloud.savedAt";

        public static string Status { get; private set; } = "";
        public static bool Busy { get; private set; }
        public static event Action Changed;

        [Serializable]
        private class Snapshot
        {
            public int version = 1;
            public long savedAt;
            public int coins;
            public List<string> owned = new List<string>();
            public List<string> purchases = new List<string>();
            public List<string> equipped = new List<string>(); // "Category=itemId"
            public string progress;                            // stats, XP and achievements (Progress.ExportJson)
        }

#if PALACE_CLOUDSAVE
        public static bool Supported => true;
        public static bool AccountSignedIn => SafeAccountSignedIn();
#else
        public static bool Supported => false;
        public static bool AccountSignedIn => false;
#endif

        // ------------------------------------------------------------------ local snapshot

        /// <summary>Call whenever progress changes locally (coins, unlocks, equips, purchases).</summary>
        public static void MarkLocalChange()
        {
            PlayerPrefs.SetString(SavedAtKey, DateTime.UtcNow.Ticks.ToString());
            ScheduleUpload();
        }

        private static long LocalSavedAt => long.TryParse(PlayerPrefs.GetString(SavedAtKey, "0"), out long t) ? t : 0;

        private static Snapshot Capture()
        {
            var snap = new Snapshot { savedAt = LocalSavedAt, coins = Cosmetics.Coins, progress = Progress.ExportJson() };
            var lib = CosmeticLibrary.Instance;
            if (lib != null)
            {
                foreach (CosmeticCategory c in Enum.GetValues(typeof(CosmeticCategory)))
                {
                    foreach (var item in lib.Items(c))
                        if (PlayerPrefs.GetInt("palace.own." + item.id, 0) == 1) snap.owned.Add(item.id);
                    string equip = Cosmetics.EquippedId(c);
                    if (!string.IsNullOrEmpty(equip)) snap.equipped.Add(c + "=" + equip);
                }
            }
            foreach (var p in ShopCatalog.Products)
                if (Shop.IsOwned(p.Id)) snap.purchases.Add(p.Id);
            return snap;
        }

        private static void Merge(Snapshot remote)
        {
            if (remote == null) return;
            foreach (string id in remote.owned) PlayerPrefs.SetInt("palace.own." + id, 1);           // never lose unlocks
            foreach (string id in remote.purchases) PlayerPrefs.SetInt("palace.iap.owned." + id, 1); // never lose purchases
            Progress.ImportJson(remote.progress);                                                     // keeps the copy with more XP

            if (remote.savedAt > LocalSavedAt)
            {
                Cosmetics.SetCoinsFromCloud(remote.coins);
                foreach (string pair in remote.equipped)
                {
                    int eq = pair.IndexOf('=');
                    if (eq > 0) PlayerPrefs.SetString("palace.equip." + pair.Substring(0, eq), pair.Substring(eq + 1));
                }
                PlayerPrefs.SetString(SavedAtKey, remote.savedAt.ToString());
            }
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ syncing

        private static float uploadAt = -1f;

        private static void ScheduleUpload()
        {
            if (!Supported) return;
            uploadAt = Time.unscaledTime + 4f; // batch quick changes together
            Runner.Ensure();
        }

        /// <summary>Download, merge and upload. Safe to call any time.</summary>
        public static async void SyncNow()
        {
            if (!Supported || Busy) return;
            Busy = true;
            SetStatus("Syncing...");
            try
            {
                await SyncAsync();
                SetStatus(AccountSignedIn ? "Progress saved to your account." : "Progress backed up on this device's cloud save.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Palace] Cloud save: " + e.Message);
                SetStatus(Friendly(e));
            }
            finally
            {
                Busy = false;
                Changed?.Invoke();
            }
        }

#if PALACE_CLOUDSAVE
        private static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static async Task SyncAsync()
        {
            await EnsureSignedInAsync();
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CloudKey });
            if (data.TryGetValue(CloudKey, out var item))
            {
                Snapshot remote = JsonUtility.FromJson<Snapshot>(item.Value.GetAs<string>());
                Merge(remote);
                Cosmetics.NotifyChanged();
                Shop.NotifyChanged();
            }
            string json = JsonUtility.ToJson(Capture());
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { CloudKey, json } });
        }

        private static bool SafeAccountSignedIn()
        {
            try { return UnityServices.State == ServicesInitializationState.Initialized && PlayerAccountService.Instance.IsSignedIn; }
            catch { return false; }
        }

        /// <summary>
        /// Sign in with a free Unity account (opens a browser page) so progress follows you to any device.
        /// The current progress is linked to that account; on a new device the account's progress is merged in.
        /// </summary>
        public static async void SignInWithAccount()
        {
            if (Busy) return;
            Busy = true;
            SetStatus("Opening sign-in...");
            try
            {
                await EnsureSignedInAsync();
                PlayerAccountService.Instance.SignedIn -= OnAccountSignedIn;
                PlayerAccountService.Instance.SignedIn += OnAccountSignedIn;
                await PlayerAccountService.Instance.StartSignInAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Palace] Account sign-in: " + e.Message);
                SetStatus(Friendly(e));
                Busy = false;
                Changed?.Invoke();
            }
        }

        private static async void OnAccountSignedIn()
        {
            string token = PlayerAccountService.Instance.AccessToken;
            try
            {
                try
                {
                    await AuthenticationService.Instance.LinkWithUnityAsync(token); // keep this device's progress
                }
                catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                {
                    // This account already has progress (e.g. from another phone): switch to it, then merge.
                    AuthenticationService.Instance.SignOut();
                    await AuthenticationService.Instance.SignInWithUnityAsync(token);
                }
                Busy = false;
                SyncNow();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Palace] Account link: " + e.Message);
                SetStatus(Friendly(e));
                Busy = false;
                Changed?.Invoke();
            }
        }
#else
        private static Task SyncAsync() => Task.CompletedTask;
        public static void SignInWithAccount() => SetStatus("Install the Cloud Save package to enable this (see Docs/Cloud-Save-Setup.md).");
#endif

        private static string Friendly(Exception e)
        {
            string m = e.Message ?? "";
            if (m.Contains("project") || m.Contains("Project") || m.Contains("cloud") || m.Contains("Cloud"))
                return "Cloud save isn't set up yet (the game needs linking to Unity Cloud).";
            if (m.Contains("network") || m.Contains("Network") || m.Contains("connect"))
                return "No internet connection - progress is still saved on this device.";
            return "Couldn't sync right now - progress is still saved on this device.";
        }

        private static void SetStatus(string s)
        {
            Status = s;
            Changed?.Invoke();
        }

        /// <summary>Tiny helper object that performs the delayed upload.</summary>
        private class Runner : MonoBehaviour
        {
            private static Runner instance;

            public static void Ensure()
            {
                if (instance != null) return;
                var go = new GameObject("CloudProgress");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Runner>();
            }

            private void Update()
            {
                if (uploadAt > 0f && Time.unscaledTime >= uploadAt && !Busy)
                {
                    uploadAt = -1f;
                    SyncNow();
                }
            }
        }
    }
}
