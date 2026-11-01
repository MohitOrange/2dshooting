using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace NeonBrawl
{
    /// <summary>
    /// Real-money purchases through Unity IAP 5 (Google Play on Android; the editor uses Unity's fake store).
    /// Gem packs are consumables, Remove Ads is a one-time purchase that comes back after a reinstall.
    /// Every purchase is granted and saved first, then confirmed with the store; a purchase that was
    /// interrupted is delivered again on the next launch, and transaction IDs stop double grants.
    /// </summary>
    public sealed class IapStore : IStoreService
    {
        public const string RemoveAds = "remove_ads";
        public const int RemoveAdsGems = 50;

        /// <summary>Product IDs (create the same IDs in the Play Console) and the gems each one gives.</summary>
        public static readonly string[] GemPackIds = { "gems_80", "gems_500", "gems_1200", "gems_2500" };
        public static readonly int[] GemPackAmounts = { 80, 500, 1200, 2500 };

        private readonly GameContext _ctx;
        private StoreController _store;
        private bool _fetched, _failed;
        private readonly Dictionary<string, Action<bool>> _waiting = new Dictionary<string, Action<bool>>();

        public event Action Changed;

        public IapStore(GameContext ctx) { _ctx = ctx; }

        public bool Ready => _fetched && _store != null;

        public async void Init()
        {
            // Only Google Play / App Store builds sell anything. Elsewhere Unity IAP would fall back to its
            // fake store, which "sells" gems for free, so Windows test builds keep the store closed.
            bool realStore = Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;
            if (!realStore && !Application.isEditor)
            {
                _failed = true;
                return;
            }
            try
            {
                _store = UnityIAPServices.StoreController();
                // every event is hooked up before Connect: a purchase left over from last time can arrive at once
                _store.OnStoreConnected += OnConnected;
                _store.OnStoreDisconnected += OnDisconnected;
                _store.OnProductsFetched += OnProductsFetched;
                _store.OnProductsFetchFailed += OnProductsFetchFailed;
                _store.OnPurchasesFetched += OnPurchasesFetched;
                _store.OnPurchasesFetchFailed += f => Debug.LogWarning("[Store] purchases fetch failed: " + f.Message);
                _store.OnPurchasePending += OnPurchasePending;
                _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                _store.OnPurchaseFailed += OnPurchaseFailed;
                _store.OnPurchaseDeferred += OnPurchaseDeferred;
                await _store.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Store] not available: " + e.Message);
                _failed = true;
                Changed?.Invoke();
            }
        }

        private static List<ProductDefinition> Catalog()
        {
            var list = new List<ProductDefinition>(GemPackIds.Length + 1);
            for (int i = 0; i < GemPackIds.Length; i++) list.Add(new ProductDefinition(GemPackIds[i], ProductType.Consumable));
            list.Add(new ProductDefinition(RemoveAds, ProductType.NonConsumable));
            return list;
        }

        private void OnConnected()
        {
            _store.FetchProducts(Catalog());
        }

        private void OnDisconnected(StoreConnectionFailureDescription f)
        {
            Debug.LogWarning("[Store] disconnected: " + f.Message);
            _failed = !_fetched;
            Changed?.Invoke();
        }

        private void OnProductsFetched(List<Product> products)
        {
            _fetched = true;
            _failed = false;
            Debug.Log("[Store] " + products.Count + " products ready");
            _store.FetchPurchases(); // restores Remove Ads, re-delivers unfinished purchases
            Changed?.Invoke();
        }

        private void OnProductsFetchFailed(ProductFetchFailed f)
        {
            Debug.LogWarning("[Store] product fetch failed: " + f.FailureReason);
            _failed = true;
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- shop API

        public string PriceOf(string productId)
        {
            if (productId == RemoveAds && _ctx.Profile.Data.NoAds) return "OWNED";
            var p = _store != null && _fetched ? _store.GetProductById(productId) : null;
            if (p != null && p.availableToPurchase && !string.IsNullOrEmpty(p.metadata.localizedPriceString)) return p.metadata.localizedPriceString;
            return _failed || (_fetched && p == null) ? "N/A" : "…";
        }

        public void Buy(string productId, Action<bool> done)
        {
            var p = Ready ? _store.GetProductById(productId) : null;
            if (p == null || !p.availableToPurchase)
            {
                _ctx.UI.Toast(Ready ? "This item isn't available right now" : "The store isn't available right now");
                done?.Invoke(false);
                return;
            }
            if (productId == RemoveAds && _ctx.Profile.Data.NoAds)
            {
                _ctx.UI.Toast("Ads are already removed");
                done?.Invoke(false);
                return;
            }
            _waiting[productId] = done;
            _store.PurchaseProduct(p);
        }

        // ---------------------------------------------------------------- purchase flow

        private void OnPurchasePending(PendingOrder order)
        {
            string tx = order.Info != null ? order.Info.TransactionID : null;
            var data = _ctx.Profile.Data;
            if (data.Orders == null) data.Orders = new List<string>();
            bool already = !string.IsNullOrEmpty(tx) && data.Orders.Contains(tx);
            var granted = new List<string>(2);
            foreach (var item in order.CartOrdered.Items())
            {
                string id = item.Product != null ? item.Product.definition.id : null;
                if (id == null) continue;
                if (!already) Grant(id, Mathf.Max(1, item.Quantity));
                granted.Add(id);
            }
            if (!already && !string.IsNullOrEmpty(tx))
            {
                data.Orders.Add(tx);
                if (data.Orders.Count > 200) data.Orders.RemoveAt(0);
            }
            _ctx.Profile.Save(); // saved before the store is told the purchase is done
            _store.ConfirmPurchase(order);
            foreach (var id in granted) Finish(id, true);
            Changed?.Invoke();
        }

        private void Grant(string id, int quantity)
        {
            var data = _ctx.Profile.Data;
            if (id == RemoveAds)
            {
                if (!data.NoAds) data.Gems += RemoveAdsGems;
                data.NoAds = true;
                return;
            }
            int i = Array.IndexOf(GemPackIds, id);
            if (i >= 0) data.Gems += GemPackAmounts[i] * quantity;
        }

        private void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed) Debug.LogWarning("[Store] confirm failed: " + failed.FailureReason + " " + failed.Details);
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            string id = FirstId(order);
            Debug.Log("[Store] purchase failed: " + order.FailureReason + " " + order.Details);
            if (order.FailureReason != PurchaseFailureReason.UserCancelled)
                _ctx.UI.Toast(order.FailureReason == PurchaseFailureReason.ExistingPurchasePending ? "A purchase is still being processed" : "Purchase failed. You were not charged.");
            if (id != null) Finish(id, false);
        }

        private void OnPurchaseDeferred(DeferredOrder order)
        {
            _ctx.UI.Toast("Waiting for the payment to be approved");
            string id = FirstId(order);
            if (id != null) Finish(id, false);
        }

        private void OnPurchasesFetched(Orders orders)
        {
            // Remove Ads survives reinstalls: the store still lists it as bought
            foreach (var o in orders.ConfirmedOrders)
                foreach (var item in o.CartOrdered.Items())
                    if (item.Product != null && item.Product.definition.id == RemoveAds && !_ctx.Profile.Data.NoAds)
                    {
                        _ctx.Profile.Data.NoAds = true;
                        _ctx.Profile.Save();
                        Changed?.Invoke();
                    }
        }

        private static string FirstId(Order order)
        {
            var items = order?.CartOrdered?.Items();
            return items != null && items.Count > 0 && items[0].Product != null ? items[0].Product.definition.id : null;
        }

        private void Finish(string id, bool ok)
        {
            if (!_waiting.TryGetValue(id, out var done)) return;
            _waiting.Remove(id);
            done?.Invoke(ok);
        }
    }
}
