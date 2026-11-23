using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Palace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// Runs a game on this device: one human (seat 0) against bots.
    ///
    ///  - The rules engine (PalaceGame) owns the game state and decides what is legal.
    ///  - This controller turns taps into commands, asks bots for their moves, and plays the
    ///    resulting GameEvents back as animations and sounds.
    ///
    /// For online play (Stage 5) the same flow is kept, but commands are sent to the host
    /// and events come back over the network.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        private static readonly string[] BotNames =
            { "Alex", "Jordan", "Sam", "Charlie", "Riley", "Taylor", "Morgan", "Casey", "Jamie" };

        [SerializeField] private TableView table;
        [SerializeField] private TableTheme tableTheme;
        [SerializeField] private CardTheme cardTheme;
        [SerializeField] private RectTransform hudRoot;   // full-screen, inside the safe area

        public PalaceGame Game { get; private set; }
        public bool IsBusy => busy;
        public event Action<PalaceGame> GameFinished;

        private System.Random botRng;
        private HandView hand;
        private int localPlayer;          // -1 when only watching bots
        private bool busy;
        private bool paused;
        private float botTimer;

        private readonly HashSet<int> selected = new HashSet<int>();
        private PlaySource selectedSource;

        // What the table currently SHOWS (lags behind the real state while animations play).
        private readonly List<Card> shownPile = new List<Card>();
        private int shownDeck;

        // HUD
        private RectTransform tableHud;
        private Button playButton;
        private Button pickUpButton;
        private TextMeshProUGUI statusText;
        private string toastMessage;
        private float toastUntil;
        private GameObject sevenPanel;
        private GameObject acePanel;
        private GameObject pausePanel;
        private RectTransform resultsLayer;

        public void Configure(TableView tableView, TableTheme tTheme, CardTheme cTheme, RectTransform hud)
        {
            table = tableView;
            tableTheme = tTheme;
            cardTheme = cTheme;
            hudRoot = hud;
        }

        // ==================================================================
        // Start-up
        // ==================================================================

        private void Start()
        {
            UIKit.Init(tableTheme);
            GameSettings.ApplyFrameRate();
            AudioManager.StartMusic();
            AutoPlayHarness.AttachIfRequested(this);

            hand = new HandView(table);
            hand.CardTapped += OnHandCardTapped;
            BuildHud();
            StartNewGame();
        }

        public void StartNewGame()
        {
            StopAllCoroutines();
            busy = false;
            paused = false;
            botTimer = 0f;
            selected.Clear();
            if (resultsLayer != null) Destroy(resultsLayer.gameObject);
            if (pausePanel != null) pausePanel.SetActive(false);
            foreach (Transform flying in table.FxRoot) Destroy(flying.gameObject); // cards left mid-flight by a restart

            int bots = Mathf.Clamp(GameLaunch.BotCount, 1, 9);
            var names = new List<string> { GameLaunch.SpectateOnly ? "Bot" : GameLaunch.PlayerName };
            var isBot = new List<bool> { GameLaunch.SpectateOnly };
            for (int i = 0; i < bots; i++)
            {
                names.Add(BotNames[i]);
                isBot.Add(true);
            }

            int seed = GameLaunch.Seed != 0 ? GameLaunch.Seed : Environment.TickCount;
            Game = PalaceGame.Create(names, isBot, new HouseRules(), seed);
            botRng = new System.Random(seed * 31 + 7);
            localPlayer = GameLaunch.SpectateOnly ? -1 : 0;

            IReadOnlyList<SeatView> seats = table.BuildSeats(names);
            for (int k = 0; k < HouseRules.TableCards; k++)
            {
                int slot = k;
                seats[0].FaceUpCards[k].Clicked += _ => OnFaceUpTapped(slot);
                seats[0].FaceDownCards[k].Clicked += _ => OnFaceDownTapped(slot);
            }
            table.FxRoot.SetAsLastSibling();

            shownPile.Clear();
            shownDeck = Game.State.TotalCards;
            StartCoroutine(DealThenPlay());
        }

        // ==================================================================
        // Main loop: bots act when it's their turn
        // ==================================================================

        private void Update()
        {
            if (Game == null) return;
            if (statusText != null) UpdateStatus();
            if (busy || paused || Game.State.Phase == GamePhase.GameOver) return;

            int actor = Game.State.ActingPlayer;
            bool autoPlayLocal = AutoPlayHarness.Active && actor == localPlayer; // test builds only
            if (!Game.State.Players[actor].IsBot && !autoPlayLocal) return;

            botTimer += Time.unscaledDeltaTime;
            if (botTimer < BotDelay) return;
            botTimer = 0f;

            BotMove move = BotBrain.Decide(Game, actor, botRng);
            Execute(BotBrain.Execute(Game, actor, move));
        }

        private float BotDelay => AutoPlayHarness.Active ? 0.02f : Mathf.Max(0.25f, 0.8f * GameSettings.AnimationScale);

        private void Execute(CommandResult result)
        {
            if (!result.Ok)
            {
                Toast(result.Error);
                return;
            }
            selected.Clear();
            HidePrompts();
            busy = true;
            StartCoroutine(PlayEvents(result.Events));
        }

        // ==================================================================
        // Local player input
        // ==================================================================

        private bool LocalCanAct =>
            localPlayer >= 0 && !busy && !paused &&
            Game.State.Phase == GamePhase.Playing && Game.State.Current == localPlayer;

        private void OnHandCardTapped(Card card)
        {
            if (!LocalCanAct || Game.State.Players[localPlayer].ActiveSource != PlaySource.Hand) return;
            ToggleSelection(card, PlaySource.Hand);
        }

        private void OnFaceUpTapped(int slot)
        {
            if (!LocalCanAct) return;
            PlayerState me = Game.State.Players[localPlayer];
            if (me.ActiveSource != PlaySource.FaceUp)
            {
                Toast(me.ActiveSource == PlaySource.Hand ? "Play the cards in your hand first" : "");
                return;
            }
            if (me.FaceUp[slot].HasValue) ToggleSelection(me.FaceUp[slot].Value, PlaySource.FaceUp);
        }

        private void OnFaceDownTapped(int slot)
        {
            if (!LocalCanAct) return;
            PlayerState me = Game.State.Players[localPlayer];
            if (me.ActiveSource != PlaySource.FaceDown)
            {
                Toast("Face-down cards come last");
                return;
            }
            Execute(Game.PlayFaceDown(localPlayer, slot));
        }

        private void ToggleSelection(Card card, PlaySource source)
        {
            PlayCheck check = Game.CheckRank(card.Rank);
            if (!check.Legal)
            {
                Toast(check.Reason);
                return;
            }

            // Only cards of one rank can be selected together.
            if (selected.Count > 0 && (selectedSource != source || FindSelectedRank() != card.Rank)) selected.Clear();
            selectedSource = source;
            if (!selected.Remove(card.Id)) selected.Add(card.Id);
            AudioManager.Play(Sfx.Select, 0.6f);
            RefreshAll();
        }

        private Rank? FindSelectedRank()
        {
            PlayerState me = Game.State.Players[localPlayer];
            foreach (var c in me.Hand) if (selected.Contains(c.Id)) return c.Rank;
            foreach (var c in me.FaceUp) if (c.HasValue && selected.Contains(c.Value.Id)) return c.Value.Rank;
            return null;
        }

        private void OnPlayPressed()
        {
            if (!LocalCanAct || selected.Count == 0) return;
            Execute(Game.PlayCards(localPlayer, selectedSource, selected.ToList()));
        }

        private void OnPickUpPressed()
        {
            if (!LocalCanAct) return;
            Execute(Game.PickUp(localPlayer));
        }

        private void OnSevenCall(SevenCall call)
        {
            if (busy || Game.State.Phase != GamePhase.AwaitingSevenCall || Game.State.PendingPlayer != localPlayer) return;
            Execute(Game.DeclareSeven(localPlayer, call));
        }

        private void OnAceChoice(bool burn)
        {
            if (busy || Game.State.Phase != GamePhase.AwaitingAceChoice || Game.State.PendingPlayer != localPlayer) return;
            Execute(Game.ChooseAce(localPlayer, burn));
        }

        // ==================================================================
        // Showing the state
        // ==================================================================

        private void RefreshAll()
        {
            GameState s = Game.State;
            IReadOnlyList<SeatView> seats = table.Seats;
            bool myTurn = LocalCanAct;
            PlaySource mySource = localPlayer >= 0 ? s.Players[localPlayer].ActiveSource : PlaySource.Hand;

            for (int i = 0; i < seats.Count; i++)
            {
                PlayerState p = s.Players[i];
                SeatView seat = seats[i];
                bool mine = i == localPlayer;

                for (int k = 0; k < HouseRules.TableCards; k++)
                {
                    seat.SetFaceDownSlot(k, p.FaceDown[k].HasValue);
                    seat.SetFaceUpSlot(k, p.FaceUp[k]);

                    CardView up = seat.FaceUpCards[k];
                    bool upActive = mine && myTurn && mySource == PlaySource.FaceUp && p.FaceUp[k].HasValue;
                    up.SetInteractable(upActive);
                    up.SetSelected(upActive && selected.Contains(p.FaceUp[k].Value.Id));
                    up.SetDimmed(upActive && !Game.CheckRank(p.FaceUp[k].Value.Rank).Legal);

                    CardView down = seat.FaceDownCards[k];
                    down.SetInteractable(mine && myTurn && mySource == PlaySource.FaceDown);
                }

                seat.SetHandCount(p.Hand.Count);
                seat.SetFinished(p.FinishPosition);
                seat.SetTurn(s.Phase != GamePhase.GameOver && i == s.ActingPlayer);
            }

            table.SetDeckCount(shownDeck);
            table.SetDiscardPile(shownPile);
            table.SetDirection(s.Direction);
            SevenCall seven = Game.ActiveSeven;
            table.SetEffect(seven == SevenCall.Higher ? "Next: HIGHER than 7" : seven == SevenCall.Lower ? "Next: LOWER than 7" : null);

            if (localPlayer >= 0)
            {
                var cards = s.Players[localPlayer].Hand.OrderBy(c => c.Rank).ThenBy(c => c.Suit).ToList();
                bool handActive = myTurn && mySource == PlaySource.Hand;
                hand.Show(cards, selected, handActive ? (Func<Card, bool>)(c => Game.CheckRank(c.Rank).Legal) : null, handActive);
            }

            playButton.gameObject.SetActive(myTurn && selected.Count > 0);
            if (selected.Count > 0) UIKit.SetButtonLabel(playButton, selected.Count == 1 ? "PLAY CARD" : "PLAY " + selected.Count + " CARDS");
            pickUpButton.gameObject.SetActive(myTurn && Game.MustPickUp(localPlayer));

            bool myChoice = !busy && localPlayer >= 0 && s.PendingPlayer == localPlayer;
            sevenPanel.SetActive(myChoice && s.Phase == GamePhase.AwaitingSevenCall);
            acePanel.SetActive(myChoice && s.Phase == GamePhase.AwaitingAceChoice);
        }

        private void HidePrompts()
        {
            sevenPanel.SetActive(false);
            acePanel.SetActive(false);
            playButton.gameObject.SetActive(false);
            pickUpButton.gameObject.SetActive(false);
        }

        private void UpdateStatus()
        {
            if (Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toastMessage))
            {
                statusText.text = toastMessage;
                statusText.color = new Color(1f, 0.6f, 0.5f);
                return;
            }

            statusText.color = UIKit.Text;
            GameState s = Game.State;
            if (s.Phase == GamePhase.GameOver) { statusText.text = "Game over"; return; }

            int actor = s.ActingPlayer;
            string name = s.Players[actor].Name;

            if (actor != localPlayer)
            {
                if (s.Phase == GamePhase.AwaitingSevenCall) statusText.text = name + " is calling higher or lower...";
                else if (s.Phase == GamePhase.AwaitingAceChoice) statusText.text = name + " is choosing how to use the Ace...";
                else statusText.text = name + "'s turn";
                return;
            }

            if (s.Phase == GamePhase.AwaitingSevenCall) { statusText.text = "Call HIGHER or LOWER for the next player"; return; }
            if (s.Phase == GamePhase.AwaitingAceChoice) { statusText.text = "Use your Ace as a high card or a burn"; return; }
            if (busy) { statusText.text = ""; return; }

            PlayerState me = s.Players[localPlayer];
            if (Game.MustPickUp(localPlayer)) statusText.text = "No card can be played. Pick up the pile.";
            else if (me.ActiveSource == PlaySource.FaceDown) statusText.text = "Your turn: tap a face-down card";
            else if (me.ActiveSource == PlaySource.FaceUp) statusText.text = "Your turn: play a face-up card (" + Game.Requirement() + ")";
            else statusText.text = "Your turn: play " + Game.Requirement();
        }

        private void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            toastMessage = message;
            toastUntil = Time.unscaledTime + 2.5f;
        }

        // ==================================================================
        // Animations
        // ==================================================================

        private IEnumerator DealThenPlay()
        {
            busy = true;
            GameState s = Game.State;
            IReadOnlyList<SeatView> seats = table.Seats;

            // Empty table, full deck.
            foreach (var seat in seats)
            {
                for (int k = 0; k < HouseRules.TableCards; k++)
                {
                    seat.SetFaceDownSlot(k, false);
                    seat.SetFaceUpSlot(k, null);
                }
                seat.SetHandCount(0);
                seat.SetTurn(false);
                seat.SetFinished(0);
            }
            table.SetDeckCount(shownDeck);
            table.SetDiscardPile(shownPile);
            table.SetEffect(null);
            table.SetDirection(s.Direction);
            hand.Show(new List<Card>(), null, null, false);
            HidePrompts();

            // Nine quick rounds: face-down, face-up, then hand cards.
            for (int r = 0; r < 9; r++)
            {
                int round = r;   // copy: the lambdas below run later, after the loop has moved on
                int k = round % 3;
                for (int i = 0; i < seats.Count; i++)
                {
                    PlayerState p = s.Players[i];
                    SeatView seat = seats[i];
                    Transform target = round < 3 ? seat.FaceDownCards[k].transform
                        : round < 6 ? seat.FaceUpCards[k].transform
                        : (i == localPlayer ? table.HandRoot : seat.transform);
                    shownDeck--;
                    StartCoroutine(FlyCard(null, table.DeckPoint, table.ToLocal(target), table.PileCardScale, ScaleOf(target), 0.22f, () =>
                    {
                        if (round < 3) seat.SetFaceDownSlot(k, true);
                        else if (round < 6) seat.SetFaceUpSlot(k, p.FaceUp[k]);
                        else seat.SetHandCount(round - 5);
                    }));
                }
                table.SetDeckCount(shownDeck);
                if (GameSettings.AnimationScale > 0f) AudioManager.Play(Sfx.CardDraw, 0.5f);
                yield return Tween.Wait(Tween.Scaled(0.09f));
            }
            yield return Tween.Wait(Tween.Scaled(0.3f));

            shownDeck = s.DrawPile.Count;
            yield return PlayEvents(Game.StartEvents);
        }

        private IEnumerator PlayEvents(IReadOnlyList<GameEvent> events)
        {
            busy = true;

            // Capture where played cards start BEFORE the views refresh.
            var origins = new Dictionary<int, (Vector2 pos, float scale)>();
            foreach (var e in events)
            {
                if (e.Type != GameEventType.CardsPlayed && e.Type != GameEventType.FaceDownRevealed) continue;
                for (int i = 0; i < e.Cards.Count; i++)
                    origins[e.Cards[i].Id] = SourcePoint(e, i);
            }

            var onPile = new HashSet<int>();
            RefreshAll();

            foreach (var e in events)
            {
                yield return AnimateEvent(e, origins, onPile);
            }

            busy = false;
            RefreshAll();

            if (Game.State.Phase != GamePhase.GameOver && Game.State.ActingPlayer == localPlayer && Game.State.Phase == GamePhase.Playing)
            {
                // Tell the player when they have no choice but to pick up.
                if (Game.MustPickUp(localPlayer)) AudioManager.Play(Sfx.YourTurn, 0.5f);
            }
        }

        private (Vector2 pos, float scale) SourcePoint(GameEvent e, int index)
        {
            SeatView seat = table.Seats[e.Player];
            if (e.Source == PlaySource.Hand)
            {
                if (e.Player == localPlayer)
                {
                    CardView v = hand.Find(e.Cards[index].Id);
                    if (v != null) return (table.ToLocal(v.transform), 1f);
                    return (table.HandCenter, 1f);
                }
                return (table.SeatPoint(e.Player), ScaleOf(seat.transform) * SeatView.TableCardScale);
            }

            int slot = e.Slots.Count > index ? e.Slots[index] : 0;
            Transform t = e.Source == PlaySource.FaceUp ? seat.FaceUpCards[slot].transform : seat.FaceDownCards[slot].transform;
            return (table.ToLocal(t), ScaleOf(t));
        }

        private IEnumerator AnimateEvent(GameEvent e, Dictionary<int, (Vector2 pos, float scale)> origins, HashSet<int> onPile)
        {
            GameState s = Game.State;
            string who = e.Player >= 0 && e.Player < s.Players.Count ? s.Players[e.Player].Name : "";
            Vector2 centre = table.DiscardPoint + new Vector2(-120f, 150f);

            switch (e.Type)
            {
                case GameEventType.FaceDownRevealed:
                {
                    Card card = e.Cards[0];
                    var from = origins[card.Id];
                    AudioManager.Play(Sfx.Reveal);
                    yield return FlipCard(card, from.pos, from.scale);
                    yield return FlyCard(card, from.pos, table.DiscardPoint, from.scale, table.PileCardScale, 0.3f, null);
                    shownPile.Add(card);
                    onPile.Add(card.Id);
                    table.SetDiscardPile(shownPile);
                    break;
                }

                case GameEventType.CardsPlayed:
                {
                    var flights = new List<Coroutine>();
                    for (int i = 0; i < e.Cards.Count; i++)
                    {
                        Card card = e.Cards[i];
                        if (onPile.Contains(card.Id)) continue;
                        (Vector2 pos, float scale) from = origins.TryGetValue(card.Id, out var o) ? o : (table.SeatPoint(e.Player), 0.5f);
                        flights.Add(StartCoroutine(FlyCard(card, from.pos, table.DiscardPoint, from.scale, table.PileCardScale, 0.3f, () =>
                        {
                            shownPile.Add(card);
                            table.SetDiscardPile(shownPile);
                        })));
                        AudioManager.Play(Sfx.CardPlay);
                        yield return Tween.Wait(Tween.Scaled(0.08f));
                    }
                    foreach (var f in flights) yield return f;
                    foreach (var c in e.Cards) if (!shownPile.Contains(c)) shownPile.Add(c);
                    table.SetDiscardPile(shownPile);
                    break;
                }

                case GameEventType.CardsDrawn:
                {
                    Vector2 to = e.Player == localPlayer ? table.HandCenter : table.SeatPoint(e.Player);
                    float toScale = e.Player == localPlayer ? 1f : 0.5f;
                    for (int i = 0; i < e.Cards.Count; i++)
                    {
                        shownDeck = Mathf.Max(0, shownDeck - 1);
                        table.SetDeckCount(shownDeck);
                        AudioManager.Play(Sfx.CardDraw, 0.6f);
                        StartCoroutine(FlyCard(null, table.DeckPoint, to, table.PileCardScale, toScale, 0.25f, null));
                        yield return Tween.Wait(Tween.Scaled(0.07f));
                    }
                    yield return Tween.Wait(Tween.Scaled(0.2f));
                    break;
                }

                case GameEventType.PileCollected:
                {
                    Vector2 to = e.Player == localPlayer ? table.HandCenter : table.SeatPoint(e.Player);
                    AudioManager.Play(Sfx.PickUp);
                    table.Announce(who + " picks up " + e.Cards.Count, new Color(1f, 0.75f, 0.6f), centre, 44f);
                    int shown = Mathf.Min(6, shownPile.Count);
                    for (int i = 0; i < shown; i++)
                    {
                        Card c = shownPile[shownPile.Count - 1 - i];
                        StartCoroutine(FlyCard(c, table.DiscardPoint, to, table.PileCardScale, 0.5f, 0.35f, null));
                    }
                    shownPile.Clear();
                    table.SetDiscardPile(shownPile);
                    yield return Tween.Wait(Tween.Scaled(0.45f));
                    break;
                }

                case GameEventType.PileBurned:
                    AudioManager.Play(Sfx.Burn);
                    table.Announce("BURN!", new Color(1f, 0.55f, 0.15f), centre, 84f);
                    yield return BurnPile();
                    break;

                case GameEventType.PlayersSkipped:
                    AudioManager.Play(Sfx.Skip);
                    table.Announce(e.Count > 1 ? "SKIP x" + e.Count : "SKIP", new Color(0.6f, 0.85f, 1f), centre, 72f);
                    foreach (int p in e.Players)
                        if (p != e.Player) table.Announce("skipped", new Color(0.6f, 0.85f, 1f), table.SeatPoint(p), 34f);
                    yield return Tween.Wait(Tween.Scaled(0.6f));
                    break;

                case GameEventType.DirectionReversed:
                    AudioManager.Play(Sfx.Reverse);
                    table.Announce(e.Count % 2 == 1 ? "REVERSE" : "DOUBLE REVERSE", new Color(0.85f, 0.7f, 1f), centre, 72f);
                    table.SetDirection(e.Direction);
                    yield return Tween.Wait(Tween.Scaled(0.6f));
                    break;

                case GameEventType.Reset:
                    AudioManager.Play(Sfx.Reset);
                    table.Announce("RESET", new Color(0.65f, 1f, 0.7f), centre, 72f);
                    yield return Tween.Wait(Tween.Scaled(0.45f));
                    break;

                case GameEventType.SevenDeclared:
                    table.Announce(e.Call == SevenCall.Higher ? "HIGHER than 7" : "LOWER than 7", UIKit.Gold, centre, 64f);
                    AudioManager.Play(Sfx.Skip, 0.5f);
                    yield return Tween.Wait(Tween.Scaled(0.6f));
                    break;

                case GameEventType.AceChosen:
                    if (!e.Burn)
                    {
                        table.Announce("ACE HIGH", UIKit.Gold, centre, 64f);
                        yield return Tween.Wait(Tween.Scaled(0.45f));
                    }
                    break;

                case GameEventType.PlayerFinished:
                {
                    bool last = e.Position == s.Players.Count;
                    string text = e.Player == localPlayer
                        ? (last ? "You finished last" : "You finish " + SeatView.Ordinal(e.Position) + "!")
                        : who + (last ? " finishes last" : " finishes " + SeatView.Ordinal(e.Position) + "!");
                    table.Announce(text, UIKit.Gold, centre + new Vector2(0f, 90f), 56f);
                    if (e.Position == 1 || e.Player == localPlayer) AudioManager.Play(Sfx.Win, 0.8f);
                    RefreshAll();
                    yield return Tween.Wait(Mathf.Max(0.4f, Tween.Scaled(0.9f)));
                    break;
                }

                case GameEventType.TurnChanged:
                    if (e.Player == localPlayer && s.Phase == GamePhase.Playing && Game.State.Current == localPlayer)
                    {
                        AudioManager.Play(Sfx.YourTurn, 0.6f);
                        table.Announce("YOUR TURN", UIKit.Gold, table.HandCenter + new Vector2(0f, 150f), 48f);
                    }
                    break;

                case GameEventType.GameOver:
                    RefreshAll();
                    yield return Tween.Wait(Mathf.Max(0.5f, Tween.Scaled(1.2f)));
                    ShowResults();
                    GameFinished?.Invoke(Game);
                    break;
            }
        }

        private IEnumerator FlyCard(Card? card, Vector2 from, Vector2 to, float fromScale, float toScale, float seconds, Action onArrive)
        {
            float duration = Tween.Scaled(seconds);
            if (duration <= 0f)
            {
                onArrive?.Invoke();
                yield break;
            }

            CardView view = CardFactory.Spawn(table.CardPrefab, table.CardTheme, table.FxRoot);
            view.SetInteractable(false);
            if (card.HasValue) view.ShowCard(card.Value, true);
            else view.ShowBack();

            var rt = (RectTransform)view.transform;
            float spin = UnityEngine.Random.Range(-12f, 12f);
            yield return Tween.Move(rt, from, to, duration, fromScale, toScale, 0f, spin);
            Destroy(view.gameObject);
            onArrive?.Invoke();
        }

        private IEnumerator FlipCard(Card card, Vector2 at, float scale)
        {
            float half = Tween.Scaled(0.12f);
            if (half <= 0f) yield break;

            CardView view = CardFactory.Spawn(table.CardPrefab, table.CardTheme, table.FxRoot);
            view.SetInteractable(false);
            view.ShowBack();
            var rt = (RectTransform)view.transform;
            rt.anchoredPosition = at;
            float big = Mathf.Max(scale * 1.3f, 0.7f);

            yield return Tween.Run(half, t => rt.localScale = new Vector3(Mathf.Lerp(scale, big, t) * (1f - t), Mathf.Lerp(scale, big, t), 1f));
            view.ShowCard(card, true);
            yield return Tween.Run(half, t => rt.localScale = new Vector3(big * t, big, 1f));
            yield return Tween.Wait(Tween.Scaled(0.25f));
            Destroy(view.gameObject);
        }

        private IEnumerator BurnPile()
        {
            var burning = new List<CardView>();
            int shown = Mathf.Min(6, shownPile.Count);
            for (int i = shownPile.Count - shown; i < shownPile.Count; i++)
            {
                CardView v = CardFactory.Spawn(table.CardPrefab, table.CardTheme, table.FxRoot);
                v.SetInteractable(false);
                v.ShowCard(shownPile[i], true);
                var rt = (RectTransform)v.transform;
                rt.anchoredPosition = table.DiscardPoint;
                rt.localScale = Vector3.one * table.PileCardScale;
                burning.Add(v);
            }
            shownPile.Clear();
            table.SetDiscardPile(shownPile);

            var groups = burning.Select(v => v.gameObject.AddComponent<CanvasGroup>()).ToList();
            yield return Tween.Run(Mathf.Max(0.2f, Tween.Scaled(0.5f)), t =>
            {
                for (int i = 0; i < burning.Count; i++)
                {
                    var rt = (RectTransform)burning[i].transform;
                    rt.localScale = Vector3.one * table.PileCardScale * (1f + 0.4f * t);
                    rt.localEulerAngles = new Vector3(0f, 0f, (i - burning.Count * 0.5f) * 14f * t);
                    rt.anchoredPosition = table.DiscardPoint + new Vector2((i - burning.Count * 0.5f) * 30f * t, 40f * t);
                    groups[i].alpha = 1f - t;
                }
            });
            foreach (var v in burning) Destroy(v.gameObject);
        }

        private float ScaleOf(Transform t) => t.lossyScale.x / Mathf.Max(0.0001f, table.transform.lossyScale.x);

        // ==================================================================
        // HUD
        // ==================================================================

        private void BuildHud()
        {
            // Buttons and prompts that belong to the table area (fixed 1920x1080 space).
            tableHud = UIBuild.Stretch("GameHud", table.transform);

            statusText = UIKit.Label(tableHud, "", 26f, new Vector2(440f, 70f), new Vector2(735f, -372f), TextAlignmentOptions.Center, null, true);
            statusText.textWrappingMode = TextWrappingModes.Normal;

            playButton = UIKit.Button(tableHud, "PLAY", new Vector2(300f, 84f), new Vector2(735f, -462f), OnPlayPressed, true, 32f);
            pickUpButton = UIKit.Button(tableHud, "PICK UP PILE", new Vector2(300f, 84f), new Vector2(735f, -462f), OnPickUpPressed, false, 30f);
            UIKit.SetButtonStyle(pickUpButton, false);
            ((Image)pickUpButton.targetGraphic).color = new Color(0.55f, 0.16f, 0.14f, 0.97f);

            sevenPanel = BuildChoice("You played a 7. The next player must play...", "HIGHER", () => OnSevenCall(SevenCall.Higher),
                "LOWER", () => OnSevenCall(SevenCall.Lower));
            acePanel = BuildChoice("Use your Ace as...", "HIGH CARD", () => OnAceChoice(false),
                "BURN (as 10)", () => OnAceChoice(true));

            // Menu button + pause menu live in the safe-area HUD.
            var menu = UIKit.Button(hudRoot, "Menu", new Vector2(150f, 64f), Vector2.zero, TogglePause, false, 26f);
            var mrt = (RectTransform)menu.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 1f);
            mrt.anchoredPosition = new Vector2(-95f, -48f);

            pausePanel = UIKit.Layer(hudRoot, "PauseMenu", UIKit.Scrim).gameObject;
            var panel = UIKit.Panel(pausePanel.transform, new Vector2(520f, 470f), Vector2.zero);
            UIKit.Label(panel.transform, "Paused", 48f, new Vector2(480f, 70f), new Vector2(0f, 165f), TextAlignmentOptions.Center, UIKit.Gold, true);
            UIKit.Button(panel.transform, "Resume", new Vector2(380f, 76f), new Vector2(0f, 65f), TogglePause, true);
            UIKit.Button(panel.transform, "Restart", new Vector2(380f, 76f), new Vector2(0f, -25f), StartNewGame);
            UIKit.Button(panel.transform, "Main Menu", new Vector2(380f, 76f), new Vector2(0f, -115f), GoToMainMenu);
            pausePanel.SetActive(false);

            HidePrompts();
        }

        private GameObject BuildChoice(string title, string a, Action onA, string b, Action onB)
        {
            var panel = UIKit.Panel(tableHud, new Vector2(620f, 230f), new Vector2(0f, 20f));
            UIKit.Label(panel.transform, title, 28f, new Vector2(580f, 60f), new Vector2(0f, 60f), TextAlignmentOptions.Center, UIKit.Text, true);
            UIKit.Button(panel.transform, a, new Vector2(270f, 90f), new Vector2(-145f, -40f), onA, true, 30f);
            UIKit.Button(panel.transform, b, new Vector2(270f, 90f), new Vector2(145f, -40f), onB, true, 30f);
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        private void TogglePause()
        {
            paused = !paused;
            pausePanel.SetActive(paused);
            pausePanel.transform.SetAsLastSibling();
        }

        private void GoToMainMenu()
        {
            SceneManager.LoadScene(GameLaunch.MainMenuScene);
        }

        private void ShowResults()
        {
            GameState s = Game.State;
            int n = s.Players.Count;
            resultsLayer = UIKit.Layer(hudRoot, "Results", UIKit.Scrim);
            float height = 300f + n * 52f;
            var panel = UIKit.Panel(resultsLayer, new Vector2(760f, height), Vector2.zero);
            float top = height * 0.5f;

            int winner = s.FinishOrder[0];
            string title = winner == localPlayer ? "You win!" : s.Players[winner].Name + " wins!";
            UIKit.Label(panel.transform, title, 60f, new Vector2(700f, 80f), new Vector2(0f, top - 60f), TextAlignmentOptions.Center, UIKit.Gold, true);
            UIKit.Label(panel.transform, n + " players", 26f, new Vector2(700f, 40f), new Vector2(0f, top - 112f), TextAlignmentOptions.Center, UIKit.Muted);

            for (int i = 0; i < s.FinishOrder.Count; i++)
            {
                int p = s.FinishOrder[i];
                float y = top - 165f - i * 52f;
                Color c = i == 0 ? UIKit.Gold : (p == localPlayer ? new Color(0.6f, 0.85f, 1f) : UIKit.Text);
                string suffix = i == n - 1 ? "   (last)" : "";
                UIKit.Label(panel.transform, SeatView.Ordinal(i + 1), 30f, new Vector2(120f, 48f), new Vector2(-230f, y), TextAlignmentOptions.Left, c, true);
                UIKit.Label(panel.transform, s.Players[p].Name + suffix, 30f, new Vector2(420f, 48f), new Vector2(60f, y), TextAlignmentOptions.Left, c, i == 0);
            }

            UIKit.Button(panel.transform, "Play Again", new Vector2(300f, 80f), new Vector2(-165f, -top + 65f), StartNewGame, true);
            UIKit.Button(panel.transform, "Main Menu", new Vector2(300f, 80f), new Vector2(165f, -top + 65f), GoToMainMenu);
            AudioManager.Play(Sfx.Win);
        }
    }
}
