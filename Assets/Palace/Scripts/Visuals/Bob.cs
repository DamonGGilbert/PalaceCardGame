using UnityEngine;

namespace Palace
{
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
}
