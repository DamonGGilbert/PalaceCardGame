using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Palace.EditorTools
{
    /// <summary>
    /// Shedhead branding: the logo's import settings, the app icons and the app's display name.
    /// Applied every time the project opens (it only changes anything if something is missing), and
    /// from Palace > Setup > Apply Shedhead Branding.
    ///
    ///  Art/Icons/shedhead_icon.png            square, full bleed, no transparency: iOS, Android (older
    ///                                         launchers), Windows and the default for everything
    ///  Art/Icons/shedhead_icon_android_fg/bg  Android adaptive icon layers: the logo sits inside the safe
    ///                                         zone, so circle / squircle / teardrop masks never cut it off
    ///
    /// The code keeps its internal "Palace" names; players only ever see "Shedhead".
    /// </summary>
    [InitializeOnLoad]
    public static class PalaceBranding
    {
        public const string LogoPath = "Assets/Palace/Resources/Brand/shedhead_logo.png";
        public const string IconPath = "Assets/Palace/Art/Icons/shedhead_icon.png";
        public const string AndroidForegroundPath = "Assets/Palace/Art/Icons/shedhead_icon_android_fg.png";
        public const string AndroidBackgroundPath = "Assets/Palace/Art/Icons/shedhead_icon_android_bg.png";
        public const string ProductName = "Shedhead";
        public const string CompanyName = "Shedhead";

        /// <summary>
        /// The app's permanent id in Google Play and the App Store. NEVER change this once the game is
        /// published: a different id is a different app (new listing, no reviews, players lose their saves).
        /// </summary>
        public const string AppId = "com.shedhead.game";

        static PalaceBranding()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode) Apply(false);
            };
        }

        [MenuItem("Palace/Setup/Apply Shedhead Branding", priority = 33)]
        public static void ApplyMenu() => Apply(true);

        public static bool Apply(bool log)
        {
            Prepare(LogoPath, TextureImporterType.Sprite, 2048, TextureImporterCompression.CompressedHQ);
            Prepare(IconPath, TextureImporterType.Default, 1024, TextureImporterCompression.Uncompressed);
            Prepare(AndroidForegroundPath, TextureImporterType.Default, 1024, TextureImporterCompression.Uncompressed);
            Prepare(AndroidBackgroundPath, TextureImporterType.Default, 1024, TextureImporterCompression.Uncompressed);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) return false;

            if (PlayerSettings.productName != ProductName) PlayerSettings.productName = ProductName;
            ApplyIdentity();

            // The default icon (every platform), plus iOS explicitly.
            SetIfDifferent(NamedBuildTarget.Unknown, icon);
            SetIfDifferent(NamedBuildTarget.iOS, icon);

            bool android = ApplyAndroid(icon);
            AssetDatabase.SaveAssets();
            if (log)
                Debug.Log("[Palace] Shedhead icon set" + (android ? " (with the Android adaptive icon)" :
                    ". Install Android Build Support in Unity Hub, then run this again for the Android adaptive icon") + ".");
            return true;
        }

        /// <summary>
        /// Company name and app id on every platform, plus the App Bundle (.aab) that Google Play wants.
        /// Saved progress follows along: PrefsMigration copies Windows saves from the old company name.
        /// </summary>
        private static void ApplyIdentity()
        {
            if (PlayerSettings.companyName != CompanyName) PlayerSettings.companyName = CompanyName;
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
                if (PlayerSettings.GetApplicationIdentifier(target) != AppId) PlayerSettings.SetApplicationIdentifier(target, AppId);
            if (!EditorUserBuildSettings.buildAppBundle) EditorUserBuildSettings.buildAppBundle = true;
        }

        private static void SetIfDifferent(NamedBuildTarget target, Texture2D icon)
        {
            Texture2D[] current = PlayerSettings.GetIcons(target, IconKind.Any);
            if (current != null && current.Length > 0 && current[0] == icon) return;
            int count = Math.Max(1, PlayerSettings.GetIconSizes(target, IconKind.Any).Length);
            var icons = new Texture2D[count];
            for (int i = 0; i < count; i++) icons[i] = icon;
            PlayerSettings.SetIcons(target, icons, IconKind.Any);
        }

        /// <summary>
        /// Android's own icon types live in the Android module, which may not be installed, so they're
        /// found by name. Returns false if the module isn't installed yet.
        /// </summary>
        private static bool ApplyAndroid(Texture2D square)
        {
            Type kinds = Type.GetType("UnityEditor.Android.AndroidPlatformIconKind, UnityEditor.Android.Extensions");
            if (kinds == null) return false;
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(AndroidForegroundPath);
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(AndroidBackgroundPath);

            PlatformIconKind Kind(string name) =>
                kinds.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as PlatformIconKind;

            void Set(PlatformIconKind kind, params Texture2D[] layers)
            {
                if (kind == null || layers == null || Array.Exists(layers, t => t == null)) return;
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var platformIcon in icons) platformIcon.SetTextures(layers);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }

            Set(Kind("Adaptive"), bg, fg);  // layers: background, then foreground
            Set(Kind("Round"), square);
            Set(Kind("Legacy"), square);
            return true;
        }

        private static void Prepare(string path, TextureImporterType type, int maxSize, TextureImporterCompression compression)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == type && !importer.mipmapEnabled && importer.maxTextureSize == maxSize
                && importer.textureCompression == compression) return;
            importer.textureType = type;
            if (type == TextureImporterType.Sprite) importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = compression;
            importer.SaveAndReimport();
        }
    }
}
