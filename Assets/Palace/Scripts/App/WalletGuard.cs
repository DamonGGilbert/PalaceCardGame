using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Protects coins and unlocks from the two common cheats on a hacked phone or PC.
    ///
    /// 1. Memory scanners (GameGuardian, Cheat Engine) look for your coin number in memory and change it.
    ///    While the game runs, coins are kept scrambled with a random key and a check value (HiddenInt),
    ///    so searching for "250" finds nothing, and a changed value is spotted and put back.
    /// 2. Save editors change the saved coins or unlocks. Every save gets a fingerprint (HMAC-SHA256 with a
    ///    key inside the game) plus a scrambled, fingerprinted backup. If the saved values no longer match
    ///    their fingerprint when the game starts, the last trusted values come back from the backup.
    ///
    /// Honest players never notice: every change the game itself makes is fingerprinted straight away.
    /// Existing saves from before this was added are trusted once and fingerprinted. A determined hacker
    /// can still dig the key out of the game, so this stops casual cheating, not everything (see
    /// Docs/Monetization-Setup.md). Every write to coins, "palace.own.*" or "palace.iap.owned.*" must be
    /// followed by Resign() (Cosmetics, Shop and CloudProgress do this).
    /// </summary>
    public static class WalletGuard
    {
        private const string CoinsKey = "palace.coins";
        private const string SigKey = "palace.sig";
        private const string SigCopyKey = "palace.ui.k2";   // second copy, under a dull name
        private const string BackupKey = "palace.ui.k3";    // scrambled backup of the trusted values

        private static bool checkedThisRun;
        private static HiddenInt coins;

        /// <summary>Set when the last check found edited saves (and put them back). For tests and the log.</summary>
        public static bool LastCheckFoundTampering { get; private set; }

        public static int Coins
        {
            get
            {
                EnsureChecked();
                if (coins.TryGet(out int value)) return value;
                // The scrambled copy in memory was changed by a cheat tool: go back to the saved value.
                Debug.LogWarning("[Palace] Coins were changed in memory; putting them back.");
                value = Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, 0));
                coins = new HiddenInt(value);
                return value;
            }
        }

        /// <summary>Saves a new coin total (and re-fingerprints everything).</summary>
        public static void SetCoins(int value)
        {
            EnsureChecked();
            value = Mathf.Max(0, value);
            PlayerPrefs.SetInt(CoinsKey, value);
            coins = new HiddenInt(value);
            Resign();
        }

        /// <summary>Checks the saved coins and unlocks against their fingerprint, once per run.</summary>
        public static void EnsureChecked()
        {
            if (checkedThisRun) return;
            checkedThisRun = true;
            LastCheckFoundTampering = false;
            if (CosmeticLibrary.Instance == null)
            {
                // Without the item list the check could mistake honest unlocks for edits, so it stands aside.
                coins = new HiddenInt(Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, 0)));
                return;
            }

            string state = Describe();
            string sig = PlayerPrefs.GetString(SigKey, ""), copy = PlayerPrefs.GetString(SigCopyKey, "");
            bool hasBackup = PlayerPrefs.HasKey(BackupKey);

            if (sig.Length == 0 && copy.Length == 0 && !hasBackup)
            {
                Resign(); // a new install, or a save from before fingerprints: trust it once
            }
            else if (!Equal(sig, Sign(state)) || !Equal(copy, Sign(state)))
            {
                LastCheckFoundTampering = true;
                Debug.LogWarning("[Palace] Saved coins or unlocks were changed outside the game; restoring the last trusted values.");
                RestoreFromBackup();
            }
            coins = new HiddenInt(Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, 0)));
        }

        /// <summary>Fingerprints the current coins and unlocks, and refreshes the backup. Call after every change.</summary>
        public static void Resign()
        {
            if (CosmeticLibrary.Instance == null) { PlayerPrefs.Save(); return; } // see EnsureChecked
            string state = Describe();
            string sig = Sign(state);
            PlayerPrefs.SetString(SigKey, sig);
            PlayerPrefs.SetString(SigCopyKey, sig);
            PlayerPrefs.SetString(BackupKey, Scramble(state + "\n" + sig));
            PlayerPrefs.Save();
        }

        /// <summary>Testing only: forget this run's check, so the next read checks the saves again.</summary>
        public static void ForgetCheck() => checkedThisRun = false;

        /// <summary>Testing only: pretend a cheat tool changed the coins in memory.</summary>
        public static void SimulateMemoryEdit(int fakeCoins) => coins.CorruptTo(fakeCoins);

        // ------------------------------------------------------------------ the saved state

        /// <summary>Coins, unlocks and purchases as one line, the same way every time: "c=250|o=a,b|p=x".</summary>
        private static string Describe()
        {
            var owned = new List<string>();
            var bought = new List<string>();
            foreach (string id in KnownItemIds()) if (PlayerPrefs.GetInt("palace.own." + id, 0) == 1) owned.Add(id);
            foreach (var p in ShopCatalog.Products) if (PlayerPrefs.GetInt("palace.iap.owned." + p.Id, 0) == 1) bought.Add(p.Id);
            owned.Sort(StringComparer.Ordinal);
            bought.Sort(StringComparer.Ordinal);
            return "c=" + PlayerPrefs.GetInt(CoinsKey, 0) + "|o=" + string.Join(",", owned) + "|p=" + string.Join(",", bought);
        }

        private static IEnumerable<string> KnownItemIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var lib = CosmeticLibrary.Instance;
            if (lib != null)
                foreach (CosmeticCategory c in Enum.GetValues(typeof(CosmeticCategory)))
                    foreach (var item in lib.Items(c))
                        if (!string.IsNullOrEmpty(item.id)) ids.Add(item.id);
            foreach (var p in ShopCatalog.Products)
                foreach (string unlock in p.Unlocks) ids.Add(unlock);
            return ids;
        }

        /// <summary>Puts back the coins, unlocks and purchases from the last trusted backup (or plain defaults).</summary>
        private static void RestoreFromBackup()
        {
            string trusted = null;
            string text = Unscramble(PlayerPrefs.GetString(BackupKey, ""));
            int split = text != null ? text.LastIndexOf('\n') : -1;
            if (split > 0 && Equal(text.Substring(split + 1), Sign(text.Substring(0, split)))) trusted = text.Substring(0, split);

            int restoredCoins = 0;
            var owned = new HashSet<string>(StringComparer.Ordinal);
            var bought = new HashSet<string>(StringComparer.Ordinal);
            if (trusted != null)
            {
                foreach (string part in trusted.Split('|'))
                {
                    if (part.StartsWith("c=", StringComparison.Ordinal)) int.TryParse(part.Substring(2), out restoredCoins);
                    else if (part.StartsWith("o=", StringComparison.Ordinal)) owned.UnionWith(part.Substring(2).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                    else if (part.StartsWith("p=", StringComparison.Ordinal)) bought.UnionWith(part.Substring(2).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                }
            }
            // (With no trusted backup at all, the backup was edited too: coins go to 0 and paid-for items can
            // still be brought back with Restore purchases.)

            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, restoredCoins));
            foreach (string id in KnownItemIds())
            {
                if (owned.Contains(id)) PlayerPrefs.SetInt("palace.own." + id, 1);
                else PlayerPrefs.DeleteKey("palace.own." + id);
            }
            foreach (var p in ShopCatalog.Products)
            {
                if (bought.Contains(p.Id)) PlayerPrefs.SetInt("palace.iap.owned." + p.Id, 1);
                else PlayerPrefs.DeleteKey("palace.iap.owned." + p.Id);
            }
            Resign();
        }

        // ------------------------------------------------------------------ fingerprints and scrambling

        // The key is kept in two halves and joined at run time, so it doesn't sit in the game as one string.
        private static readonly byte[] KeyA = { 0x5A, 0x13, 0xC7, 0x2E, 0x91, 0x48, 0xB3, 0x0D, 0x76, 0xE2, 0x3F, 0xA8, 0x64, 0x1B, 0xD9, 0x87 };
        private static readonly byte[] KeyB = { 0x2C, 0x9E, 0x41, 0xF5, 0x08, 0x73, 0xBA, 0x66, 0xD1, 0x1F, 0x8C, 0x35, 0xE7, 0x52, 0x0A, 0xC4 };

        private static byte[] Key()
        {
            var key = new byte[32];
            for (int i = 0; i < 16; i++) { key[i] = (byte)(KeyA[i] ^ 0x6D); key[16 + i] = (byte)(KeyB[15 - i] ^ KeyA[i]); }
            return key;
        }

        private static string Sign(string text)
        {
            using (var hmac = new HMACSHA256(Key()))
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes("shedhead|" + text)));
        }

        /// <summary>Compares fingerprints without stopping at the first difference.</summary>
        private static bool Equal(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static string Scramble(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text), key = Key();
            for (int i = 0; i < data.Length; i++) data[i] ^= (byte)(key[i % key.Length] + i * 31);
            return Convert.ToBase64String(data);
        }

        private static string Unscramble(string scrambled)
        {
            if (string.IsNullOrEmpty(scrambled)) return null;
            try
            {
                byte[] data = Convert.FromBase64String(scrambled), key = Key();
                for (int i = 0; i < data.Length; i++) data[i] ^= (byte)(key[i % key.Length] + i * 31);
                return Encoding.UTF8.GetString(data);
            }
            catch (FormatException) { return null; }
        }

        /// <summary>
        /// A number kept scrambled in memory: XORed with a random key, plus a check value, so memory
        /// scanners can't find it by searching for the number and a changed value is noticed.
        /// </summary>
        private struct HiddenInt
        {
            private static readonly System.Random Rng = new System.Random(Guid.NewGuid().GetHashCode());
            private int key, masked, check;

            public HiddenInt(int value)
            {
                key = Rng.Next(int.MinValue, int.MaxValue) | 1;
                masked = value ^ key;
                check = Mix(value, key);
            }

            public bool TryGet(out int value)
            {
                value = masked ^ key;
                return key != 0 && Mix(value, key) == check;
            }

            /// <summary>Testing: change the hidden number without updating its check, like a memory editor would.</summary>
            public void CorruptTo(int fake) => masked = fake ^ key;

            private static int Mix(int value, int key) => ((value << 7) | (int)((uint)value >> 25)) ^ ~key ^ 0x2F6B1D3;
        }
    }
}
