using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// A loop of arrows drifting around the felt in the direction of play (like Uno).
    /// When a Queen reverses play, the arrows flash, whizz round and turn the other way.
    /// </summary>
    public class DirectionRing : MonoBehaviour
    {
        private const int ArrowCount = 16;
        private static readonly Vector2 PathSize = new Vector2(1120f, 470f);
        private Color BaseColor = new Color(0.95f, 0.78f, 0.35f, 0.30f);
        private Color FlashColor = new Color(0.85f, 0.70f, 1f, 0.95f);

        /// <summary>Match the arrows to the effect theme (gold normally, pale blue for water).</summary>
        public void SetStyle(FxStyle style)
        {
            BaseColor = style == FxStyle.Water ? new Color(0.7f, 0.92f, 1f, 0.32f) : new Color(0.95f, 0.78f, 0.35f, 0.30f);
            FlashColor = style == FxStyle.Water ? new Color(0.85f, 1f, 1f, 0.95f) : new Color(0.85f, 0.70f, 1f, 0.95f);
            Layout();
        }

        private readonly List<Image> arrows = new List<Image>();
        private Vector2 centre;
        private int direction = 1;
        private float offset;
        private float boost;   // extra speed right after a reverse
        private float flash;   // highlight right after a reverse

        public static DirectionRing Create(RectTransform table, Vector2 centre, int siblingIndex)
        {
            var rt = UIBuild.Stretch("DirectionArrows", table);
            rt.SetSiblingIndex(siblingIndex);
            var ring = rt.gameObject.AddComponent<DirectionRing>();
            ring.centre = centre;
            for (int i = 0; i < ArrowCount; i++)
                ring.arrows.Add(UIBuild.Image("Arrow", rt, FxSprites.Chevron, ring.BaseColor, new Vector2(44f, 44f), Vector2.zero));
            ring.Layout();
            return ring;
        }

        /// <summary>+1 clockwise, -1 anticlockwise. animate = do the "reverse" flourish.</summary>
        public void SetDirection(int newDirection, bool animate)
        {
            if (newDirection == direction) return;
            direction = newDirection;
            if (animate && GameSettings.AnimationScale > 0f)
            {
                boost = 1f;
                flash = 1f;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            boost = Mathf.MoveTowards(boost, 0f, dt * 1.1f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 1.3f);
            offset += direction * 40f * (1f + boost * boost * 14f) * dt;
            Layout();
        }

        private void Layout()
        {
            float perimeter = TableLayout.StadiumPerimeter(PathSize);
            Color color = Color.Lerp(BaseColor, FlashColor, flash);
            float scale = 1f + flash * 0.45f;

            for (int i = 0; i < arrows.Count; i++)
            {
                float d = offset + i * perimeter / arrows.Count;
                Vector2 p = TableLayout.PointOnStadium(PathSize, d);
                Vector2 ahead = TableLayout.PointOnStadium(PathSize, d + direction * 4f);
                float angle = Mathf.Atan2(ahead.y - p.y, ahead.x - p.x) * Mathf.Rad2Deg;

                RectTransform rt = arrows[i].rectTransform;
                rt.anchoredPosition = centre + p;
                rt.localEulerAngles = new Vector3(0f, 0f, angle);
                rt.localScale = Vector3.one * scale;
                arrows[i].color = color;
            }
        }
    }
}
