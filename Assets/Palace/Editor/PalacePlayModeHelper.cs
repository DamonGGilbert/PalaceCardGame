using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Palace.EditorTools
{
    /// <summary>
    /// Makes play-testing foolproof:
    ///  - If the Palace scenes haven't been built yet, offers to run the full setup.
    ///  - Pressing Play from ANY scene starts the game at the Main Menu
    ///    (turn off with Palace > Always Play From Main Menu).
    /// </summary>
    [InitializeOnLoad]
    public static class PalacePlayModeHelper
    {
        private const string PrefKey = "Palace.PlayFromMainMenu";
        private const string MenuPath = "Palace/Always Play From Main Menu";

        static PalacePlayModeHelper()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Check;
        }

        private static bool PlayFromMenu
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        private static void Check()
        {
            var menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PalaceSceneBuilder.MenuScenePath);
            if (menuScene == null)
            {
                bool run = EditorUtility.DisplayDialog("Palace setup needed",
                    "The Palace menu and game scenes haven't been built yet.\n\nRun the full setup now? It takes about 20 seconds.",
                    "Run setup", "Later");
                if (run) PalaceProjectSetup.RunAllThen(AfterSetup);
                return;
            }
            ApplyStartScene();
        }

        /// <summary>Called when the full setup finishes: open the menu scene ready for Play.</summary>
        public static void AfterSetup()
        {
            ApplyStartScene();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PalaceSceneBuilder.MenuScenePath) != null)
                EditorSceneManager.OpenScene(PalaceSceneBuilder.MenuScenePath);
            Debug.Log("[Palace] Setup complete. Press Play to start at the Main Menu.");
        }

        public static void ApplyStartScene()
        {
            var menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PalaceSceneBuilder.MenuScenePath);
            EditorSceneManager.playModeStartScene = PlayFromMenu ? menuScene : null;
        }

        [MenuItem(MenuPath, priority = 20)]
        private static void Toggle()
        {
            PlayFromMenu = !PlayFromMenu;
            ApplyStartScene();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, PlayFromMenu);
            return true;
        }
    }
}
