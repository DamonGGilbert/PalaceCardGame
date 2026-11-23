using System.Collections.Generic;
using Palace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// The poker table: felt, centre area (deck, discard pile, effect banner),
    /// the seats around the edge and the local player's hand area.
    ///
    /// Works in the 1920x1080 reference space. The Canvas Scaler (Expand mode) guarantees
    /// that this whole area is visible on every screen shape, so the layout never needs
    /// separate scenes or hand-placed positions for different player counts.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TableView : MonoBehaviour
    {
        public static readonly Vector2 ReferenceSize = new Vector2(1920f, 1080f);

        [Header("Themes and prefabs")]
        [SerializeField] private TableTheme tableTheme;
        [SerializeField] private CardTheme cardTheme;
        [SerializeField] private SeatView seatPrefab;
        [SerializeField] private CardView cardPrefab;

        [Header("Table")]
        [SerializeField] private Vector2 tableSize = new Vector2(1800f, 900f);
        [SerializeField] private Vector2 tableCenter = new Vector2(0f, 20f);

        [Header("Seat layout")]
        [Tooltip("Racetrack outline that opponent seats are placed on.")]
        [SerializeField] private Vector2 seatPathSize = new Vector2(1440f, 740f);
        [Tooltip("Length of the outline (bottom centre) kept free for the local player.")]
        [SerializeField] private float localGap = 1150f;
        [SerializeField] private Vector2 localSeatPosition = new Vector2(0f, -248f);
        [SerializeField] private float localSeatScale = 0.85f;
        [SerializeField] private float seatMargin = 12f;
        [SerializeField] private float minSeatScale = 0.6f;
        [SerializeField] private float maxSeatScale = 1f;

        [Header("Areas seats must not cover")]
        [SerializeField] private Vector2 handAreaCenter = new Vector2(0f, -432f);
        [SerializeField] private Vector2 handAreaSize = new Vector2(1000f, 216f);
        [SerializeField] private Vector2 centerAreaSize = new Vector2(470f, 340f);

        [Header("Centre")]
        [SerializeField] private float pileCardScale = 0.85f;

        [Header("Built automatically - do not assign")]
        [SerializeField] private Image tableImage;
        [SerializeField] private RectTransform centerRoot;
        [SerializeField] private RectTransform seatsRoot;
        [SerializeField] private RectTransform handRoot;
        [SerializeField] private CardView[] deckCards;
        [SerializeField] private CardView[] discardCards;
        [SerializeField] private Image discardSlot;
        [SerializeField] private TextMeshProUGUI deckLabel;
        [SerializeField] private TextMeshProUGUI pileLabel;
        [SerializeField] private Image effectBadge;
        [SerializeField] private TextMeshProUGUI effectText;

        private readonly List<SeatView> seats = new List<SeatView>();
        private float lastOpponentScale = 1f;

        public IReadOnlyList<SeatView> Seats => seats;
        public RectTransform HandRoot => handRoot;
        public float LastOpponentScale => lastOpponentScale;
        public CardTheme CardTheme => cardTheme;
        public CardView CardPrefab => cardPrefab;

        public void Configure(TableTheme table, CardTheme cards, SeatView seat, CardView card)
        {
            tableTheme = table;
            cardTheme = cards;
            seatPrefab = seat;
            cardPrefab = card;
        }

        private RectTransform fxRoot;
        private TextMeshProUGUI directionText;

        private void Awake()
        {
            EnsureBuilt();
            if (tableTheme != null) tableImage.sprite = tableTheme.CurrentTable; // felt colour chosen in Settings
        }

        // ------------------------------------------------------------------
        // Positions and effects used by the game controller's animations
        // ------------------------------------------------------------------

        /// <summary>Layer above everything on the table, for cards in flight and announcements.</summary>
        public RectTransform FxRoot
        {
            get
            {
                if (fxRoot == null)
                {
                    fxRoot = UIBuild.Stretch("Effects", transform);
                    fxRoot.SetAsLastSibling();
                }
                return fxRoot;
            }
        }

        public Vector2 DeckPoint => tableCenter + DeckPosition;
        public Vector2 DiscardPoint => tableCenter + DiscardPosition;
        public float PileCardScale => pileCardScale;
        public Vector2 HandCenter => handAreaCenter;

        /// <summary>Converts any UI object's position into this table's coordinates (for FxRoot).</summary>
        public Vector2 ToLocal(Transform t) => transform.InverseTransformPoint(t.position);

        public Vector2 SeatPoint(int seat) => ((RectTransform)seats[seat].transform).anchoredPosition;

        public void SetDirection(int direction)
        {
            EnsureBuilt();
            if (directionText == null)
            {
                directionText = UIBuild.Text("Direction", centerRoot, "", 22f,
                    tableTheme != null ? tableTheme.mutedTextColor : Color.white, new Vector2(320f, 30f),
                    new Vector2(0f, -150f), TextAlignmentOptions.Center, tableTheme != null ? tableTheme.font : null, true);
            }
            directionText.text = direction >= 0 ? "Play goes clockwise" : "Play goes anticlockwise";
        }

        /// <summary>Big pop-up text such as "BURN!" or "SKIP". Fire and forget.</summary>
        public void Announce(string message, Color color, Vector2 position, float size = 64f)
        {
            var text = UIBuild.Text("Announce", FxRoot, message, size, color, new Vector2(900f, 120f), position,
                TextAlignmentOptions.Center, tableTheme != null ? tableTheme.font : null, true);
            text.outlineWidth = 0.18f;
            text.outlineColor = new Color32(0, 0, 0, 200);
            StartCoroutine(AnnounceRoutine(text));
        }

        private System.Collections.IEnumerator AnnounceRoutine(TextMeshProUGUI text)
        {
            var rt = text.rectTransform;
            float pop = Mathf.Max(0.12f, Tween.Scaled(0.18f));
            yield return Tween.Run(pop, t => rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, t));
            yield return Tween.Wait(Mathf.Max(0.45f, Tween.Scaled(0.7f)));
            Color start = text.color;
            Vector2 p = rt.anchoredPosition;
            yield return Tween.Run(Mathf.Max(0.15f, Tween.Scaled(0.3f)), t =>
            {
                text.color = new Color(start.r, start.g, start.b, 1f - t);
                rt.anchoredPosition = p + new Vector2(0f, 30f * t);
            });
            Destroy(text.gameObject);
        }

        // ------------------------------------------------------------------
        // Seats
        // ------------------------------------------------------------------

        /// <summary>
        /// Creates one seat per name. Index 0 must be the player using this device;
        /// the others follow in clockwise turn order.
        /// </summary>
        public IReadOnlyList<SeatView> BuildSeats(IReadOnlyList<string> playerNames)
        {
            EnsureBuilt();
            ClearSeats();

            for (int i = 0; i < playerNames.Count; i++)
            {
                SeatView seat = SpawnSeat();
                seat.name = i == 0 ? "Seat 0 (local)" : "Seat " + i;
                seat.SetPlayer(playerNames[i], i);
                seats.Add(seat);
            }

            Relayout();
            return seats;
        }

        public void SetTurn(int seatIndex)
        {
            for (int i = 0; i < seats.Count; i++) seats[i].SetTurn(i == seatIndex);
        }

        /// <summary>Positions and scales every seat. Safe to call at any time.</summary>
        public void Relayout()
        {
            int count = seats.Count;
            if (count == 0) return;

            List<Vector2> points = TableLayout.SeatPositions(count, seatPathSize, localGap);

            // Local player: fixed position, plate beside their table cards, hand shown separately.
            SeatView local = seats[0];
            local.SetPlateSide(SeatView.PlateSide.Left);
            local.SetHandBadgeVisible(false);
            Place(local, localSeatPosition, localSeatScale);

            // Fixed areas that opponents must avoid.
            var items = new List<TableLayout.Item>
            {
                new TableLayout.Item(localSeatPosition, local.Size * localSeatScale, false),
                new TableLayout.Item(handAreaCenter, handAreaSize, false),
                new TableLayout.Item(tableCenter, centerAreaSize, false)
            };

            for (int i = 1; i < count; i++)
            {
                Vector2 position = tableCenter + points[i];
                SeatView seat = seats[i];
                // Seats in the top half put their name on the outside (above) so the cards face the middle.
                seat.SetPlateSide(points[i].y > 60f ? SeatView.PlateSide.Above : SeatView.PlateSide.Below);
                seat.SetHandBadgeVisible(true);
                items.Add(new TableLayout.Item(position, seat.Size, true));
            }

            var bounds = new Rect(-ReferenceSize * 0.5f, ReferenceSize);
            lastOpponentScale = TableLayout.FitScale(items, bounds, seatMargin, minSeatScale, maxSeatScale);

            for (int i = 1; i < count; i++) Place(seats[i], tableCenter + points[i], lastOpponentScale);
        }

        private static void Place(SeatView seat, Vector2 position, float scale)
        {
            var rt = (RectTransform)seat.transform;
            rt.anchoredPosition = position;
            rt.localScale = Vector3.one * scale;
        }

        private void ClearSeats()
        {
            foreach (var seat in seats)
            {
                if (seat == null) continue;
                if (Application.isPlaying) Destroy(seat.gameObject);
                else DestroyImmediate(seat.gameObject);
            }
            seats.Clear();
        }

        private SeatView SpawnSeat()
        {
            SeatView seat;
            if (seatPrefab != null)
            {
                seat = Instantiate(seatPrefab, seatsRoot, false);
            }
            else
            {
                var go = new GameObject("Seat", typeof(RectTransform));
                go.layer = UIBuild.UILayer;
                go.transform.SetParent(seatsRoot, false);
                seat = go.AddComponent<SeatView>();
                seat.Configure(tableTheme, cardTheme, cardPrefab);
            }
            seat.EnsureBuilt();
            return seat;
        }

        // ------------------------------------------------------------------
        // Centre of the table
        // ------------------------------------------------------------------

        public void SetDeckCount(int count)
        {
            EnsureBuilt();
            int visible = count <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(count / 10f), 1, deckCards.Length);
            for (int i = 0; i < deckCards.Length; i++) deckCards[i].gameObject.SetActive(i < visible);
            deckLabel.text = count > 0 ? "Deck  " + count : "Deck empty";
        }

        /// <summary>Shows the top few cards of the discard pile, slightly scattered like a real pile.</summary>
        public void SetDiscardPile(IReadOnlyList<Card> pile)
        {
            EnsureBuilt();
            int shown = Mathf.Min(pile.Count, discardCards.Length);
            int first = pile.Count - shown;

            for (int i = 0; i < discardCards.Length; i++)
            {
                CardView view = discardCards[i];
                bool active = i < shown;
                view.gameObject.SetActive(active);
                if (!active) continue;

                Card card = pile[first + i];
                view.ShowCard(card, true);
                var rt = (RectTransform)view.transform;
                bool isTop = i == shown - 1;
                // Stable "random" scatter based on the card id, so cards don't jump around between refreshes.
                float angle = isTop ? 0f : ((card.Id * 37) % 17) - 8f;
                var jitter = isTop ? Vector2.zero : new Vector2(((card.Id * 13) % 9) - 4f, ((card.Id * 7) % 9) - 4f);
                rt.localEulerAngles = new Vector3(0f, 0f, angle);
                rt.anchoredPosition = DiscardPosition + jitter;
            }

            pileLabel.text = pile.Count > 0 ? "Pile  " + pile.Count : "Pile empty";
        }

        /// <summary>Banner for an active special effect, e.g. "Play LOWER than 7". Pass null to hide.</summary>
        public void SetEffect(string message)
        {
            EnsureBuilt();
            bool show = !string.IsNullOrEmpty(message);
            effectBadge.gameObject.SetActive(show);
            if (show) effectText.text = message;
        }

        private static readonly Vector2 DeckPosition = new Vector2(-120f, 6f);
        private static readonly Vector2 DiscardPosition = new Vector2(120f, 6f);

        // ------------------------------------------------------------------
        // Building
        // ------------------------------------------------------------------

        public void EnsureBuilt()
        {
            if (tableImage != null) return;

            var rt = (RectTransform)transform;
            rt.sizeDelta = ReferenceSize;

            Color text = tableTheme != null ? tableTheme.mutedTextColor : Color.white;
            TMP_FontAsset font = tableTheme != null ? tableTheme.font : null;

            tableImage = UIBuild.Image("TableFelt", transform, tableTheme != null ? tableTheme.table : null, Color.white, tableSize, tableCenter);

            centerRoot = UIBuild.Rect("Centre", transform, centerAreaSize, tableCenter);

            Sprite slotSprite = cardTheme != null ? cardTheme.outline : null;
            var slotColor = new Color(1f, 1f, 1f, 0.22f);
            var slotSize = new Vector2(CardView.Width, CardView.Height) * pileCardScale;
            UIBuild.Image("DeckSlot", centerRoot, slotSprite, slotColor, slotSize, DeckPosition);
            discardSlot = UIBuild.Image("DiscardSlot", centerRoot, slotSprite, slotColor, slotSize, DiscardPosition);

            deckCards = new CardView[5];
            for (int i = 0; i < deckCards.Length; i++)
                deckCards[i] = SpawnPileCard("DeckCard" + (i + 1), DeckPosition + new Vector2(-1.5f * i, 2.5f * i));

            discardCards = new CardView[6];
            for (int i = 0; i < discardCards.Length; i++)
                discardCards[i] = SpawnPileCard("DiscardCard" + (i + 1), DiscardPosition);

            deckLabel = UIBuild.Text("DeckLabel", centerRoot, "Deck", 22f, text, new Vector2(200f, 30f),
                DeckPosition + new Vector2(0f, -108f), TextAlignmentOptions.Center, font, true);
            pileLabel = UIBuild.Text("PileLabel", centerRoot, "Pile", 22f, text, new Vector2(200f, 30f),
                DiscardPosition + new Vector2(0f, -108f), TextAlignmentOptions.Center, font, true);

            effectBadge = UIBuild.Image("EffectBadge", centerRoot, tableTheme != null ? tableTheme.panel : null,
                tableTheme != null ? tableTheme.panelTurnColor : Color.black, new Vector2(440f, 46f), new Vector2(0f, 140f));
            effectBadge.type = Image.Type.Sliced;
            effectText = UIBuild.Text("EffectText", effectBadge.transform, "", 24f, Color.white, new Vector2(416f, 46f),
                Vector2.zero, TextAlignmentOptions.Center, font, true);

            seatsRoot = UIBuild.Stretch("Seats", transform);
            handRoot = UIBuild.Rect("LocalHand", transform, handAreaSize, handAreaCenter);

            SetDeckCount(0);
            SetDiscardPile(new List<Card>());
            SetEffect(null);
        }

        private CardView SpawnPileCard(string objectName, Vector2 position)
        {
            CardView card = CardFactory.Spawn(cardPrefab, cardTheme, centerRoot);
            card.name = objectName;
            var rt = (RectTransform)card.transform;
            rt.anchoredPosition = position;
            rt.localScale = Vector3.one * pileCardScale;
            card.SetInteractable(false);
            card.ShowBack();
            return card;
        }
    }
}
