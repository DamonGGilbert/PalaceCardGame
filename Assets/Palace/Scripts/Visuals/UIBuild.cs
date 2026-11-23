using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// Small helpers for creating uGUI objects from code. The views use these to
    /// build their own child objects, so prefabs never end up with missing references.
    /// </summary>
    public static class UIBuild
    {
        public const int UILayer = 5;

        public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            return Rect(name, parent, size, position, new Vector2(0.5f, 0.5f));
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        /// <summary>A rect that fills its parent, optionally inset by a margin.</summary>
        public static RectTransform Stretch(string name, Transform parent, float inset = 0f)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, Vector2 size, Vector2 position)
        {
            var img = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image StretchImage(string name, Transform parent, Sprite sprite, Color color, float inset = 0f)
        {
            var img = Stretch(name, parent, inset).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float fontSize, Color color,
            Vector2 size, Vector2 position, TextAlignmentOptions alignment, TMP_FontAsset font = null, bool bold = false)
        {
            var tmp = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            if (font == null && TMP_Settings.instance != null) font = TMP_Settings.defaultFontAsset;
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            // Let text draw past its box rather than vanish (Truncate hides a line that is too TALL,
            // which made big titles disappear with taller fonts). Names on banners opt in to "…".
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }
    }
}
