using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Palace.EditorTools
{
    /// <summary>
    /// Name fonts for the Customise screen. The font files are in Assets/Palace/Fonts/Names, from
    /// Google Fonts (github.com/google/fonts), each with its licence file next to it (SIL Open Font
    /// License or Apache 2.0: free to use in a commercial game). This turns each one into a
    /// TextMeshPro font (dynamic atlas: letters are added as they're needed) and lists it for sale.
    /// To add a font: drop the .ttf (and its licence) in that folder and add a line to Fonts.
    /// </summary>
    public static class PalaceNameFonts
    {
        public const string Folder = "Assets/Palace/Fonts/Names";

        // file, name shown in the game, price before Cosmetics.PriceMultiplier (x10), size adjustment
        private static readonly (string file, string name, int price, float scale)[] Fonts =
        {
            ("Righteous-Regular", "Righteous", 150, 1f),
            ("Bangers-Regular", "Bangers", 150, 1.05f),
            ("LuckiestGuy-Regular", "Luckiest Guy", 200, 0.95f),
            ("PermanentMarker-Regular", "Marker", 150, 1f),
            ("Lobster-Regular", "Lobster", 200, 1.05f),
            ("Pacifico-Regular", "Pacifico", 200, 0.9f),
            ("SpecialElite-Regular", "Typewriter", 150, 1f),
            ("BlackOpsOne-Regular", "Black Ops", 200, 0.95f),
            ("Bungee-Regular", "Bungee", 200, 0.9f),
            ("Creepster-Regular", "Creepster", 250, 1.05f),
            ("Monoton-Regular", "Monoton", 250, 0.9f),
            ("PressStart2P-Regular", "Pixel", 250, 0.7f)
        };

        [MenuItem("Palace/Setup/Rebuild Name Fonts", priority = 31)]
        public static void RebuildMenu()
        {
            var lib = AssetDatabase.LoadAssetAtPath<CosmeticLibrary>(PalaceArtGenerator.LibraryPath);
            if (lib == null)
            {
                Debug.LogWarning("[Palace] Run Palace > Setup > 2 - Generate Art first.");
                return;
            }
            AddToLibrary(lib);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Palace] Name fonts rebuilt: " + lib.fonts.Count + " fonts.");
        }

        /// <summary>The default (the game's own font, free) plus every name font found in the folder.</summary>
        public static void AddToLibrary(CosmeticLibrary lib)
        {
            lib.fonts = new List<FontSkin> { new FontSkin { id = "font_default", displayName = "Classic", price = 0, font = null } };
            foreach (var (file, name, price, scale) in Fonts)
            {
                TMP_FontAsset asset = CreateOrLoad(file);
                if (asset == null) continue;
                lib.fonts.Add(new FontSkin { id = "font_" + file.Replace("-Regular", "").ToLowerInvariant(), displayName = name, price = price, font = asset, sizeScale = scale });
            }
        }

        private static TMP_FontAsset CreateOrLoad(string file)
        {
            string assetPath = Folder + "/" + file + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            string ttf = Folder + "/" + file + ".ttf";
            if (!File.Exists(ttf)) return null;
            AssetDatabase.ImportAsset(ttf);
            var source = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (source == null) return null;

            var asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null) return null;
            asset.name = file + " SDF";
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.material.name = file + " SDF Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var atlas in asset.atlasTextures)
            {
                atlas.name = file + " SDF Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            // Letters a display font lacks (Polish / Spanish accents, symbols) come from the game font.
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PalaceArtGenerator.SerifAssetPath);
            asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (fallback != null) asset.fallbackFontAssetTable.Add(fallback);
            if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null) asset.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
