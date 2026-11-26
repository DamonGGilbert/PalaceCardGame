namespace Palace
{
    public enum ShopProductType { Consumable, NonConsumable }

    public sealed class ShopProduct
    {
        public string Id;             // must match the product ID in Google Play Console and App Store Connect
        public string Title;
        public string Description;
        public ShopProductType Type;
        public string FallbackPrice;  // shown only until the store reports the real local price
        public int Coins;             // coins granted
        public string[] Unlocks = new string[0]; // cosmetic ids granted
        public bool AdFree;           // "Remove Ads": bonus coins without watching an ad
    }

    /// <summary>
    /// Everything for sale for real money. This is the ONE place product IDs live.
    /// Create products with these exact IDs (and types) in both store consoles; prices are set there.
    /// </summary>
    public static class ShopCatalog
    {
        // Kept deliberately simple: three coin bags (bigger bags are better value per pound) and
        // Remove Ads. Everything in the game is bought with coins, so coins are the only thing sold.
        public static readonly ShopProduct[] Products =
        {
            new ShopProduct { Id = "palace.coins.small", Title = "Small Bag of Coins", Description = "2,000 coins",
                Type = ShopProductType.Consumable, FallbackPrice = "£0.99", Coins = 2000 },
            new ShopProduct { Id = "palace.coins.medium", Title = "Medium Bag of Coins", Description = "8,000 coins",
                Type = ShopProductType.Consumable, FallbackPrice = "£2.99", Coins = 8000 },
            new ShopProduct { Id = "palace.coins.big", Title = "Big Bag of Coins", Description = "20,000 coins: best value",
                Type = ShopProductType.Consumable, FallbackPrice = "£5.99", Coins = 20000 },
            new ShopProduct { Id = "palace.removeads", Title = "Remove Ads", Description = "Collect bonus coins without watching ads",
                Type = ShopProductType.NonConsumable, FallbackPrice = "£2.99", AdFree = true }
        };

        /// <summary>
        /// What's on sale right now. Remove Ads is only offered once the game actually shows ads (an
        /// ad network is set up); selling it in a game with no ads would be misleading. Someone who
        /// already owns it keeps it.
        /// </summary>
        public static System.Collections.Generic.List<ShopProduct> Offered()
        {
            var list = new System.Collections.Generic.List<ShopProduct>();
            foreach (var p in Products)
                if (!p.AdFree || RewardedAds.Available || Shop.IsOwned(p.Id)) list.Add(p);
            return list;
        }

        public static ShopProduct Find(string id)
        {
            foreach (var p in Products) if (p.Id == id) return p;
            return null;
        }
    }
}
