using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Palace.EditorTools
{
    /// <summary>
    /// One-click project configuration for Palace:
    ///  - landscape-only orientation on Android/iOS
    ///  - 1920x1080 resizable window on Windows
    ///  - imports the TextMeshPro essential resources (default font) if missing
    ///
    /// Menu: Palace > Setup > 1 - Apply Project Settings
    /// </summary>
    public static class PalaceProjectSetup
    {
        private static readonly string[] TmpEssentialsPaths =
        {
            "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage",
            "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage"
        };

        [MenuItem("Palace/Setup/Run Full Stage 1 Setup", priority = 0)]
        public static void RunAll() => RunAllThen(null);

        /// <summary>Runs steps 1-3 in order, waiting for the TextMeshPro import to finish first.</summary>
        public static void RunAllThen(Action afterwards)
        {
            ApplyProjectSettingsThen(() =>
            {
                PalaceArtGenerator.GenerateAll();
                PalaceSceneBuilder.BuildPrefabsAndScene();
                afterwards?.Invoke();
            });
        }

        [MenuItem("Palace/Setup/1 - Apply Project Settings", priority = 1)]
        public static void ApplyProjectSettings() => ApplyProjectSettingsThen(null);

        public static void ApplyProjectSettingsThen(Action whenReady)
        {
            // Mobile: landscape only, allowed to flip between the two landscape directions.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Windows: start as a 1920x1080 window that the player can resize or maximise.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true; // keeps online games ticking when the window loses focus

            AssetDatabase.SaveAssets();
            Debug.Log("[Palace] Project settings applied: landscape mobile, 1920x1080 window on PC.");

            ImportTextMeshProEssentialsThen(whenReady);
        }

        public static bool HasTextMeshProEssentials() => AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;

        /// <summary>
        /// Imports the TextMeshPro default font and settings if they are missing.
        /// Unity imports packages asynchronously, so whenReady runs once the import has finished.
        /// </summary>
        public static void ImportTextMeshProEssentialsThen(Action whenReady)
        {
            if (HasTextMeshProEssentials())
            {
                whenReady?.Invoke();
                return;
            }

            string package = TmpEssentialsPaths.FirstOrDefault(p => File.Exists(Path.GetFullPath(p)));
            if (package == null)
            {
                Debug.LogWarning("[Palace] Could not find TMP Essential Resources. Use Window > TextMeshPro > Import TMP Essential Resources.");
                whenReady?.Invoke();
                return;
            }

            bool finished = false;
            void Finish(string message)
            {
                if (finished) return;
                finished = true;
                AssetDatabase.importPackageCompleted -= OnCompleted;
                AssetDatabase.importPackageFailed -= OnFailed;
                AssetDatabase.importPackageCancelled -= OnCancelled;
                Debug.Log("[Palace] " + message);
                whenReady?.Invoke();
            }
            void OnCompleted(string name) => Finish("Imported TextMeshPro Essential Resources.");
            void OnFailed(string name, string error) => Finish("TextMeshPro import failed: " + error);
            void OnCancelled(string name) => Finish("TextMeshPro import cancelled.");

            AssetDatabase.importPackageCompleted += OnCompleted;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.importPackageCancelled += OnCancelled;
#pragma warning disable CS0618 // Still the most widely available import API (and the one that raises the events above).
            AssetDatabase.ImportPackage(package, false);
#pragma warning restore CS0618
        }
    }
}
