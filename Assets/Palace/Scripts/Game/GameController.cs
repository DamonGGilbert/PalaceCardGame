using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Palace.Core;
using Palace.Net;
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

        // Hooks used only by the automated test harness.
        public event Action<GameEvent> EventStarted;
        public bool HoldAutoPlay { get; set; }
        public TableView Table => table;

        /// <summary>A hand card the local player could drag onto the pile right now, or null.</summary>
        public CardView FindPlayableHandCard()
        {
            if (!LocalCanAct || Game.State.Players[localPlayer].ActiveSource != PlaySource.Hand) return null;
            foreach (var c in Game.State.Players[localPlayer].Hand)
                if (Game.CheckRank(c.Rank).Legal) return hand.Find(c.Id);
            return null;
        }

        private System.Random botRng;
        private HandView hand;
        private int localPlayer;          // -1 when only watching bots
        private bool busy;
        private bool paused;
        private float botTimer;
        private float botWait = -1f;      // this bot's thinking time (varies a little each turn)
        private float pauseUntil;         // bots wait until this time (e.g. after a pick-up)
        private string noticeMessage;
        private float noticeUntil;

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
        private RectTransform aceButtons;
        private TextMeshProUGUI aceTitle;
        private DirectionRing ring;
        private TableFx fx;
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
            hand.CanDrag = c => BeginDrag(c, PlaySource.Hand);
            hand.CardDragged += (v, e) => OnDragging(e);
            hand.CardDropped += (v, e) => OnDropped(v, e, PlaySource.Hand, -1);

            fx = TableFx.Attach(table);
            ring = DirectionRing.Create((RectTransform)table.transform, table.TableCenter, table.FeltSiblingIndex + 1);
            BuildHud();

            if (NetSession.Active && NetSession.Instance.InGame) StartNetworked();
            else StartNewGame();
        }

        // ==================================================================
        // Multiplayer
        //  - Host: runs the rules engine, plays the bots, and sends every other device its
        //    private view of the table after each move.
        //  - Client: sends its moves to the host and animates the updates that come back.
        // ==================================================================

        private NetSession net;   // null in single player
        private bool IsNetworked => net != null;
        private bool IsNetHost => net != null && net.IsHost;
        private bool IsNetClient => net != null && !net.IsHost;
        public bool IsWaitingForHost => awaitingHost;

        private readonly Dictionary<int, ulong> remoteClients = new Dictionary<int, ulong>(); // host: player -> device
        private readonly HashSet<int> takenOver = new HashSet<int>();                         // host: players who left
        private readonly HashSet<ulong> clientsInGame = new HashSet<ulong>();
        private readonly Queue<(ulong client, NetCommand command)> remoteCommands = new Queue<(ulong, NetCommand)>();
        private bool awaitingHost;   // client: a move was sent and the host hasn't answered yet
        private bool clientStarted;

        private void StartNetworked()
        {
            net = NetSession.Instance;
            net.Disconnected += OnNetDisconnected;
            net.ErrorReceived += OnNetError;
            if (net.IsHost)
            {
                net.CommandReceived += OnRemoteCommand;
                net.ClientInGame += OnClientInGame;
                net.ClientLeft += OnClientLeft;
                StartCoroutine(HostWaitThenStart());
            }
            else
            {
                botRng = new System.Random(Environment.TickCount);
                net.ReportInGame();
            }
        }

        private void OnRemoteCommand(ulong client, NetCommand command) => remoteCommands.Enqueue((client, command));
        private void OnClientInGame(ulong client) => clientsInGame.Add(client);

        /// <summary>Host: wait (up to 10 s) until every player's device has loaded the table.</summary>
        private IEnumerator HostWaitThenStart()
        {
            busy = true;
            float until = Time.unscaledTime + 10f;
            while (Time.unscaledTime < until)
            {
                bool everyone = net.Seats.All(s => s.IsBot || s.ClientId == net.LocalClientId || clientsInGame.Contains(s.ClientId));
                if (everyone) break;
                yield return null;
            }
            busy = false;
            StartNewGame();
        }

        private void HandleRemoteCommand((ulong client, NetCommand command) item)
        {
            int player = net.PlayerIndexOf(item.client);
            if (player < 0 || takenOver.Contains(player) || Game == null) return;
            if (Game.State.ActingPlayer != player)
            {
                net.SendError(item.client, "It isn't your turn");
                return;
            }
            CommandResult result = item.command.ApplyTo(Game, player);
            if (!result.Ok) net.SendError(item.client, result.Error);
            else Execute(result);
        }

        /// <summary>Host: send every other device its own view of what just happened.</summary>
        private void Broadcast(IReadOnlyList<GameEvent> events)
        {
            foreach (var kv in remoteClients)
                if (!takenOver.Contains(kv.Key)) net.SendUpdate(kv.Value, Game.State, events, kv.Key);
        }

        /// <summary>Client: an update from the host. A new game rebuilds the table and deals.</summary>
        private void ApplyHostUpdate(List<GameEvent> events, GameState state)
        {
            awaitingHost = false;
            localPlayer = net.LocalPlayerIndex;
            Game = new PalaceGame(state, new HouseRules(), 0);

            if (!clientStarted || events.Any(e => e.Type == GameEventType.GameStarted))
            {
                clientStarted = true;
                ResetTable();
                BuildTable();
                shownPile.Clear();
                shownDeck = state.TotalCards;
                StartCoroutine(DealThenPlay(events));
            }
            else
            {
                selected.Clear();
                HidePrompts();
                busy = true;
                StartCoroutine(PlayEvents(events));
            }
        }

        private void OnClientLeft(ulong client)
        {
            foreach (var kv in remoteClients)
            {
                if (kv.Value != client) continue;
                takenOver.Add(kv.Key);
                if (Game != null) Toast(Game.State.Players[kv.Key].Name + " left. A computer player takes over.");
            }
        }

        private void OnNetDisconnected(string reason)
        {
            GameLaunch.MenuMessage = reason;
            SceneManager.LoadScene(GameLaunch.MainMenuScene);
        }

        private void OnNetError(string error)
        {
            awaitingHost = false;
            Toast(error);
            if (Game != null) RefreshAll();
        }

        private void OnDestroy()
        {
            Tween.SpeedMultiplier = 1f; // never carry fast-forward into the menu
            if (net == null) return;
            net.Disconnected -= OnNetDisconnected;
            net.ErrorReceived -= OnNetError;
            net.CommandReceived -= OnRemoteCommand;
            net.ClientInGame -= OnClientInGame;
            net.ClientLeft -= OnClientLeft;
        }

        /// <summary>Where a player sits on THIS screen: you are always seat 0 at the bottom.</summary>
        private int SeatOf(int player)
        {
            int n = Game.State.Players.Count;
            return localPlayer < 0 ? player : (player - localPlayer + n) % n;
        }

        private int PlayerAtSeat(int seat)
        {
            int n = Game.State.Players.Count;
            return localPlayer < 0 ? seat : (seat + localPlayer) % n;
        }

        private SeatView SeatFor(int player) => table.Seats[SeatOf(player)];
        private Vector2 SeatPointFor(int player) => table.SeatPoint(SeatOf(player));

        // ==================================================================
        // Starting a game
        // ==================================================================

        private void ResetTable()
        {
            StopAllCoroutines();
            SetSpeed(0);
            busy = false;
            paused = false;
            botTimer = 0f;
            botWait = -1f;
            pauseUntil = 0f;
            selected.Clear();
            if (resultsLayer != null) Destroy(resultsLayer.gameObject);
            if (pausePanel != null) pausePanel.SetActive(false);
            ClosePileViewer();
            foreach (Transform flying in table.FxRoot) Destroy(flying.gameObject); // cards left mid-flight by a restart
        }

        public void StartNewGame()
        {
            if (IsNetClient) return; // only the host deals
            ResetTable();

            var names = new List<string>();
            var isBot = new List<bool>();
            remoteClients.Clear();
            takenOver.Clear();

            if (IsNetHost)
            {
                for (int i = 0; i < net.Seats.Count; i++)
                {
                    LobbySeat seat = net.Seats[i];
                    names.Add(seat.Name);
                    isBot.Add(seat.IsBot);
                    if (!seat.IsBot && seat.ClientId != net.LocalClientId) remoteClients[i] = seat.ClientId;
                }
                localPlayer = 0;
            }
            else
            {
                int bots = Mathf.Clamp(GameLaunch.BotCount, 1, 9);
                names.Add(GameLaunch.SpectateOnly ? "Bot" : GameLaunch.PlayerName);
                isBot.Add(GameLaunch.SpectateOnly);
                for (int i = 0; i < bots; i++)
                {
                    names.Add(BotNames[i]);
                    isBot.Add(true);
                }
                localPlayer = GameLaunch.SpectateOnly ? -1 : 0;
            }

            int seed = GameLaunch.Seed != 0 ? GameLaunch.Seed : Environment.TickCount;
            Game = PalaceGame.Create(names, isBot, new HouseRules(), seed);
            botRng = new System.Random(seed * 31 + 7);

            BuildTable();
            shownPile.Clear();
            shownDeck = Game.State.TotalCards;
            if (IsNetHost) Broadcast(Game.StartEvents);
            StartCoroutine(DealThenPlay(Game.StartEvents));
        }

        /// <summary>Seats in this screen's order (you at the bottom) plus your card taps and drags.</summary>
        private void BuildTable()
        {
            int n = Game.State.Players.Count;
            var seatNames = new List<string>();
            for (int k = 0; k < n; k++) seatNames.Add(Game.State.Players[PlayerAtSeat(k)].Name);

            IReadOnlyList<SeatView> seats = table.BuildSeats(seatNames);
            for (int k = 0; k < n; k++) seats[k].SetPlayer(seatNames[k], PlayerAtSeat(k)); // same colours on every device
            for (int k = 0; k < HouseRules.TableCards; k++)
            {
                int slot = k;
                CardView up = seats[0].FaceUpCards[k];
                up.Clicked += _ => OnFaceUpTapped(slot);
                up.CanDrag = v => v.CurrentCard.HasValue && BeginDrag(v.CurrentCard.Value, PlaySource.FaceUp);
                up.DragMoved += (v, e) => OnDragging(e);
                up.DragEnded += (v, e) => OnDropped(v, e, PlaySource.FaceUp, slot);

                CardView down = seats[0].FaceDownCards[k];
                down.Clicked += _ => OnFaceDownTapped(slot);
                down.CanDrag = _ => LocalCanAct && Game.State.Players[localPlayer].ActiveSource == PlaySource.FaceDown;
                down.DragMoved += (v, e) => OnDragging(e);
                down.DragEnded += (v, e) => OnDropped(v, e, PlaySource.FaceDown, slot);
            }
            table.FxRoot.SetAsLastSibling();
            ring.SetDirection(Game.State.Direction, false);
        }

        // ==================================================================
        // Main loop: bots act when it's their turn
        // ==================================================================

        private void Update()
        {
            if (IsNetClient && !busy && net.TryTakeUpdate(out var events, out var state)) ApplyHostUpdate(events, state);
            if (IsNetHost && !busy && remoteCommands.Count > 0) HandleRemoteCommand(remoteCommands.Dequeue());

            if (Game == null)
            {
                if (statusText != null) statusText.text = IsNetClient ? "Waiting for the host to deal..." : "";
                return;
            }
            if (statusText != null) UpdateStatus();
            bool singlePlayerPause = !IsNetworked && (paused || viewingPile);
            if (busy || singlePlayerPause || Game.State.Phase == GamePhase.GameOver) return;

            int actor = Game.State.ActingPlayer;
            bool autoPlayLocal = AutoPlayHarness.Active && actor == localPlayer && !HoldAutoPlay; // test builds only

            if (IsNetClient)
            {
                // The host plays the bots. A client only acts for itself (and only in automated tests).
                if (autoPlayLocal && !awaitingHost && BotReady()) Submit(ToCommand(BotBrain.Decide(Game, actor, botRng)));
                return;
            }

            bool computerPlays = Game.State.Players[actor].IsBot || takenOver.Contains(actor) || autoPlayLocal;
            if (!computerPlays || !BotReady()) return; // otherwise waiting for a person (here or on another device)

            BotMove move = BotBrain.Decide(Game, actor, botRng);
            Execute(BotBrain.Execute(Game, actor, move));
        }

        /// <summary>True once a computer player has "thought" for long enough.</summary>
        private bool BotReady()
        {
            if (Time.unscaledTime < pauseUntil) return false; // giving everyone a moment to think
            if (botWait < 0f) botWait = BotDelay * UnityEngine.Random.Range(0.8f, 1.25f);
            botTimer += Time.unscaledDeltaTime;
            if (botTimer < botWait) return false;
            botTimer = 0f;
            botWait = -1f;
            return true;
        }

        private NetCommand PlayCommand()
        {
            var c = new NetCommand { Type = NetCommandType.PlayCards, Source = selectedSource };
            c.CardIds.AddRange(selected);
            return c;
        }

        private static NetCommand ToCommand(BotMove m)
        {
            var c = new NetCommand { Source = m.Source, Slot = m.Slot, Call = m.Call, Burn = m.Burn, AceValue = m.AceValue };
            if (m.CardIds != null) c.CardIds.AddRange(m.CardIds);
            switch (m.Type)
            {
                case BotMoveType.PlayCards: c.Type = NetCommandType.PlayCards; break;
                case BotMoveType.PlayFaceDown: c.Type = NetCommandType.PlayFaceDown; break;
                case BotMoveType.PickUp: c.Type = NetCommandType.PickUp; break;
                case BotMoveType.DeclareSeven: c.Type = NetCommandType.DeclareSeven; break;
                default: c.Type = NetCommandType.ChooseAce; break;
            }
            return c;
        }

        /// <summary>
        /// A move by the person at this device. In single player (or as host) it is applied
        /// straight away; as a client it is sent to the host, which checks it and replies.
        /// </summary>
        private bool Submit(NetCommand command)
        {
            if (IsNetClient)
            {
                if (awaitingHost) return false;
                awaitingHost = true;
                net.SendCommand(command);
                selected.Clear();
                HidePrompts();
                RefreshAll();
                return true;
            }
            return Execute(command.ApplyTo(Game, localPlayer));
        }

        private float BotDelay => AutoPlayHarness.Active ? 0.02f : GameSettings.BotThinkSeconds / Tween.SpeedMultiplier;

        // ------------------------------------------------------------------ fast-forward

        private static readonly float[] Speeds = { 1f, 2f, 4f };
        private Button speedButton;
        private int speedIndex;

        /// <summary>Only offered once you have finished (or are watching), so you never wait ages for the bots.</summary>
        private bool CanFastForward =>
            !IsNetworked && Game != null && Game.State.Phase != GamePhase.GameOver &&
            (localPlayer < 0 || Game.State.Players[localPlayer].IsFinished);

        private void CycleSpeed()
        {
            SetSpeed((speedIndex + 1) % Speeds.Length);
        }

        private void SetSpeed(int index)
        {
            speedIndex = index;
            Tween.SpeedMultiplier = Speeds[index];
            if (speedButton != null) UIKit.SetButtonLabel(speedButton, "Speed " + Speeds[index] + "x");
        }

        private bool Execute(CommandResult result)
        {
            if (!result.Ok)
            {
                Toast(result.Error);
                return false;
            }
            selected.Clear();
            HidePrompts();
            busy = true;
            botTimer = 0f;
            botWait = -1f;
            if (IsNetHost) Broadcast(result.Events);
            StartCoroutine(PlayEvents(result.Events));
            return true;
        }

        // ==================================================================
        // Drag and drop onto the pile
        // ==================================================================

        /// <summary>A drag is starting on one of the player's cards. Allowed only for a playable card.</summary>
        private bool BeginDrag(Card card, PlaySource source)
        {
            if (!LocalCanAct || Game.State.Players[localPlayer].ActiveSource != source) return false;
            PlayCheck check = Game.CheckRank(card.Rank);
            if (!check.Legal)
            {
                Toast(check.Reason);
                return false;
            }

            // The dragged card joins the selection (cards of the same rank already selected go with it).
            if (!selected.Contains(card.Id))
            {
                if (selected.Count > 0 && (selectedSource != source || FindSelectedRank() != card.Rank)) selected.Clear();
                selected.Add(card.Id);
                selectedSource = source;
                AudioManager.Play(Sfx.Select, 0.6f);
                RefreshAll();
            }
            return true;
        }

        private void OnDragging(UnityEngine.EventSystems.PointerEventData e) => table.SetDropHighlight(IsOverPile(e));

        private void OnDropped(CardView view, UnityEngine.EventSystems.PointerEventData e, PlaySource source, int slot)
        {
            table.SetDropHighlight(false);
            bool played = false;
            if (IsOverPile(e) && LocalCanAct)
            {
                played = source == PlaySource.FaceDown
                    ? Submit(new NetCommand { Type = NetCommandType.PlayFaceDown, Slot = slot })
                    : selected.Count > 0 && Submit(PlayCommand());
            }

            if (played)
            {
                // Hand cards are re-laid out by HandView (and the view may already show another card),
                // so only table cards need putting back in their slot.
                if (source != PlaySource.Hand) view.SnapHome();
            }
            else
            {
                view.ReturnHome();
                if (source == PlaySource.Hand) RefreshAll();
            }
        }

        /// <summary>Generous drop zone around the discard pile, so it's easy to hit with a thumb.</summary>
        private bool IsOverPile(UnityEngine.EventSystems.PointerEventData e)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)table.transform, e.position,
                       e.pressEventCamera, out Vector2 p)
                   && (p - table.DiscardPoint).magnitude < 280f;
        }

        // ==================================================================
        // Local player input
        // ==================================================================

        private bool LocalCanAct =>
            localPlayer >= 0 && !busy && !awaitingHost && (!paused || IsNetworked) &&
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
            Submit(new NetCommand { Type = NetCommandType.PlayFaceDown, Slot = slot });
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
            Submit(PlayCommand());
        }

        private void OnPickUpPressed()
        {
            if (!LocalCanAct) return;
            Submit(new NetCommand { Type = NetCommandType.PickUp });
        }

        private void OnSevenCall(SevenCall call)
        {
            if (busy || Game.State.Phase != GamePhase.AwaitingSevenCall || Game.State.PendingPlayer != localPlayer) return;
            Submit(new NetCommand { Type = NetCommandType.DeclareSeven, Call = call });
        }

        private void OnAceChoice(bool burn, Rank value)
        {
            if (busy || Game.State.Phase != GamePhase.AwaitingAceChoice || Game.State.PendingPlayer != localPlayer) return;
            Submit(new NetCommand { Type = NetCommandType.ChooseAce, Burn = burn, AceValue = value });
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
                SeatView seat = SeatFor(i);
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
            SevenCall seven = Game.ActiveSeven;
            string effect = seven == SevenCall.Higher ? "Next: HIGHER than 7" : seven == SevenCall.Lower ? "Next: LOWER than 7" : null;
            if (effect == null && s.TopRank == Rank.Ace && s.AceValue.HasValue && !busy)
                effect = "Ace counts as " + Card.RankToLabel(s.AceValue.Value);
            table.SetEffect(effect);

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
            speedButton.gameObject.SetActive(CanFastForward);

            bool showAce = myChoice && s.Phase == GamePhase.AwaitingAceChoice;
            if (showAce && !acePanel.activeSelf) BuildAceButtons();
            acePanel.SetActive(showAce);
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

            if (Time.unscaledTime < noticeUntil && !string.IsNullOrEmpty(noticeMessage) && Game.State.ActingPlayer != localPlayer)
            {
                statusText.text = noticeMessage;
                statusText.color = UIKit.Gold;
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
            if (s.Phase == GamePhase.AwaitingAceChoice) { statusText.text = "Choose what your Ace counts as"; return; }
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

        private IEnumerator DealThenPlay(IReadOnlyList<GameEvent> startEvents)
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
                    SeatView seat = SeatFor(i);
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
            yield return PlayEvents(startEvents);
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

            // Make sure the table shows exactly the real state now the animations are done.
            shownPile.Clear();
            shownPile.AddRange(Game.State.Discard);
            shownDeck = Game.State.DrawPile.Count;

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
            SeatView seat = SeatFor(e.Player);
            if (e.Source == PlaySource.Hand)
            {
                if (e.Player == localPlayer)
                {
                    CardView v = hand.Find(e.Cards[index].Id);
                    if (v != null) return (table.ToLocal(v.transform), 1f);
                    return (table.HandCenter, 1f);
                }
                return (SeatPointFor(e.Player), ScaleOf(seat.transform) * SeatView.TableCardScale);
            }

            int slot = e.Slots.Count > index ? e.Slots[index] : 0;
            Transform t = e.Source == PlaySource.FaceUp ? seat.FaceUpCards[slot].transform : seat.FaceDownCards[slot].transform;
            return (table.ToLocal(t), ScaleOf(t));
        }

        private IEnumerator AnimateEvent(GameEvent e, Dictionary<int, (Vector2 pos, float scale)> origins, HashSet<int> onPile)
        {
            GameState s = Game.State;
            EventStarted?.Invoke(e);
            string who = e.Player >= 0 && e.Player < s.Players.Count ? s.Players[e.Player].Name : "";
            Vector2 centre = table.DiscardPoint + new Vector2(-120f, 210f); // above the "Next:" banner, below the top seats

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
                        (Vector2 pos, float scale) from = origins.TryGetValue(card.Id, out var o) ? o : (SeatPointFor(e.Player), 0.5f);
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
                    Vector2 to = e.Player == localPlayer ? table.HandCenter : SeatPointFor(e.Player);
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
                    Vector2 to = e.Player == localPlayer ? table.HandCenter : SeatPointFor(e.Player);
                    int n = e.Cards.Count;
                    string what = (e.Player == localPlayer ? "You pick up " : who + " picks up ") + n + (n == 1 ? " card" : " cards");
                    AudioManager.Play(Sfx.PickUp);
                    table.Announce(what, new Color(1f, 0.75f, 0.6f), centre, 52f);

                    // Cards peel off the pile one after another so the size of the pick-up is obvious.
                    int shown = Mathf.Min(10, shownPile.Count);
                    for (int i = 0; i < shown; i++)
                    {
                        Card c = shownPile[shownPile.Count - 1];
                        shownPile.RemoveAt(shownPile.Count - 1);
                        table.SetDiscardPile(shownPile);
                        StartCoroutine(FlyCard(c, table.DiscardPoint, to, table.PileCardScale, 0.5f, 0.4f, null));
                        yield return Tween.Wait(Tween.Scaled(0.06f));
                    }
                    shownPile.Clear();
                    table.SetDiscardPile(shownPile);
                    yield return Tween.Wait(Tween.Scaled(0.45f));

                    // Give everyone a moment to take it in before the computer plays on.
                    if (!AutoPlayHarness.Active)
                    {
                        pauseUntil = Time.unscaledTime + GameSettings.PickUpPauseSeconds / Tween.SpeedMultiplier;
                        noticeMessage = what + ". Take a moment...";
                        noticeUntil = pauseUntil;
                    }
                    break;
                }

                case GameEventType.PileBurned:
                {
                    // The top cards of the pile spread out and burn away; the rest just vanish.
                    var burning = shownPile.Skip(Mathf.Max(0, shownPile.Count - 6)).ToList();
                    shownPile.Clear();
                    table.SetDiscardPile(shownPile);
                    AudioManager.Play(Sfx.Burn);
                    table.Announce("BURN!", new Color(1f, 0.55f, 0.15f), centre, 84f);
                    yield return fx.Burn(burning, table.DiscardPoint, table.PileCardScale);
                    break;
                }

                case GameEventType.PlayersSkipped:
                {
                    AudioManager.Play(Sfx.Skip);
                    table.Announce(e.Count > 1 ? "SKIP x" + e.Count : "SKIP", new Color(0.6f, 0.85f, 1f), centre, 72f);
                    float delay = 0f;
                    foreach (int p in e.Players)
                    {
                        fx.SkipIcon(SeatPointFor(p), delay);
                        delay += 0.18f;
                    }
                    yield return Tween.Wait(Tween.Scaled(0.6f) + delay * GameSettings.AnimationScale);
                    break;
                }

                case GameEventType.DirectionReversed:
                    AudioManager.Play(Sfx.Reverse);
                    table.Announce(e.Count % 2 == 1 ? "REVERSE" : "DOUBLE REVERSE", new Color(0.85f, 0.7f, 1f), centre, 72f);
                    table.SetDirection(e.Direction);
                    ring.SetDirection(e.Direction, true);
                    fx.Ripple(table.DiscardPoint, new Color(0.8f, 0.65f, 1f, 0.9f));
                    yield return Tween.Wait(Tween.Scaled(0.8f));
                    break;

                case GameEventType.Reset:
                    AudioManager.Play(Sfx.Reset);
                    table.Announce("RESET", new Color(0.65f, 1f, 0.7f), centre, 72f);
                    fx.Ripple(table.DiscardPoint, new Color(0.6f, 1f, 0.7f, 0.9f));
                    fx.Ripple(table.DiscardPoint, new Color(0.6f, 1f, 0.7f, 0.6f), 0.15f);
                    yield return Tween.Wait(Tween.Scaled(0.5f));
                    break;

                case GameEventType.SevenDeclared:
                    table.Announce(e.Call == SevenCall.Higher ? "HIGHER than 7" : "LOWER than 7", UIKit.Gold, centre, 64f);
                    fx.SevenArrow(table.DiscardPoint, e.Call == SevenCall.Higher);
                    AudioManager.Play(Sfx.Skip, 0.5f);
                    yield return Tween.Wait(Tween.Scaled(0.8f));
                    break;

                case GameEventType.AceChosen:
                    if (!e.Burn)
                    {
                        table.Announce("ACE = " + RankName(e.AceValue), UIKit.Gold, centre, 64f);
                        fx.Ripple(table.DiscardPoint, new Color(1f, 0.85f, 0.4f, 0.8f));
                        yield return Tween.Wait(Tween.Scaled(0.5f));
                    }
                    break;

                case GameEventType.PlayerFinished:
                {
                    bool last = e.Position == s.Players.Count;
                    string text = e.Player == localPlayer
                        ? (last ? "You're the Shedhead!" : "You finish " + SeatView.Ordinal(e.Position) + "!")
                        : who + (last ? " is the Shedhead!" : " finishes " + SeatView.Ordinal(e.Position) + "!");
                    table.Announce(text, UIKit.Gold, table.DiscardPoint + new Vector2(-120f, -10f), 56f);
                    if (e.Position == 1 || e.Player == localPlayer) AudioManager.Play(Sfx.Win, 0.8f);
                    RefreshAll();
                    yield return Tween.Wait(Mathf.Max(0.4f, Tween.Scaled(0.9f)));
                    break;
                }

                case GameEventType.TurnChanged:
                    if (e.Player == localPlayer && s.Phase == GamePhase.Playing && Game.State.Current == localPlayer)
                    {
                        AudioManager.Play(Sfx.YourTurn, 0.6f);
                        table.Announce("YOUR TURN", UIKit.Gold, new Vector2(735f, -312f), 44f); // just above the status text, clear of your cards
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

        private static string RankName(Rank rank)
        {
            switch (rank)
            {
                case Rank.Jack: return "JACK";
                case Rank.Queen: return "QUEEN";
                case Rank.King: return "KING";
                case Rank.Ace: return "ACE";
                default: return ((int)rank).ToString();
            }
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
            var ace = UIKit.Panel(tableHud, new Vector2(1000f, 320f), new Vector2(0f, 20f));
            aceTitle = UIKit.Label(ace.transform, "", 28f, new Vector2(940f, 50f),
                new Vector2(0f, 115f), TextAlignmentOptions.Center, UIKit.Text, true);
            aceButtons = UIBuild.Stretch("AceButtons", ace.transform);
            acePanel = ace.gameObject;
            acePanel.SetActive(false);

            // Tap the pile (or the button beside it) to look through it.
            var pileTap = UIBuild.Image("PileTap", tableHud, null, new Color(0f, 0f, 0f, 0f),
                new Vector2(CardView.Width, CardView.Height) * table.PileCardScale + new Vector2(20f, 20f), table.DiscardPoint);
            pileTap.raycastTarget = true;
            pileTap.gameObject.AddComponent<Button>().onClick.AddListener(OpenPileViewer);
            UIKit.Button(tableHud, "View pile", new Vector2(140f, 52f), table.DiscardPoint + new Vector2(150f, 0f), OpenPileViewer, false, 22f);

            // Menu button + pause menu live in the safe-area HUD.
            var menu = UIKit.Button(hudRoot, "Menu", new Vector2(150f, 64f), Vector2.zero, TogglePause, false, 26f);
            var mrt = (RectTransform)menu.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 1f);
            mrt.anchoredPosition = new Vector2(-95f, -48f);

            speedButton = UIKit.Button(hudRoot, "Speed 1x", new Vector2(170f, 64f), Vector2.zero, CycleSpeed, true, 26f);
            var srt = (RectTransform)speedButton.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
            srt.anchoredPosition = new Vector2(-280f, -48f);
            speedButton.gameObject.SetActive(false);

            pausePanel = UIKit.Layer(hudRoot, "PauseMenu", UIKit.Scrim).gameObject;
            var panel = UIKit.Panel(pausePanel.transform, new Vector2(520f, 470f), Vector2.zero);
            UIKit.Label(panel.transform, "Paused", 48f, new Vector2(480f, 70f), new Vector2(0f, 165f), TextAlignmentOptions.Center, UIKit.Gold, true);
            UIKit.Button(panel.transform, "Resume", new Vector2(380f, 76f), new Vector2(0f, 65f), TogglePause, true);
            restartButton = UIKit.Button(panel.transform, "Restart", new Vector2(380f, 76f), new Vector2(0f, -25f), StartNewGame);
            leaveButton = UIKit.Button(panel.transform, "Main Menu", new Vector2(380f, 76f), new Vector2(0f, -115f), GoToMainMenu);
            pausePanel.SetActive(false);

            HidePrompts();
        }

        // ------------------------------------------------------------------ pile viewer

        private RectTransform pileViewer;
        private bool viewingPile;

        /// <summary>Shows every card in the discard pile (newest first) plus a count of each rank.</summary>
        private void OpenPileViewer()
        {
            if (pileViewer != null || Game == null) return;
            viewingPile = true;
            AudioManager.Play(Sfx.CardDraw, 0.6f);

            pileViewer = UIKit.Layer(hudRoot, "PileViewer", UIKit.Scrim);
            var panel = UIKit.Panel(pileViewer, new Vector2(1560f, 920f), Vector2.zero, new Color(0.04f, 0.06f, 0.05f, 0.98f));
            int n = shownPile.Count;

            UIKit.Label(panel.transform, n == 0 ? "The pile is empty" : "The pile: " + n + (n == 1 ? " card" : " cards"),
                48f, new Vector2(1400f, 70f), new Vector2(0f, 392f), TextAlignmentOptions.Center, UIKit.Gold, true);

            string info = "Burned so far: " + Game.State.BurnedCount + "     Left in deck: " + shownDeck;
            if (n > 0)
            {
                Card top = shownPile[n - 1];
                string topText = "Top card: " + RankName(top.Rank).ToLowerInvariant();
                if (top.Rank == Rank.Ace && Game.State.AceValue.HasValue) topText += " (counts as " + Card.RankToLabel(Game.State.AceValue.Value) + ")";
                info = topText + "     Next card must be " + Game.Requirement() + "     " + info;
            }
            UIKit.Label(panel.transform, info, 24f, new Vector2(1480f, 40f), new Vector2(0f, 340f), TextAlignmentOptions.Center, UIKit.Muted);

            if (n > 0)
            {
                // Scrollable grid of the pile, newest (top) card first.
                var view = UIBuild.Image("Cards", panel.transform, null, new Color(0f, 0f, 0f, 0.25f), new Vector2(1480f, 580f), new Vector2(0f, 20f));
                view.raycastTarget = true;
                view.gameObject.AddComponent<RectMask2D>();

                var content = UIBuild.Rect("Content", view.transform, Vector2.zero, Vector2.zero);
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.offsetMin = Vector2.zero;
                content.offsetMax = Vector2.zero;
                var grid = content.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(84f, 118f);
                grid.spacing = new Vector2(8f, 12f);
                grid.padding = new RectOffset(20, 20, 18, 18);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 15;
                grid.childAlignment = TextAnchor.UpperCenter;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                for (int i = n - 1; i >= 0; i--)
                {
                    var cell = UIBuild.Rect("Cell", content, new Vector2(84f, 118f), Vector2.zero);
                    if (i == n - 1)
                        UIBuild.Image("TopGlow", cell, FxSprites.Dot, new Color(1f, 0.8f, 0.3f, 0.8f), new Vector2(150f, 190f), Vector2.zero);
                    CardView card = CardFactory.Spawn(table.CardPrefab, table.CardTheme, cell);
                    card.SetInteractable(false);
                    card.ShowCard(shownPile[i], true);
                    ((RectTransform)card.transform).localScale = Vector3.one * 0.6f;
                }

                var scroll = view.gameObject.AddComponent<ScrollRect>();
                scroll.content = content;
                scroll.viewport = view.rectTransform;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 40f;

                // How many of each rank are in the pile (useful for planning).
                var counts = shownPile.GroupBy(c => c.Rank).OrderByDescending(g => g.Key)
                    .Select(g => "<b>" + Card.RankToLabel(g.Key) + "</b> x" + g.Count());
                UIKit.Label(panel.transform, "In the pile:   " + string.Join("     ", counts), 28f, new Vector2(1480f, 50f),
                    new Vector2(0f, -310f), TextAlignmentOptions.Center, UIKit.Text);
            }

            UIKit.Button(panel.transform, "Close", new Vector2(260f, 74f), new Vector2(0f, -392f), ClosePileViewer, true);
        }

        public void TestOpenPileViewer() => OpenPileViewer();   // used by the automated test harness
        public void TestClosePileViewer() => ClosePileViewer();

        private void ClosePileViewer()
        {
            if (pileViewer != null) Destroy(pileViewer.gameObject);
            pileViewer = null;
            viewingPile = false;
        }

        /// <summary>One button per value the Ace may stand for, plus "Burn" when allowed.</summary>
        private void BuildAceButtons()
        {
            foreach (Transform child in aceButtons) Destroy(child.gameObject);

            List<Rank> values = Game.State.PendingAceValues;
            int n = values.Count;
            bool lowOnly = n > 0 && values.All(v => (int)v < 7);
            aceTitle.text = lowOnly ? "Your Ace counts as... (LOWER than 7 was called)" : "Your Ace counts as... (it must match or beat the pile)";
            float step = n > 0 ? Mathf.Min(110f, 920f / n) : 0f;
            for (int i = 0; i < n; i++)
            {
                Rank value = values[i];
                UIKit.Button(aceButtons, Card.RankToLabel(value), new Vector2(step - 10f, 86f),
                    new Vector2((i - (n - 1) * 0.5f) * step, 20f), () => OnAceChoice(false, value), true, 36f);
            }

            if (Game.State.PendingAceBurnAllowed)
            {
                var burn = UIKit.Button(aceButtons, "BURN THE PILE (Ace as 10)", new Vector2(460f, 72f), new Vector2(0f, -95f),
                    () => OnAceChoice(true, Rank.Ace), false, 28f);
                ((Image)burn.targetGraphic).color = new Color(0.62f, 0.2f, 0.08f, 0.97f);
            }
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

        private Button restartButton, leaveButton;

        /// <summary>In multiplayer the game keeps going for everyone else while this menu is open.</summary>
        private void TogglePause()
        {
            paused = !paused;
            pausePanel.SetActive(paused);
            pausePanel.transform.SetAsLastSibling();
            restartButton.gameObject.SetActive(!IsNetClient);
            UIKit.SetButtonLabel(leaveButton, IsNetworked ? "Leave game" : "Main Menu");
        }

        private void GoToMainMenu()
        {
            if (IsNetworked) net.Leave();
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
                string suffix = i == n - 1 ? "   - Shedhead!" : "";
                UIKit.Label(panel.transform, SeatView.Ordinal(i + 1), 30f, new Vector2(120f, 48f), new Vector2(-230f, y), TextAlignmentOptions.Left, c, true);
                UIKit.Label(panel.transform, s.Players[p].Name + suffix, 30f, new Vector2(420f, 48f), new Vector2(60f, y), TextAlignmentOptions.Left, c, i == 0);
            }

            if (IsNetClient)
                UIKit.Label(panel.transform, "Waiting for the host...", 26f, new Vector2(300f, 80f), new Vector2(-165f, -top + 65f),
                    TextAlignmentOptions.Center, UIKit.Muted);
            else
                UIKit.Button(panel.transform, "Play Again", new Vector2(300f, 80f), new Vector2(-165f, -top + 65f), StartNewGame, true);
            UIKit.Button(panel.transform, IsNetworked ? "Leave game" : "Main Menu", new Vector2(300f, 80f), new Vector2(165f, -top + 65f), GoToMainMenu);
            AudioManager.Play(Sfx.Win);
        }
    }
}
