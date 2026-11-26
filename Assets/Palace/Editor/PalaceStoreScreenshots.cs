using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Palace.EditorTools
{
    /// <summary>
    /// Store screenshots: press Play, set the Game view to 1920x1080 (or bigger), get to a good moment
    /// and press F12 (or Palace > Store > Take Screenshot). Saved to Docs/StoreAssets/Screenshots as
    /// 24-bit PNGs with no transparency, which is what Google Play and the App Store want.
    /// </summary>
    public static class PalaceStoreScreenshots
    {
        public static string Folder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Docs", "StoreAssets", "Screenshots");

        [MenuItem("Palace/Store/Take Screenshot _F12", priority = 1)]
        public static void Take()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Palace] Press Play first: screenshots are taken from the Game view.");
                return;
            }
            new GameObject("StoreScreenshot") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Runner>();
        }

        [MenuItem("Palace/Store/Open Screenshots Folder", priority = 2)]
        public static void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            EditorUtility.RevealInFinder(Folder);
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                yield return new WaitForEndOfFrame();
                // Big enough for the stores (at least 1080 pixels tall) whatever size the Game view is.
                int scale = Mathf.Clamp(Mathf.CeilToInt(1080f / Mathf.Max(1, Screen.height)), 1, 4);
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture(scale);
                try
                {
                    var rgb = new Texture2D(shot.width, shot.height, TextureFormat.RGB24, false);
                    rgb.SetPixels32(shot.GetPixels32());
                    rgb.Apply();
                    Directory.CreateDirectory(Folder);
                    string path = Path.Combine(Folder, "shedhead_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                    File.WriteAllBytes(path, rgb.EncodeToPNG());
                    DestroyImmediate(rgb);
                    float ratio = (float)Mathf.Max(shot.width, shot.height) / Mathf.Min(shot.width, shot.height);
                    Debug.Log("[Palace] Screenshot saved (" + shot.width + "x" + shot.height + "): " + path +
                              (ratio > 2f ? "  NOTE: Google Play only accepts screenshots up to 2:1, so set the Game view to 16:9." : ""));
                }
                finally
                {
                    Destroy(shot);
                    Destroy(gameObject);
                }
            }
        }
    }
}
