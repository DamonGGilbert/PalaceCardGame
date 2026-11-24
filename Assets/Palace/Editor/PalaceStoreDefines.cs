using UnityEditor;
using UnityEditor.Build;

namespace Palace.EditorTools
{
    /// <summary>
    /// Switches optional features on automatically when their Unity package is installed
    /// (Window > Package Manager), and off again if it is removed:
    ///   In App Purchasing  -> PALACE_IAP        (real store purchases)
    ///   Cloud Save         -> PALACE_CLOUDSAVE  (progress backup / restore)
    ///   Mobile Notifications -> PALACE_NOTIFICATIONS (optional daily streak reminders)
    /// The game always compiles either way; nothing to do by hand.
    /// </summary>
    [InitializeOnLoad]
    public static class PalaceStoreDefines
    {
        private static readonly (string package, string define)[] Features =
        {
            ("com.unity.purchasing", "PALACE_IAP"),
            ("com.unity.services.cloudsave", "PALACE_CLOUDSAVE"),
            ("com.unity.mobile.notifications", "PALACE_NOTIFICATIONS")
        };

        static PalaceStoreDefines()
        {
            EditorApplication.delayCall += Sync;
        }

        [MenuItem("Palace/Setup/Refresh Optional Features", priority = 10)]
        public static void Sync()
        {
            foreach (var target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                string original = PlayerSettings.GetScriptingDefineSymbols(target);
                string defines = original;
                foreach (var (package, define) in Features)
                {
                    bool installed = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + package + "/package.json") != null;
                    bool has = (";" + defines + ";").Contains(";" + define + ";");
                    if (installed && !has) defines = string.IsNullOrEmpty(defines) ? define : defines + ";" + define;
                    if (!installed && has) defines = string.Join(";", System.Array.FindAll(defines.Split(';'), d => d != define));
                }
                if (defines != original) PlayerSettings.SetScriptingDefineSymbols(target, defines);
            }
        }
    }
}
