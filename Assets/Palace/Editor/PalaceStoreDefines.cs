using UnityEditor;
using UnityEditor.Build;

namespace Palace.EditorTools
{
    /// <summary>
    /// Switches real purchasing on automatically: when the "In App Purchasing" package is
    /// installed (Window > Package Manager), the PALACE_IAP define is added so UnityIapBackend
    /// compiles. Remove the package and the define is removed again. No manual steps needed.
    /// </summary>
    [InitializeOnLoad]
    public static class PalaceStoreDefines
    {
        private const string Define = "PALACE_IAP";

        static PalaceStoreDefines()
        {
            EditorApplication.delayCall += Sync;
        }

        [MenuItem("Palace/Setup/Refresh Store Settings", priority = 10)]
        public static void Sync()
        {
            bool installed = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.purchasing/package.json") != null;
            foreach (var target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                string defines = PlayerSettings.GetScriptingDefineSymbols(target);
                bool has = (";" + defines + ";").Contains(";" + Define + ";");
                if (installed == has) continue;
                defines = installed
                    ? (string.IsNullOrEmpty(defines) ? Define : defines + ";" + Define)
                    : string.Join(";", System.Array.FindAll(defines.Split(';'), d => d != Define));
                PlayerSettings.SetScriptingDefineSymbols(target, defines);
            }
        }
    }
}
