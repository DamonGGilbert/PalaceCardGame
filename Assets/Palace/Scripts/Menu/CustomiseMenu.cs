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
        private static readonly string[] TabNames = { "Table", "Cards", "Card back", "Banner", "Name style", "Effects" };

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

            UIKit.Segmented(p, TabNames, 0, new Vector2(1500f, 64f), new Vector2(0f, 330f), i =>
            {
                category = (CosmeticCategory)i;
                Refresh();
            });

            grid = UIBuild.Rect("Items", p, new Vector2(1560f, 640f), new Vector2(0f, -20f));
            message = UIKit.Label(p, "", 26f, new Vector2(1400f, 40f), new Vector2(0f, -370f), TextAlignmentOptions.Center, UIKit.Muted);
            UIKit.Button(p, "Back", new Vector2(220f, 70f), new Vector2(0f, -435f), () =>
            {
                screen.SetActive(false);
                backToMain();
            });
            screen.SetActive(false);
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
