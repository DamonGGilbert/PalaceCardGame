using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// Animated water for the Lagoon table: two layers of light ripples drift across the
    /// felt in different directions, kept inside the rail by a felt-shaped mask.
    /// </summary>
    public class WaterShimmer : MonoBehaviour
    {
        private RawImage layerA, layerB;
        private float time;

        public static WaterShimmer Create(Transform parent, Vector2 size, Vector2 position, Sprite feltMask, Texture2D caustics)
        {
            var maskImage = UIBuild.Image("Water", parent, feltMask, Color.white, size, position);
            UIBuild.OwnCanvas(maskImage.gameObject); // the ripples move every frame (mask and ripples share this canvas)
            var mask = maskImage.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var shimmer = maskImage.gameObject.AddComponent<WaterShimmer>();
            shimmer.layerA = Layer(maskImage.transform, caustics, new Color(0.75f, 0.97f, 1f, 0.14f));
            shimmer.layerB = Layer(maskImage.transform, caustics, new Color(0.55f, 0.9f, 1f, 0.09f));
            return shimmer;
        }

        private static RawImage Layer(Transform parent, Texture2D texture, Color color)
        {
            var rt = UIBuild.Stretch("Ripples", parent);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.color = color;
            raw.raycastTarget = false;
            return raw;
        }

        private void Update()
        {
            time += Time.unscaledDeltaTime;
            // Several tiles across the table; the two layers drift and "breathe" differently.
            layerA.uvRect = new Rect(time * 0.012f, time * 0.008f, 2.2f, 1.1f);
            layerB.uvRect = new Rect(-time * 0.009f + 0.37f, time * 0.011f + 0.21f, 1.7f, 0.85f);
            var a = layerA.color;
            a.a = 0.12f + 0.04f * Mathf.Sin(time * 0.9f);
            layerA.color = a;
        }
    }
}
