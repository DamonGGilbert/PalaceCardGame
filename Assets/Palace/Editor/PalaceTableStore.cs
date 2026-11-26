using System.IO;
using UnityEditor;
using UnityEngine;
using static Palace.EditorTools.PalaceArtGenerator;

namespace Palace.EditorTools
{
    /// <summary>
    /// Keeps table art out of memory until it's used. Tables are the biggest images in the game
    /// (2048 x 1024, over 50 of them), and the cosmetics catalogue used to point straight at all of
    /// them, which loaded every table the moment the catalogue was read. Now:
    ///  - each table image lives in Resources/Tables and the catalogue stores only its path,
    ///    so the game loads just the one being used (TableSkin.Load);
    ///  - the Customise screen shows a small thumbnail instead of the full image;
    ///  - tables use normal compression, and flag tables (flat colours) are stored a little smaller,
    ///    which keeps the download size down.
    /// Runs at the end of Generate Art and the flag / premium regenerations.
    /// </summary>
    public static class PalaceTableStore
    {
        public const string Folder = "Assets/Palace/Resources/Tables";
        private const int ThumbWidth = 320, ThumbHeight = 160;

        [MenuItem("Palace/Setup/Organise Table Art (load on demand)", priority = 35)]
        public static void OrganiseMenu()
        {
            var lib = AssetDatabase.LoadAssetAtPath<CosmeticLibrary>(LibraryPath);
            if (lib == null) return;
            Organise(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Palace] Table art organised: " + lib.tables.Count + " tables load on demand.");
        }

        public static void Organise(CosmeticLibrary lib)
        {
            EnsureFolder(Folder);
            foreach (var skin in lib.tables)
            {
                string file = null;
                if (skin.table != null)
                {
                    string path = AssetDatabase.GetAssetPath(skin.table);
                    file = Path.GetFileNameWithoutExtension(path);
                    string dest = Folder + "/" + file + ".png";
                    if (path != dest)
                    {
                        if (File.Exists(dest)) AssetDatabase.DeleteAsset(dest); // an older copy: the new one replaces it
                        string error = AssetDatabase.MoveAsset(path, dest);      // keeps the image's id, so nothing else breaks
                        if (!string.IsNullOrEmpty(error))
                        {
                            Debug.LogWarning("[Palace] Couldn't move " + path + ": " + error);
                            continue;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(skin.tablePath)) file = Path.GetFileName(skin.tablePath);
                if (file == null) continue;

                bool flag = file.StartsWith("flag_table_");
                ConfigureSprite(file, flag ? 1536 : 2048, default, 100f, TextureImporterCompression.Compressed);

                skin.tablePath = "Tables/" + file;
                skin.table = null;                          // the catalogue no longer holds the big image
                skin.preview = Thumbnail(file) ?? skin.preview;
            }
            EditorUtility.SetDirty(lib);
        }

        /// <summary>A small copy of a table for the Customise screen (made once, and again if the table changes).</summary>
        private static Sprite Thumbnail(string file)
        {
            string source = Folder + "/" + file + ".png";
            string thumb = ArtFolder + "/thumb_" + file + ".png";
            if (!File.Exists(source)) return null;
            bool upToDate = File.Exists(thumb) && File.GetLastWriteTimeUtc(thumb) >= File.GetLastWriteTimeUtc(source);
            if (!upToDate)
            {
                var full = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                full.LoadImage(File.ReadAllBytes(source));
                // Halve a few times before the final size, so the small copy is smooth rather than speckled.
                Texture current = full;
                int w = full.width, h = full.height;
                RenderTexture last = null;
                while (w / 2 >= ThumbWidth)
                {
                    w /= 2; h /= 2;
                    var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    rt.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(current, rt);
                    if (last != null) RenderTexture.ReleaseTemporary(last);
                    current = last = rt;
                }
                var final = RenderTexture.GetTemporary(ThumbWidth, ThumbHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(current, final);
                if (last != null) RenderTexture.ReleaseTemporary(last);

                var active = RenderTexture.active;
                RenderTexture.active = final;
                var small = new Texture2D(ThumbWidth, ThumbHeight, TextureFormat.RGBA32, false);
                small.ReadPixels(new Rect(0, 0, ThumbWidth, ThumbHeight), 0, 0);
                small.Apply();
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(final);

                File.WriteAllBytes(thumb, small.EncodeToPNG());
                Object.DestroyImmediate(full);
                Object.DestroyImmediate(small);
                AssetDatabase.ImportAsset(thumb);
            }
            ConfigureSprite("thumb_" + file, 512);
            return LoadSprite("thumb_" + file);
        }
    }
}
