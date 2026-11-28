using UnityEngine;

namespace Palace
{
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
}
