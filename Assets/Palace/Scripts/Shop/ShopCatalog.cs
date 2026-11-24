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
        public static readonly ShopProduct[] Products =
        {
            new ShopProduct { Id = "palace.coins.500", Title = "Pouch of Coins", Description = "6,000 coins",
                Type = ShopProductType.Consumable, FallbackPrice = "£0.99", Coins = 6000 },
            new ShopProduct { Id = "palace.coins.1500", Title = "Bag of Coins", Description = "18,000 coins",
                Type = ShopProductType.Consumable, FallbackPrice = "£2.49", Coins = 18000 },
            new ShopProduct { Id = "palace.coins.4000", Title = "Chest of Coins", Description = "50,000 coins: best value",
                Type = ShopProductType.Consumable, FallbackPrice = "£4.99", Coins = 50000 },
            new ShopProduct { Id = "palace.bundle.water", Title = "Water Pack", Description = "Lagoon table + Water effects",
                Type = ShopProductType.NonConsumable, FallbackPrice = "£1.99", Unlocks = new[] { "table_lagoon", "fx_water" } },
            new ShopProduct { Id = "palace.bundle.midnight", Title = "Midnight Pack", Description = "Midnight Gold deck, back and table",
                Type = ShopProductType.NonConsumable, FallbackPrice = "£1.99", Unlocks = new[] { "deck_midnight", "back_midnight", "table_navy" } },
            new ShopProduct { Id = "palace.removeads", Title = "Remove Ads", Description = "Collect bonus coins without watching ads",
                Type = ShopProductType.NonConsumable, FallbackPrice = "£2.99", AdFree = true },
            new ShopProduct { Id = "palace.supporter", Title = "Supporter Pack", Description = "60,000 coins + exclusive Supporter banner and Royal name",
                Type = ShopProductType.NonConsumable, FallbackPrice = "£6.99", Coins = 60000, AdFree = true,
                Unlocks = new[] { "banner_supporter", "name_royal" } }
        };

        public static ShopProduct Find(string id)
        {
            foreach (var p in Products) if (p.Id == id) return p;
            return null;
        }
    }
}
