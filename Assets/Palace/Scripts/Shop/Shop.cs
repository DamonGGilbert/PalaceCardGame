using System;
using System.Linq;
using UnityEngine;

namespace Palace
{
    /// <summary>A real-money store (Unity IAP), or a stand-in for testing.</summary>
    public interface IStoreBackend
    {
        bool IsReady { get; }
        string StatusMessage { get; }
        void Initialize();
        string LocalizedPrice(string productId);   // null until the store reports it
        void Purchase(string productId);
        void Restore();
    }

    /// <summary>
    /// Real-money shop. Delivers what was bought (coins, cosmetics, ad-free bonus) and remembers
    /// non-consumable purchases. The backend calls Grant() when the store reports a pending
    /// purchase; content is granted and SAVED before the backend confirms the purchase, so a
    /// crash at the wrong moment can never lose something a player paid for.
    /// </summary>
    public static class Shop
    {
        public static IStoreBackend Backend { get; private set; }
        public static event Action Changed;
        public static event Action<string> Message;

        public static void Initialize()
        {
            if (Backend != null) return;
#if PALACE_IAP
            Backend = new UnityIapBackend();
#else
            // Without the In App Purchasing package: a free test store in the editor and development
            // builds only. Release builds never hand out paid items for free.
            Backend = Application.isEditor || Debug.isDebugBuild ? (IStoreBackend)new TestStoreBackend() : new UnavailableStoreBackend();
#endif
            Backend.Initialize();
        }

        public static bool IsOwned(string productId) => PlayerPrefs.GetInt("palace.iap.owned." + productId, 0) == 1;

        /// <summary>Remove Ads (or the Supporter Pack) bought: bonus coins without watching ads.</summary>
        public static bool HasAdFree => ShopCatalog.Products.Any(p => p.AdFree && IsOwned(p.Id));

        public static string PriceLabel(ShopProduct product) => Backend?.LocalizedPrice(product.Id) ?? product.FallbackPrice;

        public static void Buy(ShopProduct product)
        {
            Initialize();
            if (product.Type == ShopProductType.NonConsumable && IsOwned(product.Id))
            {
                Report("You already own " + product.Title + ".");
                return;
            }
            Backend.Purchase(product.Id);
        }

        public static void Restore()
        {
            Initialize();
            Backend.Restore();
        }

        /// <summary>
        /// Deliver a purchase. Returns true once it is saved, meaning the backend may confirm it.
        /// Safe to call more than once for the same purchase (restores, re-deliveries).
        /// </summary>
        public static bool Grant(string productId, string transactionId)
        {
            ShopProduct product = ShopCatalog.Find(productId);
            if (product == null)
            {
                Debug.LogWarning("[Palace] Unknown product purchased: " + productId);
                return true; // confirm so it doesn't re-deliver forever
            }

            string txKey = string.IsNullOrEmpty(transactionId) ? null : "palace.iap.tx." + transactionId;
            if (txKey != null && PlayerPrefs.GetInt(txKey, 0) == 1) return true;                 // already delivered
            if (product.Type == ShopProductType.NonConsumable && IsOwned(product.Id))
            {
                if (txKey != null) PlayerPrefs.SetInt(txKey, 1);
                PlayerPrefs.Save();
                return true;
            }

            if (txKey != null) PlayerPrefs.SetInt(txKey, 1);
            if (product.Type == ShopProductType.NonConsumable) PlayerPrefs.SetInt("palace.iap.owned." + product.Id, 1);
            foreach (string unlock in product.Unlocks) PlayerPrefs.SetInt("palace.own." + unlock, 1);
            if (product.Coins > 0) Cosmetics.AddCoins(product.Coins); // saves PlayerPrefs (including everything above)
            PlayerPrefs.Save();

            Report("Thank you! " + product.Title + " has been added.");
            NotifyChanged();
            return true;
        }

        public static void Report(string message) => Message?.Invoke(message);
        public static void NotifyChanged() => Changed?.Invoke();
    }

    /// <summary>Editor / development builds: purchases succeed instantly and cost nothing.</summary>
    public sealed class TestStoreBackend : IStoreBackend
    {
        public bool IsReady => true;
        public string StatusMessage => "Test store: purchases are free (editor / development builds only).";
        public void Initialize() { }
        public string LocalizedPrice(string productId) => null;

        public void Purchase(string productId)
        {
            Shop.Grant(productId, "test-" + Guid.NewGuid().ToString("N"));
        }

        public void Restore() => Shop.Report("Test store: nothing to restore.");
    }

    /// <summary>Release builds without the In App Purchasing package.</summary>
    public sealed class UnavailableStoreBackend : IStoreBackend
    {
        public bool IsReady => false;
        public string StatusMessage => "The shop isn't available in this version.";
        public void Initialize() { }
        public string LocalizedPrice(string productId) => null;
        public void Purchase(string productId) => Shop.Report(StatusMessage);
        public void Restore() => Shop.Report(StatusMessage);
    }
}
