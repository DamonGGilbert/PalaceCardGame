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
        private static readonly string[] TabNames = { "Table", "Cards", "Card back", "Banner", "Name style", "Effects", "Picture", "Shop" };
        private const int PictureTab = 6, ShopTab = 7;
        private bool pictureTab, shownPicture;
        private bool shopTab;

        private RectTransform root;
        private Action backToMain;
        private GameObject screen;
        private RectTransform grid, extras;
        private ScrollRect scroll;
        private GameObject restoreButton;
        private float contentHeight = 640f;
        private CosmeticCategory? shownCategory;
        private bool shownShop;

        private const float Columns = 4, RowHeight = 320f, TopRow = 160f;

        /// <summary>Sizes the scrolling area for this many tiles (and jumps to the top on a new tab).</summary>
        private void LayoutGrid(int count)
        {
            int rows = Mathf.CeilToInt(count / Columns);
            contentHeight = Mathf.Max(640f, rows * RowHeight);
            grid.sizeDelta = new Vector2(1560f, contentHeight);
            bool newTab = shownShop != shopTab || shownPicture != pictureTab || shownCategory != category;
            shownPicture = pictureTab;
            shownShop = shopTab;
            shownCategory = category;
            if (newTab) grid.anchoredPosition = Vector2.zero;
        }

        private Vector2 SlotPosition(int i)
        {
            int col = i % (int)Columns, row = i / (int)Columns;
            return new Vector2((col - (Columns - 1) * 0.5f) * 380f, contentHeight * 0.5f - TopRow - row * RowHeight);
        }
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
                pictureTab = i == PictureTab;
                shopNote = null;
                if (!shopTab && !pictureTab) category = (CosmeticCategory)i;
                Refresh();
            });
            Shop.Changed += OnShopChanged;
            Shop.Message += OnShopMessage;

            // The item grid scrolls (there are lots of tables now); extras like "Restore purchases" sit outside it.
            extras = UIBuild.Rect("Items", p, new Vector2(1560f, 640f), new Vector2(0f, -20f));
            var view = UIBuild.StretchImage("Viewport", extras, null, new Color(0f, 0f, 0f, 0.001f));
            view.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            grid = UIBuild.Rect("Content", view.transform, new Vector2(1560f, 640f), Vector2.zero);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = grid;
            scroll.viewport = view.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;
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
            pictureTab = false;
            shopNote = null;
            Refresh();
        }

        /// <summary>TESTING ONLY (menu screenshots): show a category scrolled to a point (1 = top, 0 = bottom).</summary>
        public void TestShow(CosmeticCategory c, float scrollTo)
        {
            screen.SetActive(true);
            shopTab = false;
            pictureTab = false;
            category = c;
            Refresh();
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = scrollTo;
        }

        public void Open()
        {
            screen.SetActive(true);
            message.text = Loc.T("Win games to earn coins: 1st place pays 50, and every finish earns something.");
            Refresh();
        }

        private void Refresh()
        {
            coinsLabel.text = Loc.F("{0} coins", Cosmetics.Coins);
            foreach (Transform child in grid) Destroy(child.gameObject);
            if (restoreButton != null) Destroy(restoreButton);
            if (shopTab)
            {
                BuildShop();
                return;
            }
            if (pictureTab)
            {
                BuildPictures();
                return;
            }

            var lib = CosmeticLibrary.Instance;
            if (lib == null)
            {
                message.text = Loc.T("Run Palace > Setup > Run Full Setup to create the cosmetics.");
                return;
            }

            IReadOnlyList<CosmeticItem> items = lib.Items(category);
            LayoutGrid(items.Count);
            for (int i = 0; i < items.Count; i++) BuildTile(items[i], SlotPosition(i));
        }

        // ------------------------------------------------------------------ profile pictures

        /// <summary>Your initial, then every picture. Locked ones show the level that unlocks them.</summary>
        private void BuildPictures()
        {
            int count = AvatarArt.Count + 1;
            LayoutGrid(count);
            int chosen = GameSettings.AvatarIfUnlocked;
            int level = Progress.Level;
            for (int i = -1; i < AvatarArt.Count; i++)
            {
                int index = i;
                bool unlocked = i < 0 || AvatarArt.Unlocked(i);
                bool using_ = chosen == i;
                var tile = UIKit.Panel(grid, new Vector2(350f, 300f), SlotPosition(i + 1),
                    using_ ? new Color(1f, 0.92f, 0.7f, 1f) : new Color(0.75f, 0.72f, 0.68f, 1f));
                Transform t = tile.transform;

                if (i < 0)
                {
                    // The default: the first letter of your name on your seat colour.
                    var circle = UIBuild.Image("Initial", t, UIKit.CircleSprite, new Color(0.75f, 0.32f, 0.25f), new Vector2(150f, 150f), new Vector2(0f, 40f));
                    string initial = string.IsNullOrEmpty(GameSettings.PlayerName) ? "?" : GameSettings.PlayerName.Substring(0, 1).ToUpperInvariant();
                    UIBuild.Text("Letter", circle.transform, initial, 80f, Color.white, new Vector2(150f, 150f), Vector2.zero, TextAlignmentOptions.Center, UIKit.Font, true, false);
                }
                else
                {
                    var img = UIBuild.Image("Picture", t, AvatarArt.Get(i), unlocked ? Color.white : new Color(0.35f, 0.35f, 0.35f, 0.9f), new Vector2(150f, 150f), new Vector2(0f, 40f));
                    img.preserveAspect = true;
                }

                string name = i < 0 ? "Your initial" : AvatarArt.Pictures[i].name;
                UIKit.Label(t, name, 26f, new Vector2(330f, 40f), new Vector2(0f, -60f), TextAlignmentOptions.Center, UIKit.Text, true);
                if (using_)
                    UIKit.Label(t, "Equipped", 26f, new Vector2(300f, 56f), new Vector2(0f, -112f), TextAlignmentOptions.Center, UIKit.Gold, true);
                else if (unlocked)
                    UIKit.Button(t, "Equip", new Vector2(220f, 60f), new Vector2(0f, -112f), () =>
                    {
                        GameSettings.Avatar = index;
                        GameSettings.Save();
                        AudioManager.Play(Sfx.Select, 0.7f);
                        Refresh();
                    }, true, 26f);
                else
                    UIKit.Label(t, Loc.F("Unlocks at level {0}", AvatarArt.Pictures[i].level), 24f, new Vector2(320f, 56f), new Vector2(0f, -112f),
                        TextAlignmentOptions.Center, UIKit.Muted, true);
            }
            message.text = Loc.F("You're level {0}. Play games to level up and unlock more pictures.", level);
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
                var buy = UIKit.Button(t, Loc.F("Buy  {0}", item.price), new Vector2(220f, 60f), new Vector2(0f, -112f), () => Buy(item), affordable, 26f);
                if (!affordable) UIKit.SetButtonLabel(buy, Loc.F("{0} coins", item.price));
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
                        TextAlignmentOptions.Center, UIKit.Font, true, false);
                    break;
                }
                case BannerSkin banner:
                {
                    var plate = UIBuild.Image("Banner", tile, banner.plate, banner.tint, new Vector2(300f, 60f), centre);
                    plate.type = Image.Type.Sliced;
                    var name = UIBuild.Text("Name", plate.transform, GameSettings.PlayerName, 28f, banner.text, new Vector2(280f, 56f), Vector2.zero,
                        TextAlignmentOptions.Center, UIKit.Font, true, false);
                    if (banner.textOutline > 0f)
                    {
                        name.outlineWidth = banner.textOutline;
                        name.outlineColor = new Color32(20, 12, 6, 255);
                    }
                    break;
                }
                case NameStyle style:
                {
                    var text = UIBuild.Text("Name", tile, GameSettings.PlayerName, 46f, style.face, area, centre,
                        TextAlignmentOptions.Center, UIKit.Font, true, false);
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
            LayoutGrid(products.Length + 1);
            int slot = 0;

            // Free bonus coins first: watch an optional ad (or claim instantly with Remove Ads).
            BuildBonusTile(SlotPosition(slot++));
            foreach (var product in products) BuildProductTile(product, SlotPosition(slot++));

            string status = Shop.Backend != null && !string.IsNullOrEmpty(Shop.Backend.StatusMessage) ? Shop.Backend.StatusMessage : "";
            message.text = Loc.T(!string.IsNullOrEmpty(shopNote) ? shopNote : status);

            restoreButton = UIKit.Button(extras, "Restore purchases", new Vector2(270f, 52f), new Vector2(-600f, 445f), Shop.Restore, false, 22f).gameObject;
        }

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
                    message.text = ok ? Loc.F("+{0} coins!", RewardedAds.Reward) : Loc.T("No bonus available right now.");
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
            if (this != null && message != null) message.text = Loc.T(text);
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
            message.text = Loc.F("{0} equipped.", Loc.T(item.displayName));
            Refresh();
        }

        private void Buy(CosmeticItem item)
        {
            if (!Cosmetics.TryBuy(item))
            {
                message.text = Loc.F("You need {0} more coins for {1}.", item.price - Cosmetics.Coins, Loc.T(item.displayName));
                return;
            }
            AudioManager.Play(Sfx.Win, 0.7f);
            Cosmetics.Equip(category, item);
            message.text = Loc.F("Unlocked {0}!", Loc.T(item.displayName));
            Refresh();
        }
    }
}
