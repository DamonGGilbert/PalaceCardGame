using System.Collections.Generic;
using Palace.Core;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Traditional court card pictures (Jack, Queen, King of each suit) for the Classic card faces.
    /// Resources/Courts/court_{jack|queen|king}_{suit}.png, cropped to the picture frame.
    ///
    /// Source: Byron Knoll's vector playing cards (released into the public domain; listed as CC0 at
    /// opengameart.org/content/playing-cards-vector-png), traced from 19th-century court designs.
    /// Only the 12 court pictures are used; every other part of the cards is drawn by the game.
    /// Loaded the first time each one is needed.
    /// </summary>
    public static class CourtArt
    {
        private static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();

        public static Sprite Get(Rank rank, Suit suit)
        {
            if (rank != Rank.Jack && rank != Rank.Queen && rank != Rank.King) return null;
            int key = (int)rank * 10 + (int)suit;
            if (cache.TryGetValue(key, out var sprite)) return sprite;

            string path = "Courts/court_" + rank.ToString().ToLowerInvariant() + "_" + suit.ToString().ToLowerInvariant();
            sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null) sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            cache[key] = sprite; // remembers a missing picture too, so it isn't looked up again
            return sprite;
        }
    }
}
