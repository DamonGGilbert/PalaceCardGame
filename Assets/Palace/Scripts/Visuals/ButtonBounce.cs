using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Palace
{
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
}
