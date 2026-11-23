#if PALACE_IAP
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Palace
{
    /// <summary>
    /// Real purchases through Unity IAP 5 (Google Play and the App Store). In the Unity editor
    /// Unity IAP shows its own fake store, so purchases can be tested without spending money.
    ///
    /// Flow: PurchaseProduct -> OnPurchasePending -> Shop.Grant (saves) -> ConfirmPurchase.
    /// Compiled only when the In App Purchasing package is installed (PALACE_IAP is added
    /// automatically by Palace/Editor/PalaceStoreDefines.cs).
    /// </summary>
    public sealed class UnityIapBackend : IStoreBackend
    {
        private StoreController store;
        private bool ready;
        private string status = "Connecting to the store...";

        public bool IsReady => ready;
        public string StatusMessage => status;

        public async void Initialize()
        {
            store = UnityIAPServices.StoreController();

            // Subscribe to everything BEFORE Connect: unfinished purchases may arrive straight away.
            store.OnPurchasePending += OnPurchasePending;
            store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            store.OnPurchaseFailed += failed => Shop.Report("Purchase didn't go through (" + failed.FailureReason + ").");
            store.OnPurchaseDeferred += deferred => Shop.Report("Your purchase is waiting for approval.");
            store.OnStoreConnected += OnStoreConnected;
            store.OnStoreDisconnected += failure =>
            {
                ready = false;
                status = "The store is unavailable right now.";
                Debug.LogWarning("[Palace] Store disconnected: " + failure.Message);
                Shop.NotifyChanged();
            };
            store.OnProductsFetched += products =>
            {
                ready = true;
                status = "";
                Shop.NotifyChanged();
            };
            store.OnProductsFetchFailed += failure =>
            {
                status = "Couldn't load the shop.";
                Debug.LogWarning("[Palace] Product fetch failed: " + failure.FailureReason);
                Shop.NotifyChanged();
            };
            store.OnPurchasesFetched += orders =>
            {
                // Re-apply non-consumables bought earlier (e.g. on a reinstall or a new device).
                foreach (var order in orders.ConfirmedOrders)
                    foreach (var item in order.CartOrdered.Items())
                        if (item.Product.definition.type == ProductType.NonConsumable)
                            Shop.Grant(item.Product.definition.id, null);
            };
            store.OnPurchasesFetchFailed += failure => Debug.LogWarning("[Palace] Purchases fetch failed: " + failure.Message);

            try
            {
                await store.Connect();
            }
            catch (Exception e)
            {
                status = "The store is unavailable right now.";
                Debug.LogWarning("[Palace] Store connect failed: " + e.Message);
                Shop.NotifyChanged();
            }
        }

        private void OnStoreConnected()
        {
            var definitions = ShopCatalog.Products
                .Select(p => new ProductDefinition(p.Id, p.Type == ShopProductType.Consumable ? ProductType.Consumable : ProductType.NonConsumable))
                .ToList();
            store.FetchProducts(definitions);
            store.FetchPurchases();
        }

        private void OnPurchasePending(PendingOrder order)
        {
            // Grant + save first; only then confirm. If saving failed, leaving it unconfirmed
            // means the store delivers it again next launch.
            foreach (var item in order.CartOrdered.Items())
                if (!Shop.Grant(item.Product.definition.id, order.Info.TransactionID)) return;
            store.ConfirmPurchase(order);
        }

        private static void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed)
                Debug.LogWarning("[Palace] Purchase confirmation failed: " + failed.FailureReason + " " + failed.Details);
        }

        public string LocalizedPrice(string productId)
        {
            if (!ready || store == null) return null;
            return store.GetProductById(productId)?.metadata.localizedPriceString;
        }

        public void Purchase(string productId)
        {
            if (!ready)
            {
                Shop.Report(status);
                return;
            }
            Product product = store.GetProductById(productId);
            if (product == null || !product.availableToPurchase)
            {
                Shop.Report("That item isn't available right now.");
                return;
            }
            store.PurchaseProduct(product);
        }

        public void Restore()
        {
            if (store == null) return;
            store.RestoreTransactions((ok, error) => Shop.Report(ok ? "Purchases restored." : "Couldn't restore purchases: " + error));
        }
    }
}
#endif
