using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// The player's coins, unlocked cosmetics and what is equipped, saved on the device.
    /// Coins are earned by finishing games; items are bought with coins (and, in step 3,
    /// can also be granted by real-money purchases through Grant()).
    /// </summary>
    public static class Cosmetics
    {
        public static event Action Changed;

        private static CosmeticLibrary Library => CosmeticLibrary.Instance;

        // ------------------------------------------------------------------ coins

        public static int Coins
        {
            get => PlayerPrefs.GetInt("palace.coins", 0);
            private set
            {
                PlayerPrefs.SetInt("palace.coins", Mathf.Max(0, value));
                PlayerPrefs.Save();
                CloudProgress.MarkLocalChange();
                Changed?.Invoke();
            }
        }

        public static void AddCoins(int amount) => Coins += amount;

        /// <summary>Cloud restore only: set coins without marking a new local change.</summary>
        public static void SetCoinsFromCloud(int coins) => PlayerPrefs.SetInt("palace.coins", Mathf.Max(0, coins));

        public static void NotifyChanged() => Changed?.Invoke();

        /// <summary>Coins for a finishing place: winning pays best, but every finish earns something.</summary>
        public static int RewardFor(int position, int players)
        {
            if (position == 1) return 50;
            if (position == players) return 10;
            return Mathf.Max(15, 40 - (position - 2) * 5);
        }

        // ------------------------------------------------------------------ prices

        /// <summary>
        /// Every coin price in the catalogue (Generate Art) is multiplied by this, so all of them can be
        /// raised or lowered in one place without regenerating anything. 10 = a flag table costs 2,500 coins.
        /// </summary>
        public const int PriceMultiplier = 10;

        public static int PriceOf(CosmeticItem item) => item == null ? 0 : item.price * PriceMultiplier;

        /// <summary>A coin amount written for people, e.g. 2,500.</summary>
        public static string Amount(int coins) => coins.ToString("N0");

        // ------------------------------------------------------------------ owning and equipping

        public static bool Owns(CosmeticItem item) =>
            item != null && ((item.price <= 0 && !item.storeOnly) || PlayerPrefs.GetInt("palace.own." + item.id, 0) == 1);

        /// <summary>Buy with coins. Returns false if there aren't enough (or it's a shop-only item).</summary>
        public static bool TryBuy(CosmeticItem item)
        {
            if (Owns(item)) return true;
            if (item.storeOnly) return false;
            if (Coins < PriceOf(item)) return false;
            PlayerPrefs.SetInt("palace.own." + item.id, 1);
            Coins -= PriceOf(item); // also saves and notifies
            return true;
        }

        /// <summary>Unlock without coins (e.g. after a real-money purchase).</summary>
        public static void Grant(string itemId)
        {
            PlayerPrefs.SetInt("palace.own." + itemId, 1);
            PlayerPrefs.Save();
            CloudProgress.MarkLocalChange();
            Changed?.Invoke();
        }

        public static string EquippedId(CosmeticCategory category) => PlayerPrefs.GetString("palace.equip." + category, "");

        public static void Equip(CosmeticCategory category, CosmeticItem item)
        {
            if (!Owns(item)) return;
            PlayerPrefs.SetString("palace.equip." + category, item.id);
            PlayerPrefs.Save();
            CloudProgress.MarkLocalChange();
            Changed?.Invoke();
        }

        private static T Equipped<T>(CosmeticCategory category, List<T> items) where T : CosmeticItem
        {
            if (items == null || items.Count == 0) return null;
            string id = EquippedId(category);
            return items.FirstOrDefault(i => i.id == id && Owns(i)) ?? items[0];
        }

        public static TableSkin Table => Library != null ? Equipped(CosmeticCategory.Table, Library.tables) : null;
        public static DeckSkin Deck => Library != null ? Equipped(CosmeticCategory.Deck, Library.decks) : null;
        public static BackSkin Back => Library != null ? Equipped(CosmeticCategory.CardBack, Library.backs) : null;
        public static BannerSkin Banner => Library != null ? Equipped(CosmeticCategory.Banner, Library.banners) : null;
        public static NameStyle NameStyle => Library != null ? Equipped(CosmeticCategory.NameStyle, Library.nameStyles) : null;
        public static EffectSkin Effect => Library != null ? Equipped(CosmeticCategory.Effects, Library.effects) : null;
        public static FxStyle EffectStyle => Effect != null ? Effect.style : FxStyle.Classic;
        public static FontSkin Font => Library != null ? Equipped(CosmeticCategory.Font, Library.fonts) : null;
        public static CelebrationSkin Celebration => Library != null ? Equipped(CosmeticCategory.Celebration, Library.celebrations) : null;

        /// <summary>The id of what you have equipped in a category (owned, or the free default), for sending online.</summary>
        public static string WornId(CosmeticCategory category)
        {
            var list = Library != null ? Library.Items(category) : null;
            if (list == null || list.Count == 0) return "";
            string id = EquippedId(category);
            CosmeticItem current = list.FirstOrDefault(i => i.id == id && Owns(i)) ?? list[0];
            return current.id;
        }

        public static bool IsEquipped(CosmeticCategory category, CosmeticItem item)
        {
            var list = Library != null ? Library.Items(category) : null;
            if (list == null || list.Count == 0) return false;
            string id = EquippedId(category);
            CosmeticItem current = list.FirstOrDefault(i => i.id == id && Owns(i)) ?? list[0];
            return current == item;
        }

        /// <summary>TESTING ONLY (command line -palace-showcase): unlock and wear the premium items.</summary>
        public static void ShowcaseForTesting()
        {
            var lib = Library;
            if (lib == null) return;
            void Wear(CosmeticCategory c, IEnumerable<CosmeticItem> items, string id)
            {
                foreach (var i in items) PlayerPrefs.SetInt("palace.own." + i.id, 1);
                var pick = items.FirstOrDefault(i => i.id == id);
                if (pick != null) PlayerPrefs.SetString("palace.equip." + c, pick.id);
            }
            Wear(CosmeticCategory.Table, lib.tables, "table_lagoon");
            Wear(CosmeticCategory.Deck, lib.decks, "deck_midnight");
            Wear(CosmeticCategory.CardBack, lib.backs, "back_emerald");
            Wear(CosmeticCategory.Banner, lib.banners, "banner_leather");
            Wear(CosmeticCategory.NameStyle, lib.nameStyles, "name_neon");
            Wear(CosmeticCategory.Effects, lib.effects, "fx_water");
            Changed?.Invoke();
        }

        /// <summary>Points the shared themes at the equipped table, deck and card back.</summary>
        public static void ApplyThemes(TableTheme table, CardTheme cards)
        {
            TableSkin equipped = Table;
            if (table != null && table.tableSkin != equipped && Library != null)
            {
                // Switching table: let go of every other big table image so it can leave memory.
                foreach (var t in Library.tables) if (t != equipped) t.Release();
                Resources.UnloadUnusedAssets();
            }
            if (table != null) table.tableSkin = equipped;
            if (cards != null)
            {
                cards.deckSkin = Deck;
                cards.backSkin = Back;
            }
        }
    }
}
