using System.Collections.Generic;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Pure layout maths for seating 2-10 players around a poker-style (stadium) table.
    /// No GameObjects here, so it is easy to test and tweak.
    ///
    /// Seat 0 is always the local player at the bottom centre. Higher seat indices
    /// go CLOCKWISE around the table (bottom -> left -> top -> right), matching the
    /// default direction of play.
    /// </summary>
    public static class TableLayout
    {
        public struct Item
        {
            public Vector2 Center;
            public Vector2 Size;      // full size at scale 1
            public bool Scalable;     // true for opponent seats, false for fixed areas (hand, centre pile)

            public Item(Vector2 center, Vector2 size, bool scalable)
            {
                Center = center;
                Size = size;
                Scalable = scalable;
            }
        }

        public static float StadiumPerimeter(Vector2 size)
        {
            float r = size.y * 0.5f;
            float straight = Mathf.Max(0f, size.x - size.y);
            return 2f * straight + 2f * Mathf.PI * r;
        }

        /// <summary>
        /// Point on a stadium (racetrack) outline centred on (0,0).
        /// distance = 0 is bottom centre; increasing distance travels clockwise.
        /// </summary>
        public static Vector2 PointOnStadium(Vector2 size, float distance)
        {
            float r = size.y * 0.5f;
            float straight = Mathf.Max(0f, size.x - size.y);
            float half = straight * 0.5f;
            float arc = Mathf.PI * r;
            float d = Mathf.Repeat(distance, StadiumPerimeter(size));

            if (d < half) return new Vector2(-d, -r);                       // bottom edge, heading left
            d -= half;
            if (d < arc)                                                    // left end, bottom -> top
            {
                float a = 1.5f * Mathf.PI - d / r;
                return new Vector2(-half + r * Mathf.Cos(a), r * Mathf.Sin(a));
            }
            d -= arc;
            if (d < straight) return new Vector2(-half + d, r);             // top edge, heading right
            d -= straight;
            if (d < arc)                                                    // right end, top -> bottom
            {
                float a = 0.5f * Mathf.PI - d / r;
                return new Vector2(half + r * Mathf.Cos(a), r * Mathf.Sin(a));
            }
            d -= arc;
            return new Vector2(half - d, -r);                               // bottom edge, back to centre
        }

        /// <summary>
        /// Positions for every seat. Seat 0 = bottom centre. The opponents are spread
        /// evenly (by distance along the table edge) over the part of the outline that
        /// is not reserved for the local player (localGap).
        /// </summary>
        public static List<Vector2> SeatPositions(int count, Vector2 pathSize, float localGap)
        {
            var result = new List<Vector2>(count);
            if (count <= 0) return result;

            result.Add(PointOnStadium(pathSize, 0f));
            int opponents = count - 1;
            if (opponents == 0) return result;

            float perimeter = StadiumPerimeter(pathSize);
            localGap = Mathf.Clamp(localGap, 0f, perimeter * 0.9f);
            float spacing = (perimeter - localGap) / opponents;

            for (int k = 0; k < opponents; k++)
            {
                float distance = localGap * 0.5f + (k + 0.5f) * spacing;
                result.Add(PointOnStadium(pathSize, distance));
            }

            return result;
        }

        /// <summary>
        /// Returns the largest scale (between minScale and maxScale) at which no two
        /// items overlap and every scalable item stays inside bounds.
        /// Fixed items keep their size; scalable items are all scaled by the same amount.
        /// </summary>
        public static float FitScale(IReadOnlyList<Item> items, Rect bounds, float margin, float minScale, float maxScale)
        {
            float scale = maxScale;

            for (int i = 0; i < items.Count; i++)
            {
                if (!items[i].Scalable) continue;
                Vector2 c = items[i].Center;
                Vector2 h = items[i].Size * 0.5f;
                scale = Mathf.Min(scale, (c.x - bounds.xMin - margin) / h.x);
                scale = Mathf.Min(scale, (bounds.xMax - margin - c.x) / h.x);
                scale = Mathf.Min(scale, (c.y - bounds.yMin - margin) / h.y);
                scale = Mathf.Min(scale, (bounds.yMax - margin - c.y) / h.y);
            }

            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    Item a = items[i];
                    Item b = items[j];
                    if (!a.Scalable && !b.Scalable) continue;

                    // Two boxes do not overlap if they are separated on X OR on Y.
                    float gapX = Mathf.Abs(a.Center.x - b.Center.x) - margin;
                    float gapY = Mathf.Abs(a.Center.y - b.Center.y) - margin;
                    float sx = SolveAxis(gapX, a.Size.x * 0.5f, a.Scalable, b.Size.x * 0.5f, b.Scalable);
                    float sy = SolveAxis(gapY, a.Size.y * 0.5f, a.Scalable, b.Size.y * 0.5f, b.Scalable);
                    scale = Mathf.Min(scale, Mathf.Max(sx, sy));
                }
            }

            return Mathf.Clamp(scale, minScale, maxScale);
        }

        private static float SolveAxis(float gap, float halfA, bool scalableA, float halfB, bool scalableB)
        {
            float fixedPart = (scalableA ? 0f : halfA) + (scalableB ? 0f : halfB);
            float scaledPart = (scalableA ? halfA : 0f) + (scalableB ? halfB : 0f);
            return (gap - fixedPart) / scaledPart;
        }
    }
}
