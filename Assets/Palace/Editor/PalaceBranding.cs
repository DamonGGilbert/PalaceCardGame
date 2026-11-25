using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Palace.EditorTools
{
    /// <summary>
    /// Shedhead branding: the logo's import settings, the app icon and the app's display name.
    /// Runs once when the project opens (and again from Palace > Setup > Apply Shedhead Branding).
    /// The code keeps its internal "Palace" names; players only ever see "Shedhead".
    /// </summary>
    [InitializeOnLoad]
    public static class PalaceBranding
    {
        public const string LogoPath = "Assets/Palace/Resources/Brand/shedhead_logo.png";
        public const string ProductName = "Shedhead";
        private const string DoneKey = "Palace.BrandingApplied.v1";

        static PalaceBranding()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(DoneKey + "." + Application.dataPath, false)) return;
                if (Apply()) EditorPrefs.SetBool(DoneKey + "." + Application.dataPath, true);
            };
        }

        [MenuItem("Palace/Setup/Apply Shedhead Branding", priority = 33)]
        public static void ApplyMenu()
        {
            if (Apply()) Debug.Log("[Palace] Shedhead logo set as the app icon; app name is \"" + ProductName + "\".");
        }

        /// <summary>Returns false if the logo file isn't there yet.</summary>
        public static bool Apply()
        {
            var importer = AssetImporter.GetAtPath(LogoPath) as TextureImporter;
            if (importer == null) return false;

            // A crisp sprite for the splash screen, and a full-quality source for the icons.
            bool changed = importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.maxTextureSize != 2048
                           || importer.textureCompression != TextureImporterCompression.CompressedHQ;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            if (logo == null) return false;

            // The default icon: every platform uses it unless it has its own icons set.
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { logo }, IconKind.Any);
            PlayerSettings.productName = ProductName;
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
