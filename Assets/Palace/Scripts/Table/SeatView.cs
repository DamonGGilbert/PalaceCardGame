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

        public const float TableCardScale = 0.6f;
        public static readonly Vector2 StackedSize = new Vector2(300f, 196f);   // plate above or below the cards
        public static readonly Vector2 SideBySideSize = new Vector2(530f, 144f); // plate to the left of the cards

        private const float CardStep = 92f;          // horizontal distance between table cards
        private const float FaceUpOffset = 13.2f;    // face-up cards sit slightly above the face-down ones

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
            if (cardTheme != null)
                foreach (var fan in handFan) fan.sprite = cardTheme.CurrentBack; // card back chosen in Settings
        }

        /// <summary>Shows "1st", "2nd"... once the player has finished. 0 hides it.</summary>
        public void SetFinished(int position)
        {
            EnsureBuilt();
            if (placeBadge == null)
            {
                placeBadge = UIBuild.Image("PlaceBadge", content, tableTheme != null ? tableTheme.panel : null,
                    tableTheme != null ? tableTheme.accent : Color.yellow, new Vector2(120f, 56f), Vector2.zero);
                placeBadge.type = Image.Type.Sliced;
                placeText = UIBuild.Text("Place", placeBadge.transform, "", 32f, new Color(0.15f, 0.1f, 0.02f),
                    new Vector2(120f, 56f), Vector2.zero, TextAlignmentOptions.Center, tableTheme != null ? tableTheme.font : null, true);
            }

            bool finished = position > 0;
            placeBadge.gameObject.SetActive(finished);
            placeBadge.rectTransform.anchoredPosition = cardsBlock.anchoredPosition;
            if (finished) placeText.text = Ordinal(position);
            if (finished && handBadge != null) handBadge.gameObject.SetActive(false);
        }

        public static string Ordinal(int n)
        {
            int mod100 = n % 100;
            string suffix = mod100 >= 11 && mod100 <= 13 ? "th" : (n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th");
            return n + suffix;
        }

        // ------------------------------------------------------------------
        // Public display API
        // ------------------------------------------------------------------

        public void SetPlayer(string playerName, int avatarIndex)
        {
            EnsureBuilt();
            nameText.text = playerName;
            avatarInitial.text = string.IsNullOrEmpty(playerName) ? "?" : playerName.Substring(0, 1).ToUpperInvariant();
            avatar.color = tableTheme != null ? tableTheme.GetAvatarColor(avatarIndex) : Color.gray;
        }

        public void SetTurn(bool value)
        {
            EnsureBuilt();
            isTurn = value;
            UIBuild.SetActive(turnGlow, value);
            UIBuild.SetActive(avatarRing, value);
            if (tableTheme != null)
            {
                plate.color = value ? tableTheme.panelTurnColor : tableTheme.panelColor;
                nameText.color = value ? Color.white : tableTheme.textColor;
            }
        }

        /// <summary>Hidden hand: shows up to three small card backs plus the exact count.</summary>
        public void SetHandCount(int count)
        {
            EnsureBuilt();
            for (int i = 0; i < handFan.Length; i++) UIBuild.SetActive(handFan[i], i < count);
            handCountText.text = count.ToString();
        }

        public void SetHandBadgeVisible(bool visible)
        {
            EnsureBuilt();
            handBadge.gameObject.SetActive(visible);
            nameText.rectTransform.offsetMax = new Vector2(visible ? -84f : -14f, 0f);
        }

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
                    cardsBlock.anchoredPosition = new Vector2(0f, 26f);
                    plateRt.sizeDelta = new Vector2(StackedSize.x, 44f);
                    plateRt.anchoredPosition = new Vector2(0f, -76f);
                    break;
                case PlateSide.Above:
                    cardsBlock.anchoredPosition = new Vector2(0f, -26f);
                    plateRt.sizeDelta = new Vector2(StackedSize.x, 44f);
                    plateRt.anchoredPosition = new Vector2(0f, 76f);
                    break;
                default:
                    cardsBlock.anchoredPosition = new Vector2(131f, 0f);
                    plateRt.sizeDelta = new Vector2(230f, 44f);
                    plateRt.anchoredPosition = new Vector2(-145f, 0f);
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

            cardsBlock = UIBuild.Rect("TableCards", content, new Vector2(CardStep * 2f + CardView.Width * TableCardScale, 144f), Vector2.zero);
            faceDownCards = new CardView[3];
            faceUpCards = new CardView[3];
            for (int i = 0; i < 3; i++)
                faceDownCards[i] = SpawnCard("FaceDown" + (i + 1), new Vector2((i - 1) * CardStep, -FaceUpOffset));
            for (int i = 0; i < 3; i++)
                faceUpCards[i] = SpawnCard("FaceUp" + (i + 1), new Vector2((i - 1) * CardStep, FaceUpOffset));

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
