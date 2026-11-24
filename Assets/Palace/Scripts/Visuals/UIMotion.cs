using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// Small, friendly menu animations: screens pop open, buttons pop in one after another and
    /// squish when pressed, the title bobs, and hearts and stars drift up behind the menus.
    /// Everything respects Settings > Animations: with Off, nothing moves.
    /// </summary>
    public static class UIMotion
    {
        public static bool Enabled => GameSettings.AnimationScale > 0f;

        /// <summary>A screen opening: a quick fade and a soft "pop" from slightly smaller.</summary>
        public static IEnumerator Appear(Transform screen)
        {
            if (!Enabled || screen == null) yield break;
            var group = screen.GetComponent<CanvasGroup>() ?? screen.gameObject.AddComponent<CanvasGroup>();
            float t = 0f;
            const float duration = 0.24f;
            while (t < duration && screen != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                group.alpha = k;
                screen.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, BackOut(k));
                yield return null;
            }
            if (screen == null) yield break;
            group.alpha = 1f;
            screen.localScale = Vector3.one;
        }

        /// <summary>Buttons pop in one after another (a little bounce each).</summary>
        public static IEnumerator PopIn(IList<Transform> items, float gap = 0.05f)
        {
            if (!Enabled) yield break;
            foreach (var item in items) if (item != null) item.localScale = Vector3.zero;
            for (int i = 0; i < items.Count; i++)
            {
                Transform item = items[i];
                if (item != null) Runner.Get().StartCoroutine(Pop(item));
                yield return new WaitForSecondsRealtime(gap);
            }
        }

        private static IEnumerator Pop(Transform item)
        {
            float t = 0f;
            const float duration = 0.32f;
            while (t < duration && item != null)
            {
                t += Time.unscaledDeltaTime;
                item.localScale = Vector3.one * BackOut(Mathf.Clamp01(t / duration));
                yield return null;
            }
            if (item != null) item.localScale = Vector3.one;
        }

        /// <summary>Ease-out with a small overshoot, for a bouncy feel.</summary>
        public static float BackOut(float k)
        {
            const float s = 1.9f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }

        /// <summary>Runs pop-ins that outlive the coroutine that started them.</summary>
        private sealed class Runner : MonoBehaviour
        {
            private static Runner instance;

            public static Runner Get()
            {
                if (instance != null) return instance;
                var go = new GameObject("UIMotion");
                Object.DontDestroyOnLoad(go);
                return instance = go.AddComponent<Runner>();
            }
        }
    }

    /// <summary>Buttons squish when pressed and grow a touch on hover (mouse).</summary>
    public sealed class ButtonBounce : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private float target = 1f, current = 1f;
        private bool over, down;
        private Selectable selectable;

        private void Awake() => selectable = GetComponent<Selectable>();

        public void OnPointerEnter(PointerEventData e) { over = true; Retarget(); }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; Retarget(); }
        public void OnPointerDown(PointerEventData e) { down = true; Retarget(); }
        public void OnPointerUp(PointerEventData e) { down = false; Retarget(); }

        private void Retarget()
        {
            bool active = selectable == null || selectable.IsInteractable();
            target = !active || !UIMotion.Enabled ? 1f : down ? 0.93f : over ? 1.05f : 1f;
        }

        private void OnDisable()
        {
            over = down = false;
            target = current = 1f;
            if (transform.localScale.x > 0.01f) transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (Mathf.Abs(current - target) < 0.001f) return;
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            transform.localScale = Vector3.one * current;
        }
    }

    /// <summary>A gentle bob: up and down, and/or a little rock from side to side.</summary>
    public sealed class Bob : MonoBehaviour
    {
        public float Height = 6f, Degrees = 0f, Speed = 0.6f, Phase;
        private Vector2 home;
        private float baseAngle;
        private RectTransform rt;

        private void Start()
        {
            rt = (RectTransform)transform;
            home = rt.anchoredPosition;
            baseAngle = rt.localEulerAngles.z;
        }

        private void Update()
        {
            if (!UIMotion.Enabled) return;
            float s = Mathf.Sin((Time.unscaledTime * Speed + Phase) * Mathf.PI * 2f);
            rt.anchoredPosition = home + new Vector2(0f, s * Height);
            if (Degrees != 0f) rt.localEulerAngles = new Vector3(0f, 0f, baseAngle + s * Degrees);
        }
    }

    /// <summary>A soft heartbeat pulse (for things that want attention, like the challenges dot).</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float Amount = 0.12f, Speed = 1.2f;

        private void Update()
        {
            if (!UIMotion.Enabled) return;
            float s = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * Speed * Mathf.PI * 2f)), 4f);
            transform.localScale = Vector3.one * (1f + s * Amount);
        }
    }

    /// <summary>Hearts, stars, crowns and cards drifting slowly up behind the menus, very faintly.</summary>
    public sealed class Floaters : MonoBehaviour
    {
        private const int Max = 14;
        private static readonly int[] Emojis = { 7, 10, 7, 10, 9, 11 }; // heart, star, heart, star, crown, cards
        private readonly List<(RectTransform rt, Image img, float speed, float sway, float spin, float phase)> items =
            new List<(RectTransform, Image, float, float, float, float)>();
        private float spawnTimer;

        private void Update()
        {
            var area = (RectTransform)transform;
            float w = area.rect.width, h = area.rect.height;
            bool on = UIMotion.Enabled;

            spawnTimer -= Time.unscaledDeltaTime;
            if (on && spawnTimer <= 0f && items.Count < Max)
            {
                spawnTimer = Random.Range(0.6f, 1.4f);
                float size = Random.Range(36f, 74f);
                var img = UIBuild.Image("Floater", transform, EmojiArt.Get(Emojis[Random.Range(0, Emojis.Length)]),
                    new Color(1f, 1f, 1f, 0f), new Vector2(size, size), new Vector2(Random.Range(-w * 0.48f, w * 0.48f), -h * 0.5f - 60f));
                img.raycastTarget = false;
                items.Add((img.rectTransform, img, Random.Range(28f, 58f), Random.Range(10f, 30f), Random.Range(-20f, 20f), Random.value * 10f));
            }

            for (int i = items.Count - 1; i >= 0; i--)
            {
                var f = items[i];
                if (f.rt == null) { items.RemoveAt(i); continue; }
                Vector2 pos = f.rt.anchoredPosition;
                pos.y += f.speed * Time.unscaledDeltaTime;
                pos.x += Mathf.Sin(Time.unscaledTime * 0.8f + f.phase) * f.sway * Time.unscaledDeltaTime;
                f.rt.anchoredPosition = pos;
                f.rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(Time.unscaledTime * 0.5f + f.phase) * f.spin);

                // Fade in at the bottom, out near the top; never more than faint.
                float k = Mathf.InverseLerp(-h * 0.5f - 60f, h * 0.5f + 60f, pos.y);
                float alpha = Mathf.Min(Mathf.InverseLerp(0f, 0.15f, k), Mathf.InverseLerp(1f, 0.75f, k)) * 0.28f;
                f.img.color = new Color(1f, 1f, 1f, on ? alpha : 0f);
                if (k >= 1f || !on)
                {
                    Destroy(f.rt.gameObject);
                    items.RemoveAt(i);
                }
            }
        }
    }
}
