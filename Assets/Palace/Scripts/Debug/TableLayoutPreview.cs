using System.Collections;
using System.Collections.Generic;
using Palace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// STAGE 1 TEST HARNESS ONLY. Fills the table with sample players and cards so the
    /// layout and visual style can be checked for 2-10 players. It contains no game rules
    /// and will be replaced by the real GameManager in Stage 2-4.
    /// </summary>
    public class TableLayoutPreview : MonoBehaviour
    {
        private static readonly string[] SampleNames =
            { "You", "Alex", "Jordan", "Sam", "Charlie", "Riley", "Taylor", "Morgan", "Casey", "Jamie" };

        [SerializeField] private TableView table;
        [SerializeField] private RectTransform controlsParent;
        [Range(2, 10)] [SerializeField] private int playerCount = 6;
        [SerializeField] private bool cycleTurns = true;
        [SerializeField] private float turnSeconds = 1.5f;
        [SerializeField] private int randomSeed = 7;

        private readonly List<CardView> handCards = new List<CardView>();
        private TextMeshProUGUI countLabel;
        private int currentTurn;
        private Coroutine turnRoutine;

        public void Configure(TableView tableView, RectTransform controls)
        {
            table = tableView;
            controlsParent = controls;
        }

        private void Start()
        {
            if (controlsParent != null) BuildControls();
            Apply();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && table != null && isActiveAndEnabled) Apply();
        }

        public void SetPlayerCount(int count)
        {
            playerCount = Mathf.Clamp(count, 2, 10);
            Apply();
        }

        /// <summary>Rebuilds the table with sample data. Public so tests/tools can call it.</summary>
        public void Apply()
        {
            if (table == null) return;

            var rng = new System.Random(randomSeed);
            var names = new List<string>();
            for (int i = 0; i < playerCount; i++) names.Add(SampleNames[i]);

            IReadOnlyList<SeatView> seats = table.BuildSeats(names);
            int nextId = 0;

            for (int s = 0; s < seats.Count; s++)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    seats[s].SetFaceDownSlot(slot, true);
                    seats[s].SetFaceUpSlot(slot, RandomCard(rng, nextId++));
                }
                seats[s].SetHandCount(3);
            }

            // Show a mid-game feel on one opponent: one face-up card already played, big hand.
            if (seats.Count > 2)
            {
                seats[2].SetFaceUpSlot(1, null);
                seats[2].SetHandCount(11);
            }

            var pile = new List<Card>();
            for (int i = 0; i < 5; i++) pile.Add(RandomCard(rng, nextId++));
            pile.Add(new Card(nextId++, Rank.Seven, Suit.Hearts));
            table.SetDiscardPile(pile);
            table.SetDeckCount(52 - playerCount * 9 > 0 ? 52 - playerCount * 9 : 0);
            table.SetEffect("Next player: LOWER than 7");

            BuildSampleHand(rng, ref nextId);

            currentTurn = Mathf.Min(1, seats.Count - 1);
            table.SetTurn(currentTurn);
            if (countLabel != null) countLabel.text = playerCount + " players";

            if (turnRoutine != null) StopCoroutine(turnRoutine);
            if (cycleTurns && Application.isPlaying) turnRoutine = StartCoroutine(CycleTurns());
        }

        private IEnumerator CycleTurns()
        {
            var wait = new WaitForSeconds(turnSeconds);
            while (true)
            {
                yield return wait;
                currentTurn = (currentTurn + 1) % table.Seats.Count;
                table.SetTurn(currentTurn);
            }
        }

        private void BuildSampleHand(System.Random rng, ref int nextId)
        {
            foreach (var card in handCards)
            {
                if (card == null) continue;
                if (Application.isPlaying) Destroy(card.gameObject);
                else DestroyImmediate(card.gameObject);
            }
            handCards.Clear();

            var ranks = new[] { Rank.Four, Rank.Nine, Rank.Nine, Rank.Queen };
            for (int i = 0; i < ranks.Length; i++)
            {
                CardView view = CardFactory.Spawn(table.CardPrefab, table.CardTheme, table.HandRoot);
                view.name = "HandCard" + (i + 1);
                ((RectTransform)view.transform).anchoredPosition = new Vector2((i - (ranks.Length - 1) * 0.5f) * 156f, 0f);
                view.ShowCard(new Card(nextId++, ranks[i], (Suit)rng.Next(4)), true);
                view.Clicked += c => c.SetSelected(!c.IsSelected);
                handCards.Add(view);
            }
        }

        private static Card RandomCard(System.Random rng, int id)
        {
            return new Card(id, (Rank)rng.Next(2, 15), (Suit)rng.Next(4));
        }

        private void BuildControls()
        {
            var panel = UIBuild.Rect("PreviewControls", controlsParent, new Vector2(330f, 64f), Vector2.zero, new Vector2(0f, 1f));
            panel.anchoredPosition = new Vector2(190f, -46f);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            MakeButton(panel, "-", new Vector2(-128f, 0f), () => SetPlayerCount(playerCount - 1));
            MakeButton(panel, "+", new Vector2(128f, 0f), () => SetPlayerCount(playerCount + 1));
            countLabel = UIBuild.Text("Count", panel, "", 26f, Color.white, new Vector2(190f, 50f), Vector2.zero,
                TextAlignmentOptions.Center, null, true);
        }

        private static void MakeButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var img = UIBuild.Image("Button " + label, parent, null, new Color(1f, 1f, 1f, 0.15f), new Vector2(56f, 52f), position);
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            UIBuild.Text("Label", img.transform, label, 34f, Color.white, new Vector2(56f, 52f), Vector2.zero,
                TextAlignmentOptions.Center, null, true);
        }
    }
}
