using System;
using System.Collections.Generic;
using Palace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// Customise screen: tabs for each category, a grid of items with previews, and
    /// Equip / Buy buttons. Coins are earned by finishing games.
    /// </summary>
    public class CustomiseMenu : MonoBehaviour
    {
        private static readonly string[] TabNames = { "Table", "Cards", "Card back", "Banner", "Name style", "Effects", "Shop" };
        private const int ShopTab = 6;
        private bool shopTab;

        private RectTransform root;
        private Action backToMain;
        private GameObject screen;
        private RectTransform grid;
        private TextMeshProUGUI coinsLabel, message;
        private CosmeticCategory category = CosmeticCategory.Table;

        public void Build(RectTransform menuRoot, CardTheme cards, Action onBack)
        {
            root = menuRoot;
            backToMain = onBack;

            screen = UIBuild.Stretch("Customise", root).gameObject;
            var panel = UIKit.Panel(screen.transform, new Vector2(1640f, 980f), Vector2.zero);
            Transform p = panel.transform;
            UIKit.Label(p, "Customise", 54f, new Vector2(800f, 80f), new Vector2(0f, 425f), TextAlignmentOptions.Center, UIKit.Gold, true);
            coinsLabel = UIKit.Label(p, "", 32f, new Vector2(420f, 60f), new Vector2(560f, 425f), TextAlignmentOptions.Right, UIKit.Gold, true);

            UIKit.Segmented(p, TabNames, 0, new Vector2(1540f, 64f), new Vector2(0f, 330f), i =>
            {
                shopTab = i == ShopTab;
                shopNote = null;
                if (!shopTab) category = (CosmeticCategory)i;
                Refresh();
            });
            Shop.Changed += OnShopChanged;
            Shop.Message += OnShopMessage;

            grid = UIBuild.Rect("Items", p, new Vector2(1560f, 640f), new Vector2(0f, -20f));
            message = UIKit.Label(p, "", 26f, new Vector2(1400f, 40f), new Vector2(0f, -370f), TextAlignmentOptions.Center, UIKit.Muted);
            UIKit.Button(p, "Back", new Vector2(220f, 70f), new Vector2(0f, -435f), () =>
            {
                screen.SetActive(false);
                backToMain();
            });
            screen.SetActive(false);
        }

        /// <summary>Open straight on the Shop tab (e.g. from a "get coins" prompt).</summary>
        public void OpenShop()
        {
            screen.SetActive(true);
            shopTab = true;
            shopNote = null;
            Refresh();
        }

        public void Open()
        {
            screen.SetActive(true);
            message.text = "Win games to earn coins: 1st place pays 50, and every finish earns something.";
            Refresh();
        }

        private void Refresh()
        {
            coinsLabel.text = Cosmetics.Coins + " coins";
            foreach (Transform child in grid) Destroy(child.gameObject);
            if (shopTab)
            {
                BuildShop();
                return;
            }

            var lib = CosmeticLibrary.Instance;
            if (lib == null)
            {
                message.text = "Run Palace > Setup > Run Full Setup to create the cosmetics.";
                return;
            }

            IReadOnlyList<CosmeticItem> items = lib.Items(category);
            const int columns = 4;
            for (int i = 0; i < items.Count; i++)
            {
                int col = i % columns, row = i / columns;
                var pos = new Vector2((col - (columns - 1) * 0.5f) * 380f, 160f - row * 320f);
                BuildTile(items[i], pos);
            }
        }

        private void BuildTile(CosmeticItem item, Vector2 pos)
        {
            bool owned = Cosmetics.Owns(item);
            bool equipped = Cosmetics.IsEquipped(category, item);

            var tile = UIKit.Panel(grid, new Vector2(350f, 300f), pos, equipped ? new Color(1f, 0.92f, 0.7f, 1f) : new Color(0.75f, 0.72f, 0.68f, 1f));
            Transform t = tile.transform;
            BuildPreview(item, t);

            UIKit.Label(t, item.displayName, 26f, new Vector2(330f, 40f), new Vector2(0f, -60f), TextAlignmentOptions.Center, UIKit.Text, true);

            if (equipped)
            {
                UIKit.Label(t, "Equipped", 26f, new Vector2(300f, 56f), new Vector2(0f, -112f), TextAlignmentOptions.Center, UIKit.Gold, true);
            }
            else if (owned)
            {
                UIKit.Button(t, "Equip", new Vector2(220f, 60f), new Vector2(0f, -112f), () => Equip(item), true, 26f);
            }
            else if (item.storeOnly)
            {
                UIKit.Label(t, "Supporter Pack (Shop)", 22f, new Vector2(320f, 56f), new Vector2(0f, -112f), TextAlignmentOptions.Center, UIKit.Muted, true);
            }
            else
            {
                bool affordable = Cosmetics.Coins >= item.price;
                var buy = UIKit.Button(t, "Buy  " + item.price, new Vector2(220f, 60f), new Vector2(0f, -112f), () => Buy(item), affordable, 26f);
                if (!affordable) UIKit.SetButtonLabel(buy, item.price + " coins");
            }
        }

        private void BuildPreview(CosmeticItem item, Transform tile)
        {
            var area = new Vector2(300f, 150f);
            var centre = new Vector2(0f, 50f);

            switch (item)
            {
                case DeckSkin deck:
                {
                    // A king from that deck, so the face, ink and court picture all show.
                    var face = UIBuild.Image("Face", tile, deck.face, Color.white, new Vector2(107f, 150f), centre);
                    var icon = deck.king != null ? deck.king : deck.hearts;
                    UIBuild.Image("King", face.transform, icon, deck.red, new Vector2(64f, 64f), new Vector2(0f, 8f)).preserveAspect = true;
                    UIBuild.Text("K", face.transform, "K", 24f, deck.red, new Vector2(30f, 30f), new Vector2(-34f, 55f),
                        TextAlignmentOptions.Center, UIKit.Font, true);
                    break;
                }
                case BannerSkin banner:
                {
                    var plate = UIBuild.Image("Banner", tile, banner.plate, banner.tint, new Vector2(300f, 60f), centre);
                    plate.type = Image.Type.Sliced;
                    UIBuild.Text("Name", plate.transform, GameSettings.PlayerName, 28f, banner.text, new Vector2(280f, 56f), Vector2.zero,
                        TextAlignmentOptions.Center, UIKit.Font, true);
                    break;
                }
                case NameStyle style:
                {
                    var text = UIBuild.Text("Name", tile, GameSettings.PlayerName, 46f, style.face, area, centre,
                        TextAlignmentOptions.Center, UIKit.Font, true);
                    text.outlineWidth = style.outlineWidth;
                    text.outlineColor = style.outline;
                    break;
                }
                default:
                {
                    var img = UIBuild.Image("Preview", tile, item.preview, Color.white, area, centre);
                    img.preserveAspect = true;
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ shop

        private string shopNote;

        private void BuildShop()
        {
            Shop.Initialize();
            var products = ShopCatalog.Products;
            const int columns = 4;
            int slot = 0;

            // Free bonus coins first: watch an optional ad (or claim instantly with Remove Ads).
            BuildBonusTile(SlotPosition(slot++, columns));
            foreach (var product in products) BuildProductTile(product, SlotPosition(slot++, columns));

            string status = Shop.Backend != null && !string.IsNullOrEmpty(Shop.Backend.StatusMessage) ? Shop.Backend.StatusMessage : "";
            message.text = !string.IsNullOrEmpty(shopNote) ? shopNote : status;

            UIKit.Button(grid, "Restore purchases", new Vector2(270f, 52f), new Vector2(-600f, 445f), Shop.Restore, false, 22f);
        }

        private static Vector2 SlotPosition(int i, int columns) =>
            new Vector2((i % columns - (columns - 1) * 0.5f) * 380f, 160f - (i / columns) * 320f);

        private void BuildBonusTile(Vector2 pos)
        {
            var tile = UIKit.Panel(grid, new Vector2(350f, 300f), pos, new Color(1f, 0.95f, 0.8f, 1f));
            Transform t = tile.transform;
            bool adFree = Shop.HasAdFree;
            UIKit.Label(t, "Bonus Coins", 30f, new Vector2(330f, 44f), new Vector2(0f, 95f), TextAlignmentOptions.Center, UIKit.Gold, true);
            var info = UIKit.Label(t, "+" + RewardedAds.Reward + " coins\n" + RewardedAds.RemainingToday + " left today", 26f,
                new Vector2(320f, 90f), new Vector2(0f, 20f), TextAlignmentOptions.Center, UIKit.Text);
            info.textWrappingMode = TextWrappingModes.Normal;

            string label = adFree ? "Claim" : "Watch an ad";
            var button = UIKit.Button(t, label, new Vector2(240f, 60f), new Vector2(0f, -100f), () =>
            {
                RewardedAds.Claim(ok =>
                {
                    if (this == null) return;
                    message.text = ok ? "+" + RewardedAds.Reward + " coins!" : "No bonus available right now.";
                    Refresh();
                });
            }, true, 26f);
            button.interactable = RewardedAds.CanClaim;
        }

        private void BuildProductTile(ShopProduct product, Vector2 pos)
        {
            bool owned = product.Type == ShopProductType.NonConsumable && Shop.IsOwned(product.Id);
            var tile = UIKit.Panel(grid, new Vector2(350f, 300f), pos, new Color(0.75f, 0.72f, 0.68f, 1f));
            Transform t = tile.transform;
            UIKit.Label(t, product.Title, 30f, new Vector2(330f, 44f), new Vector2(0f, 95f), TextAlignmentOptions.Center, UIKit.Gold, true);
            var desc = UIKit.Label(t, product.Description, 24f, new Vector2(310f, 100f), new Vector2(0f, 15f), TextAlignmentOptions.Center, UIKit.Text);
            desc.textWrappingMode = TextWrappingModes.Normal;

            if (owned)
                UIKit.Label(t, "Owned", 28f, new Vector2(300f, 56f), new Vector2(0f, -100f), TextAlignmentOptions.Center, UIKit.Gold, true);
            else
                UIKit.Button(t, Shop.PriceLabel(product), new Vector2(240f, 60f), new Vector2(0f, -100f), () => Shop.Buy(product), true, 28f);
        }

        private void OnShopChanged()
        {
            if (this != null && screen != null && screen.activeSelf) Refresh();
        }

        private void OnShopMessage(string text)
        {
            shopNote = text;
            if (this != null && message != null) message.text = text;
        }

        private void OnDestroy()
        {
            Shop.Changed -= OnShopChanged;
            Shop.Message -= OnShopMessage;
        }

        private void Equip(CosmeticItem item)
        {
            Cosmetics.Equip(category, item);
            AudioManager.Play(Sfx.Select);
            message.text = item.displayName + " equipped.";
            Refresh();
        }

        private void Buy(CosmeticItem item)
        {
            if (!Cosmetics.TryBuy(item))
            {
                message.text = "You need " + (item.price - Cosmetics.Coins) + " more coins for " + item.displayName + ".";
                return;
            }
            AudioManager.Play(Sfx.Win, 0.7f);
            Cosmetics.Equip(category, item);
            message.text = "Unlocked " + item.displayName + "!";
            Refresh();
        }
    }
}
