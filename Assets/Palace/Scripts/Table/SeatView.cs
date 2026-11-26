using Palace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// One player's place at the table: name plate with avatar, hand-size badge,
    /// three face-down cards and three face-up cards on top of them.
    ///
    /// Display only. Game logic tells the seat what to show; the seat never decides anything.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SeatView : MonoBehaviour
    {
        public enum PlateSide { Below, Above, Left }

        public const float TableCardScale = 0.85f;   // table cards (was 0.6): bigger for everyone
        public static readonly Vector2 StackedSize = new Vector2(390f, 316f);   // plate above or below the cards
        public static readonly Vector2 SideBySideSize = new Vector2(630f, 264f); // plate to the left of the cards

        // Two rows, like cards laid out on a real table: the face-up row on top and the face-down row
        // below it, shifted down so more than half of each face-down card shows.
        private const float CardStep = 127f;                                     // horizontal distance between table cards
        private const float RowGap = 94f;                                        // vertical distance between the two rows
        private static float CardW => CardView.Width * TableCardScale;           // 119
        private static float CardH => CardView.Height * TableCardScale;          // 167
        private static Vector2 BlockSize => new Vector2(CardStep * 2f + CardW, CardH + RowGap);

        [SerializeField] private TableTheme tableTheme;
        [SerializeField] private CardTheme cardTheme;
        [SerializeField] private CardView cardPrefab;

        [Header("Built automatically - do not assign")]
        [SerializeField] private RectTransform content;
        [SerializeField] private Image turnGlow;
        [SerializeField] private RectTransform cardsBlock;
        [SerializeField] private CardView[] faceDownCards;
        [SerializeField] private CardView[] faceUpCards;
        [SerializeField] private Image plate;
        [SerializeField] private Image avatar;
        [SerializeField] private Image avatarRing;
        [SerializeField] private TextMeshProUGUI avatarInitial;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private RectTransform handBadge;
        [SerializeField] private Image[] handFan;
        [SerializeField] private TextMeshProUGUI handCountText;

        private PlateSide plateSide = PlateSide.Below;
        private bool isTurn;

        public CardView[] FaceDownCards => faceDownCards;
        public CardView[] FaceUpCards => faceUpCards;
        public PlateSide CurrentPlateSide => plateSide;
        public Vector2 Size => plateSide == PlateSide.Left ? SideBySideSize : StackedSize;

        public void Configure(TableTheme table, CardTheme cards, CardView prefab)
        {
            tableTheme = table;
            cardTheme = cards;
            cardPrefab = prefab;
        }

        [SerializeField] private Image placeBadge;
        [SerializeField] private TextMeshProUGUI placeText;

        private void Awake()
        {
            EnsureBuilt();
            LayoutTableCards(); // seats come from a prefab built with the old layout, so lay the cards out again
            SetPlateSide(plateSide);
            if (cardTheme != null)
            {
                foreach (var fan in handFan) fan.sprite = cardTheme.CurrentBack; // card back chosen in Settings
                foreach (var card in stackCards) if (card != null) card.sprite = cardTheme.CurrentBack;
            }
        }

        /// <summary>Shows "1st", "2nd"... once the player has finished. 0 hides it.</summary>
        public void SetFinished(int position)
        {
            EnsureBuilt();
            if (placeBadge == null)
            {
                bool painted = tableTheme != null && tableTheme.buttonPrimary != null;
                placeBadge = UIBuild.Image("PlaceBadge", content, painted ? tableTheme.buttonPrimary : (tableTheme != null ? tableTheme.panel : null),
                    painted ? Color.white : (tableTheme != null ? tableTheme.accent : Color.yellow), new Vector2(120f, 56f), Vector2.zero);
                placeBadge.type = Image.Type.Sliced;
                placeText = UIBuild.Text("Place", placeBadge.transform, "", 32f, new Color(0.15f, 0.1f, 0.02f),
                    new Vector2(120f, 56f), Vector2.zero, TextAlignmentOptions.Center, tableTheme != null ? tableTheme.font : null, true);
            }

            // The place sits on the banner itself, like a tab on its top edge, where the hand count was.
            if (placeBadge.transform.parent != plate.transform)
            {
                placeBadge.transform.SetParent(plate.transform, false);
                var rt = placeBadge.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0f);
                rt.sizeDelta = new Vector2(104f, 44f);
                rt.anchoredPosition = new Vector2(-10f, -6f);
                placeText.rectTransform.sizeDelta = new Vector2(104f, 44f);
                placeText.fontSize = 28f;
                placeText.fontSizeMax = 28f;
            }

            bool finished = position > 0;
            placeBadge.gameObject.SetActive(finished);
            if (finished) placeBadge.transform.SetAsLastSibling();
            if (finished) placeText.text = Ordinal(position);
            if (finished && handBadge != null) handBadge.gameObject.SetActive(false);
            if (finished && handStack != null) handStack.gameObject.SetActive(false);
        }

        public static string Ordinal(int n) => Loc.Ordinal(n);

        // ------------------------------------------------------------------
        // Public display API
        // ------------------------------------------------------------------

        public void SetPlayer(string playerName, int avatarIndex)
        {
            EnsureBuilt();
            nameText.overflowMode = TextOverflowModes.Ellipsis; // long names end in "…" instead of spilling over
            nameText.text = playerName;
            avatarInitial.text = string.IsNullOrEmpty(playerName) ? "?" : playerName.Substring(0, 1).ToUpperInvariant();
            avatar.color = tableTheme != null ? tableTheme.GetAvatarColor(avatarIndex) : Color.gray;
        }

        private Image picture;

        /// <summary>A profile picture (AvatarArt) in the circle instead of the initial; null shows the initial again.</summary>
        public void SetPicture(Sprite sprite)
        {
            EnsureBuilt();
            if (picture == null)
            {
                picture = UIBuild.Image("Picture", avatar.transform, null, Color.white, new Vector2(60f, 60f), Vector2.zero);
                picture.raycastTarget = false;
                picture.preserveAspect = true;
            }
            picture.sprite = sprite;
            picture.gameObject.SetActive(sprite != null);
            avatarInitial.gameObject.SetActive(sprite == null);
        }

        public void SetTurn(bool value)
        {
            EnsureBuilt();
            isTurn = value;
            UIBuild.SetActive(turnGlow, value);
            UIBuild.SetActive(avatarRing, value);
            if (banner != null)
            {
                plate.color = value ? Color.Lerp(banner.tint, tableTheme != null ? tableTheme.panelTurnColor : Color.yellow, 0.5f) : banner.tint;
            }
            else if (tableTheme != null)
            {
                plate.color = value ? tableTheme.panelTurnColor : tableTheme.panelColor;
            }
            if (nameStyle == null && tableTheme != null)
                nameText.color = value ? Color.white : (banner != null ? banner.text : tableTheme.textColor);
        }

        // ------------------------------------------------------------------ cosmetics (your own seat)

        private BannerSkin banner;
        private NameStyle nameStyle;

        /// <summary>Your name banner from the Customise screen.</summary>
        public void ApplyBanner(BannerSkin skin)
        {
            EnsureBuilt();
            banner = skin;
            if (skin == null || skin.plate == null) return;
            plate.sprite = skin.plate;
            plate.type = Image.Type.Sliced;
            nameText.color = skin.text;
            if (skin.textOutline > 0f)
            {
                nameText.outlineWidth = skin.textOutline;
                nameText.outlineColor = new Color32(20, 12, 6, 255);
            }
            SetTurn(isTurn);
        }

        /// <summary>Your name lettering from the Customise screen.</summary>
        private TMPro.TMP_FontAsset defaultNameFont;
        private float defaultNameSize;

        /// <summary>A name font from the Customise screen (null or the default puts the game font back).</summary>
        public void ApplyFont(FontSkin skin)
        {
            EnsureBuilt();
            if (defaultNameFont == null)
            {
                defaultNameFont = nameText.font;
                defaultNameSize = nameText.fontSize;
            }
            bool custom = skin != null && skin.font != null;
            nameText.font = custom ? skin.font : defaultNameFont;
            // An outline (from the banner or name style) gives the text its own copy of the font's
            // material, and that copy still points at the OLD font's letter sheet, which drew the name
            // in garbled shapes. Go back to the new font's own material, then put the outline back.
            if (nameText.font != null) nameText.fontSharedMaterial = nameText.font.material;
            ReapplyOutline();
            float size = defaultNameSize * (custom ? skin.sizeScale : 1f);
            nameText.fontSize = size;
            if (nameText.enableAutoSizing) nameText.fontSizeMax = size;
        }

        /// <summary>The name's outline: the name style's if it has one, otherwise the banner's.</summary>
        private void ReapplyOutline()
        {
            if (nameStyle != null)
            {
                nameText.outlineWidth = nameStyle.outlineWidth;
                nameText.outlineColor = nameStyle.outline;
            }
            else if (banner != null && banner.textOutline > 0f)
            {
                nameText.outlineWidth = banner.textOutline;
                nameText.outlineColor = new Color32(20, 12, 6, 255);
            }
        }

        public void ApplyNameStyle(NameStyle style)
        {
            EnsureBuilt();
            // "Plain" lettering just uses the banner's own text colour.
            nameStyle = style != null && style.outlineWidth > 0f ? style : null;
            if (nameStyle == null) return;
            nameText.color = style.face;
            nameText.outlineWidth = style.outlineWidth;
            nameText.outlineColor = style.outline;
        }

        // ------------------------------------------------------------------
        // Hand size: a fan of card backs that grows with the hand, plus a big count bubble coloured by
        // how close the player is to going out (the most important thing to know near the end):
        //   1 red (pulsing, "LAST CARD")  2 orange  3 yellow  4-7 green  8-14 blue  15+ purple  0 grey
        // ------------------------------------------------------------------

        private const int MaxFanCards = 8;
        private const float BubbleSize = 68f;
        private static readonly Color BubbleNone = new Color(0.42f, 0.44f, 0.46f, 1f);   // 0: on the table cards
        private static readonly Color BubbleLast = new Color(0.86f, 0.12f, 0.12f, 1f);   // 1
        private static readonly Color BubbleTwo = new Color(0.95f, 0.47f, 0.08f, 1f);    // 2
        private static readonly Color BubbleThree = new Color(0.98f, 0.80f, 0.16f, 1f);  // 3
        private static readonly Color BubbleFew = new Color(0.20f, 0.62f, 0.30f, 1f);    // 4-7
        private static readonly Color BubbleBig = new Color(0.18f, 0.42f, 0.82f, 1f);    // 8-14
        private static readonly Color BubbleHuge = new Color(0.50f, 0.24f, 0.72f, 1f);   // 15+

        private static Color BubbleColour(int count) =>
            count <= 0 ? BubbleNone : count == 1 ? BubbleLast : count == 2 ? BubbleTwo : count == 3 ? BubbleThree
            : count <= 7 ? BubbleFew : count <= 14 ? BubbleBig : BubbleHuge;

        private Image countRim, lastGlow;

        /// <summary>The bigger bubble with a dark rim, and the red glow for the last card (made once).</summary>
        private void EnsureCountStyle()
        {
            if (countRim != null) return;
            if (countLabel == null) // seats from the prefab: the label exists but isn't linked
            {
                var found = countBubble.transform.Find("Cards");
                if (found != null) countLabel = found.GetComponent<TextMeshProUGUI>();
            }
            var rt = countBubble.rectTransform;
            rt.sizeDelta = new Vector2(BubbleSize, BubbleSize);
            rt.anchoredPosition = new Vector2(50f, 2f);
            countText.rectTransform.sizeDelta = new Vector2(BubbleSize, BubbleSize);
            countText.fontSize = 40f;
            countText.outlineWidth = 0.2f;
            countText.outlineColor = new Color32(0, 0, 0, 160);
            if (countLabel != null)
            {
                countLabel.fontSize = 19f;
                countLabel.rectTransform.sizeDelta = new Vector2(130f, 24f);
                countLabel.rectTransform.anchoredPosition = new Vector2(0f, -BubbleSize * 0.5f - 12f);
            }

            Sprite circle = tableTheme != null ? tableTheme.circle : null;
            // Red glow behind the bubble that pulses on the last card.
            lastGlow = UIBuild.Image("LastCardGlow", handStack, FxSprites.Dot, new Color(1f, 0.15f, 0.1f, 0.85f),
                new Vector2(BubbleSize * 2f, BubbleSize * 2f), rt.anchoredPosition);
            lastGlow.raycastTarget = false;
            var pulse = lastGlow.gameObject.AddComponent<Pulse>();
            pulse.Amount = 0.25f;
            pulse.Speed = 1.4f;
            // A dark rim so the colour stands out on any table.
            countRim = UIBuild.Image("CountRim", handStack, circle, new Color(0.03f, 0.03f, 0.03f, 0.95f),
                new Vector2(BubbleSize + 8f, BubbleSize + 8f), rt.anchoredPosition);
            countRim.raycastTarget = false;
            int at = countBubble.transform.GetSiblingIndex();
            lastGlow.transform.SetSiblingIndex(at);
            countRim.transform.SetSiblingIndex(at + 1);
            countBubble.transform.SetAsLastSibling();
        }

        [SerializeField] private RectTransform handStack;
        [SerializeField] private System.Collections.Generic.List<Image> stackCards = new System.Collections.Generic.List<Image>();
        [SerializeField] private Image countBubble;
        [SerializeField] private TextMeshProUGUI countText;
        private int shownCount = -1;

        public void SetHandCount(int count)
        {
            EnsureBuilt();
            EnsureHandStack();

            int k = Mathf.Min(count, MaxFanCards);
            float spread = k <= 1 ? 0f : Mathf.Lerp(16f, 84f, (k - 2) / (float)(MaxFanCards - 2));
            for (int i = 0; i < stackCards.Count; i++)
            {
                bool active = i < k;
                UIBuild.SetActive(stackCards[i], active);
                if (!active) continue;
                var rt = stackCards[i].rectTransform;
                float angle = k <= 1 ? 0f : spread * 0.5f - i * spread / (k - 1);
                rt.anchoredPosition = new Vector2(-20f + (i - (k - 1) * 0.5f) * 5f, -22f);
                rt.localEulerAngles = new Vector3(0f, 0f, angle);
            }

            EnsureCountStyle();
            countText.text = count.ToString();
            countBubble.color = BubbleColour(count);
            countText.color = count == 3 ? new Color(0.18f, 0.12f, 0.02f) : Color.white; // dark on yellow, white on the rest
            if (countLabel != null)
            {
                bool last = count == 1 && onLastCard; // the very last card (hand and table together)
                countLabel.text = Loc.T(last ? "LAST CARD" : count == 1 ? "card" : "cards");
                countLabel.color = last ? new Color(1f, 0.55f, 0.5f) : new Color(1f, 1f, 1f, 0.9f);
                countLabel.fontStyle = last ? FontStyles.Bold : FontStyles.Normal;
            }
            UIBuild.SetActive(lastGlow, count == 1 && onLastCard);

            if (shownCount >= 0 && count > shownCount && isActiveAndEnabled) StartCoroutine(PopCount());
            shownCount = count;
        }

        private bool onLastCard;

        /// <summary>True when this player has exactly one card left in total: the bubble says LAST CARD and glows.</summary>
        public void SetLastCard(bool on)
        {
            if (onLastCard == on) return;
            onLastCard = on;
            if (shownCount >= 0) { int c = shownCount; shownCount = -1; SetHandCount(c); }
        }

        public void SetHandBadgeVisible(bool visible)
        {
            EnsureBuilt();
            EnsureHandStack();
            handStack.gameObject.SetActive(visible);
            nameText.rectTransform.offsetMax = new Vector2(visible ? -144f : -14f, 0f);
        }

        private System.Collections.IEnumerator PopCount()
        {
            var rt = countBubble.rectTransform;
            yield return Tween.Run(0.35f, t => rt.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, t));
        }

        private void EnsureHandStack()
        {
            if (handStack != null) return;
            if (handBadge != null) handBadge.gameObject.SetActive(false); // older, smaller design

            handStack = UIBuild.Rect("HandStack", plate.transform, new Vector2(140f, 70f), Vector2.zero);
            SetAnchor(handStack, new Vector2(1f, 0.5f), new Vector2(-70f, 0f));

            Sprite back = cardTheme != null ? cardTheme.CurrentBack : null;
            for (int i = 0; i < MaxFanCards; i++)
            {
                var img = UIBuild.Image("HandCard", handStack, back, Color.white, new Vector2(34f, 48f), Vector2.zero);
                img.rectTransform.pivot = new Vector2(0.5f, 0.1f);
                stackCards.Add(img);
            }

            // A big, plain number: the decorative font's 0 looked like an o.
            var plainFont = TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
            countBubble = UIBuild.Image("CountBubble", handStack, tableTheme != null ? tableTheme.circle : null, BubbleThree,
                new Vector2(52f, 52f), new Vector2(44f, 0f));
            countText = UIBuild.Text("Count", countBubble.transform, "0", 30f, Color.white, new Vector2(52f, 52f), Vector2.zero,
                TextAlignmentOptions.Center, plainFont != null ? plainFont : (tableTheme != null ? tableTheme.font : null), true, false);
            countLabel = UIBuild.Text("Cards", countBubble.transform, "cards", 15f, new Color(1f, 1f, 1f, 0.85f), new Vector2(70f, 20f),
                new Vector2(0f, -36f), TextAlignmentOptions.Center, plainFont != null ? plainFont : (tableTheme != null ? tableTheme.font : null), true);
            countLabel.outlineWidth = 0.25f;
            countLabel.outlineColor = new Color32(0, 0, 0, 200);
        }

        private TextMeshProUGUI countLabel;

        /// <summary>slot 0-2. present = false means that face-down card has been played.</summary>
        public void SetFaceDownSlot(int slot, bool present)
        {
            EnsureBuilt();
            faceDownCards[slot].ShowBack();
            faceDownCards[slot].gameObject.SetActive(present);
        }

        /// <summary>slot 0-2. Pass null when that face-up card has been played.</summary>
        public void SetFaceUpSlot(int slot, Card? card)
        {
            EnsureBuilt();
            faceUpCards[slot].gameObject.SetActive(card.HasValue);
            if (card.HasValue) faceUpCards[slot].ShowCard(card.Value, true);
        }

        public void SetPlateSide(PlateSide side)
        {
            EnsureBuilt();
            plateSide = side;
            var rt = (RectTransform)transform;
            rt.sizeDelta = Size;

            var plateRt = plate.rectTransform;
            switch (side)
            {
                case PlateSide.Below:
                    cardsBlock.anchoredPosition = new Vector2(0f, StackedSize.y * 0.5f - BlockSize.y * 0.5f);
                    plateRt.sizeDelta = new Vector2(StackedSize.x, 44f);
                    plateRt.anchoredPosition = new Vector2(0f, -StackedSize.y * 0.5f + 22f);
                    break;
                case PlateSide.Above:
                    cardsBlock.anchoredPosition = new Vector2(0f, -StackedSize.y * 0.5f + BlockSize.y * 0.5f);
                    plateRt.sizeDelta = new Vector2(StackedSize.x, 44f);
                    plateRt.anchoredPosition = new Vector2(0f, StackedSize.y * 0.5f - 22f);
                    break;
                default:
                    // Name plate on the left, the two rows of cards to its right.
                    cardsBlock.anchoredPosition = new Vector2(SideBySideSize.x * 0.5f - BlockSize.x * 0.5f - 8f, 0f);
                    plateRt.sizeDelta = new Vector2(230f, 44f);
                    plateRt.anchoredPosition = new Vector2(-SideBySideSize.x * 0.5f + 115f, 0f);
                    break;
            }

            turnGlow.rectTransform.sizeDelta = Size + new Vector2(150f, 120f);
        }

        // ------------------------------------------------------------------
        // Building
        // ------------------------------------------------------------------

        public void EnsureBuilt()
        {
            if (content != null) return;

            var rt = (RectTransform)transform;
            rt.sizeDelta = StackedSize;

            content = UIBuild.Stretch("Content", transform);

            Sprite glowSprite = tableTheme != null ? tableTheme.softGlow : null;
            Color glowColor = tableTheme != null ? tableTheme.turnGlow : new Color(1f, 0.8f, 0.3f, 0.5f);
            turnGlow = UIBuild.Image("TurnGlow", content, glowSprite, glowColor, StackedSize + new Vector2(150f, 120f), Vector2.zero);

            cardsBlock = UIBuild.Rect("TableCards", content, BlockSize, Vector2.zero);
            faceDownCards = new CardView[3];
            faceUpCards = new CardView[3];
            for (int i = 0; i < 3; i++) faceDownCards[i] = SpawnCard("FaceDown" + (i + 1), Vector2.zero);
            for (int i = 0; i < 3; i++) faceUpCards[i] = SpawnCard("FaceUp" + (i + 1), Vector2.zero);
            LayoutTableCards();

            BuildPlate();
            SetPlateSide(PlateSide.Below);
            SetTurn(false);
            SetHandCount(3);
        }

        private void BuildPlate()
        {
            Sprite panelSprite = tableTheme != null ? tableTheme.panel : null;
            Sprite circleSprite = tableTheme != null ? tableTheme.circle : null;
            Color textColor = tableTheme != null ? tableTheme.textColor : Color.white;
            TMP_FontAsset font = tableTheme != null ? tableTheme.font : null;

            plate = UIBuild.Image("NamePlate", content, panelSprite, Color.black, new Vector2(StackedSize.x, 44f), Vector2.zero);
            plate.type = Image.Type.Sliced;

            var left = new Vector2(0f, 0.5f);
            var right = new Vector2(1f, 0.5f);

            avatarRing = UIBuild.Image("AvatarRing", plate.transform, tableTheme != null ? tableTheme.ring : null,
                tableTheme != null ? tableTheme.accent : Color.yellow, new Vector2(72f, 72f), Vector2.zero);
            SetAnchor(avatarRing.rectTransform, left, new Vector2(16f, 0f));

            avatar = UIBuild.Image("Avatar", plate.transform, circleSprite, Color.gray, new Vector2(56f, 56f), Vector2.zero);
            SetAnchor(avatar.rectTransform, left, new Vector2(16f, 0f));
            avatarInitial = UIBuild.Text("Initial", avatar.transform, "?", 30f, Color.white, new Vector2(56f, 56f),
                Vector2.zero, TextAlignmentOptions.Center, font, true);

            nameText = UIBuild.Text("Name", plate.transform, "Player", 24f, textColor, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Left, font, true);
            var nameRt = nameText.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(52f, 0f);
            nameRt.offsetMax = new Vector2(-84f, 0f);

            handBadge = UIBuild.Rect("HandBadge", plate.transform, new Vector2(80f, 44f), Vector2.zero);
            SetAnchor(handBadge, right, new Vector2(-42f, 0f));
            handFan = new Image[3];
            Sprite backSprite = cardTheme != null ? cardTheme.CurrentBack : null;
            for (int i = 0; i < 3; i++)
            {
                handFan[i] = UIBuild.Image("HandCard" + (i + 1), handBadge, backSprite, Color.white, new Vector2(24f, 34f),
                    new Vector2(-22f + i * 9f, 0f));
                handFan[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, 12f - i * 12f);
            }
            handCountText = UIBuild.Text("HandCount", handBadge, "3", 24f, textColor, new Vector2(32f, 40f),
                new Vector2(22f, 0f), TextAlignmentOptions.Center, font, true);
        }

        /// <summary>Places the two rows of table cards (face-up row on top, face-down row below).</summary>
        private void LayoutTableCards()
        {
            if (cardsBlock == null) return;
            cardsBlock.sizeDelta = BlockSize;
            for (int i = 0; i < 3; i++)
            {
                Place(faceDownCards[i], new Vector2((i - 1) * CardStep, -RowGap * 0.5f));
                Place(faceUpCards[i], new Vector2((i - 1) * CardStep, RowGap * 0.5f));
            }
            // Face-down cards first, so the face-up row is drawn over the top edge of the row below.
            foreach (var c in faceDownCards) if (c != null) c.transform.SetSiblingIndex(0);
        }

        private static void Place(CardView card, Vector2 position)
        {
            if (card == null) return;
            var rt = (RectTransform)card.transform;
            rt.anchoredPosition = position;
            rt.localScale = Vector3.one * TableCardScale;
        }

        private static void SetAnchor(RectTransform rt, Vector2 anchor, Vector2 position)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = position;
        }

        private CardView SpawnCard(string objectName, Vector2 position)
        {
            CardView card = CardFactory.Spawn(cardPrefab, cardTheme, cardsBlock);
            card.name = objectName;
            var rt = (RectTransform)card.transform;
            rt.anchoredPosition = position;
            rt.localScale = Vector3.one * TableCardScale;
            card.SetInteractable(false);
            card.ShowBack();
            return card;
        }
    }
}
