using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Keeps saved progress when the app's name changes. On Windows, Unity stores PlayerPrefs (coins,
    /// unlocks, stats, settings) in the registry under "company name / product name", so renaming the
    /// game from "PalaceCardGame" to "Shedhead" pointed it at a new, empty place. The first time the game
    /// starts under the new name, this copies every saved "palace.*" value across from any older name
    /// (never overwriting something already saved under the new name), then never runs again.
    ///
    /// It also covers a future change of company name: any company's "PalaceCardGame" or "Shedhead"
    /// saves are found. On Android and iOS, saves belong to the app's id (bundle identifier) instead,
    /// which isn't affected by the name. Keep that id the same once the game is in the stores.
    /// </summary>
    public static class PrefsMigration
    {
        private const string DoneKey = "palace.prefsmigrated";
        private static readonly string[] OldProductNames = { "PalaceCardGame", "Shedhead" };
        private static readonly HashSet<string> FloatKeys = new HashSet<string> { "palace.music", "palace.sfx" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Run()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (PlayerPrefs.GetInt(DoneKey, 0) == 1) return;
            int copied = 0;
            try
            {
                string root = Application.isEditor ? @"Software\Unity\UnityEditor" : "Software";
                foreach (string company in Registry.SubKeys(root))
                    foreach (string product in OldProductNames)
                    {
                        if (company == Application.companyName && product == Application.productName) continue; // that's where we are now
                        copied += CopyFrom(root + "\\" + company + "\\" + product);
                    }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Palace] Couldn't look for older saves: " + e.Message);
            }
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
            if (copied > 0) Debug.Log("[Palace] Brought " + copied + " saved values over from the game's old name.");
#endif
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private static int CopyFrom(string path)
        {
            int copied = 0;
            foreach (var (rawName, type, data) in Registry.Values(path))
            {
                string name = StripHash(rawName);
                if (!name.StartsWith("palace.", StringComparison.Ordinal) || name == DoneKey || PlayerPrefs.HasKey(name)) continue;
                switch (type)
                {
                    case Registry.Binary: // strings: UTF-8 with a trailing zero
                        PlayerPrefs.SetString(name, Encoding.UTF8.GetString(data).TrimEnd('\0'));
                        break;
                    case Registry.DWord:
                    case Registry.QWord:
                        if (FloatKeys.Contains(name))
                        {
                            // Unity keeps floats as an 8-byte double.
                            if (data.Length >= 8) PlayerPrefs.SetFloat(name, (float)BitConverter.ToDouble(data, 0));
                            else if (data.Length >= 4) PlayerPrefs.SetFloat(name, BitConverter.ToSingle(data, 0));
                            else continue;
                        }
                        else if (data.Length >= 4) PlayerPrefs.SetInt(name, BitConverter.ToInt32(data, 0));
                        else continue;
                        break;
                    default:
                        continue;
                }
                copied++;
            }
            return copied;
        }

        /// <summary>Unity adds "_h" plus a hash to each name in the registry: "palace.coins_h1547356873" -> "palace.coins".</summary>
        private static string StripHash(string name)
        {
            int at = name.LastIndexOf("_h", StringComparison.Ordinal);
            if (at <= 0) return name;
            for (int i = at + 2; i < name.Length; i++) if (!char.IsDigit(name[i])) return name;
            return name.Substring(0, at);
        }

        /// <summary>Just enough of the Windows registry to read old saves (read only).</summary>
        private static class Registry
        {
            public const int Binary = 3, DWord = 4, QWord = 11;
            private static readonly UIntPtr CurrentUser = new UIntPtr(0x80000001u);
            private const int KeyRead = 0x20019, MoreData = 234;

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
            private static extern int RegOpenKeyEx(UIntPtr key, string subKey, int options, int access, out IntPtr result);

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
            private static extern int RegEnumKeyEx(IntPtr key, int index, StringBuilder name, ref int nameLength,
                IntPtr reserved, IntPtr className, IntPtr classLength, IntPtr lastWrite);

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
            private static extern int RegEnumValue(IntPtr key, int index, StringBuilder name, ref int nameLength,
                IntPtr reserved, out int type, byte[] data, ref int dataLength);

            [DllImport("advapi32.dll")]
            private static extern int RegCloseKey(IntPtr key);

            public static List<string> SubKeys(string path)
            {
                var list = new List<string>();
                if (RegOpenKeyEx(CurrentUser, path, 0, KeyRead, out IntPtr key) != 0) return list;
                try
                {
                    var name = new StringBuilder(512);
                    for (int i = 0; ; i++)
                    {
                        int length = name.Capacity;
                        if (RegEnumKeyEx(key, i, name, ref length, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) != 0) break;
                        list.Add(name.ToString(0, length));
                    }
                }
                finally { RegCloseKey(key); }
                return list;
            }

            public static List<(string name, int type, byte[] data)> Values(string path)
            {
                var list = new List<(string, int, byte[])>();
                if (RegOpenKeyEx(CurrentUser, path, 0, KeyRead, out IntPtr key) != 0) return list;
                try
                {
                    var name = new StringBuilder(1024);
                    var buffer = new byte[4096];
                    for (int i = 0; ; i++)
                    {
                        int nameLength = name.Capacity, dataLength = buffer.Length;
                        int result = RegEnumValue(key, i, name, ref nameLength, IntPtr.Zero, out int type, buffer, ref dataLength);
                        if (result == MoreData)
                        {
                            buffer = new byte[Math.Max(dataLength, buffer.Length * 2)];
                            nameLength = name.Capacity;
                            dataLength = buffer.Length;
                            result = RegEnumValue(key, i, name, ref nameLength, IntPtr.Zero, out type, buffer, ref dataLength);
                        }
                        if (result != 0) break;
                        var data = new byte[dataLength];
                        Array.Copy(buffer, data, dataLength);
                        list.Add((name.ToString(0, nameLength), type, data));
                    }
                }
                finally { RegCloseKey(key); }
                return list;
            }
        }
#endif
    }
}
