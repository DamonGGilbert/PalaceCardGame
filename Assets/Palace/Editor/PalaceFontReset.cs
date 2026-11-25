using TMPro;
using UnityEditor;
using UnityEngine;

namespace Palace.EditorTools
{
    /// <summary>
    /// The game's fonts are "dynamic": while you play, Unity adds each letter it needs to the font
    /// file, so the files changed after every Play (by hundreds of thousands of lines) and always
    /// showed up as modified in git. This clears those cached letters when you stop playing (and on
    /// request), so the files go back to their saved state. The fonts still work exactly the same;
    /// letters are simply cached again as text appears, which is instant.
    /// Fonts are also set to clear this cache when a build is made, so builds stay small.
    /// </summary>
    [InitializeOnLoad]
    public static class PalaceFontReset
    {
        private const string Folder = "Assets/Palace/Fonts";

        static PalaceFontReset()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) ClearCachedLetters(false);
            };
            // Also after scripts reload (e.g. when the project opens), so files left over from before are tidied up.
            EditorApplication.delayCall += () =>
            {
                if (!Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode) ClearCachedLetters(false);
            };
        }

        [MenuItem("Palace/Setup/Clear Cached Font Letters", priority = 32)]
        public static void ClearMenu() => ClearCachedLetters(true);

        public static void ClearCachedLetters(bool log)
        {
            int cleared = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { Folder }))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;
                // "Clear Dynamic Data On Build" (the setting isn't public in code, so set it the way the Inspector does).
                var so = new SerializedObject(font);
                var clearOnBuild = so.FindProperty("m_ClearDynamicDataOnBuild");
                if (clearOnBuild != null && !clearOnBuild.boolValue)
                {
                    clearOnBuild.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (font.characterTable.Count == 0 && font.glyphTable.Count == 0) continue; // already clean
                font.ClearFontAssetData(true);
                EditorUtility.SetDirty(font);
                cleared++;
            }
            if (cleared > 0) AssetDatabase.SaveAssets();
            if (log) Debug.Log("[Palace] Cleared cached letters from " + cleared + " font(s).");
        }
    }
}
