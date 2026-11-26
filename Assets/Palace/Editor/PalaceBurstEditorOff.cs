using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Palace.EditorTools
{
    /// <summary>
    /// Windows 11 Smart App Control blocks the unsigned DLLs that Burst compiles while the EDITOR runs
    /// (Library/BurstCache/JIT), which pops up "Part of this app has been blocked" again and again.
    /// The game doesn't need Burst in the editor, so this switches editor Burst compilation off each
    /// time Unity starts (the same as unticking Jobs > Burst > Enable Compilation). Player builds still
    /// use Burst as set in Project Settings > Burst AOT Settings.
    /// Palace > Setup > Allow Burst In Editor turns this off.
    /// </summary>
    [InitializeOnLoad]
    public static class PalaceBurstEditorOff
    {
        private const string AllowKey = "Palace.AllowBurstInEditor";

        static PalaceBurstEditorOff()
        {
#if UNITY_EDITOR_WIN
            EditorApplication.delayCall += Apply;
#endif
        }

        private static void Apply()
        {
            if (EditorPrefs.GetBool(AllowKey, false)) return;
            try
            {
                // Through reflection, so this compiles even if the Burst package is ever removed.
                Type compiler = Type.GetType("Unity.Burst.BurstCompiler, Unity.Burst");
                object options = compiler?.GetProperty("Options", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                PropertyInfo enable = options?.GetType().GetProperty("EnableBurstCompilation", BindingFlags.Public | BindingFlags.Instance);
                if (enable == null || !(bool)enable.GetValue(options)) return;
                enable.SetValue(options, false);
                Debug.Log("[Palace] Burst compilation is off in the editor (it set off Windows Security pop-ups). Builds still use Burst.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Palace] Couldn't switch editor Burst off: " + e.Message);
            }
        }

        [MenuItem("Palace/Setup/Allow Burst In Editor", priority = 40)]
        private static void ToggleAllow()
        {
            bool allow = !EditorPrefs.GetBool(AllowKey, false);
            EditorPrefs.SetBool(AllowKey, allow);
            Debug.Log(allow ? "[Palace] Burst may run in the editor again: re-enable it under Jobs > Burst > Enable Compilation."
                            : "[Palace] Burst will be switched off in the editor next time Unity starts.");
            if (!allow) Apply();
        }

        [MenuItem("Palace/Setup/Allow Burst In Editor", true)]
        private static bool ToggleAllowCheck()
        {
            Menu.SetChecked("Palace/Setup/Allow Burst In Editor", EditorPrefs.GetBool(AllowKey, false));
            return true;
        }
    }
}
