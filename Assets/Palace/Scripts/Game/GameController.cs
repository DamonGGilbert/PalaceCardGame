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
        public bool TipShowing => coach != null && coach.Showing;     // tests only
        public void TestDismissTip() => coach?.Dismiss();

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
            hand.CardDragged += (v, e) => { OnDragging(e); DragFollowers(v); }; // other selected cards travel with it
            hand.CardDropped += (v, e) => OnDropped(v, e, PlaySource.Hand, -1);

            // Equipped cosmetics: table, deck, card back, effects (banner + name style are applied per seat).
            Cosmetics.ApplyThemes(tableTheme, cardTheme);
            table.ApplySkin();
            fx = TableFx.Attach(table);
            fx.Style = Cosmetics.EffectStyle;
            ring = DirectionRing.Create((RectTransform)table.transform, table.TableCenter, table.FeltSiblingIndex + 1);
            ring.SetStyle(fx.Style);
            BuildHud();
            Progress.Celebrate += OnCelebrate;

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
                net.RematchRequested += OnRematchRequested;
                net.ReactionReceived += OnNetReaction;
                StartCoroutine(HostWaitThenStart());
            }
            else
            {
                botRng = new System.Random(Environment.TickCount);
                net.RematchRequested += OnRematchRequested;
                net.ReactionReceived += OnNetReaction;
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
            if (Game.State.ActingPlayer != player && Game.State.Phase != GamePhase.Swapping) // everyone swaps at once
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
            Game = new PalaceGame(state, net.HostRules ?? new HouseRules(), 0); // the host's game mode

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
                if (Game != null) Toast(Loc.F("{0} left. A computer player takes over.", Game.State.Players[kv.Key].Name));
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
            Progress.Celebrate -= OnCelebrate;
            Tween.SpeedMultiplier = 1f; // never carry fast-forward into the menu
            if (net == null) return;
            net.Disconnected -= OnNetDisconnected;
            net.ErrorReceived -= OnNetError;
            net.CommandReceived -= OnRemoteCommand;
            net.ClientInGame -= OnClientInGame;
            net.ClientLeft -= OnClientLeft;
            net.RematchRequested -= OnRematchRequested;
            net.ReactionReceived -= OnNetReaction;
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

        // ------------------------------------------------------------------ what each player wears

        private sealed class PlayerLook
        {
            public BannerSkin Banner;
            public NameStyle NameStyle;
            public FontSkin Font;
            public FxStyle Effect = FxStyle.Classic;
            public CelebrationStyle Celebration = CelebrationStyle.Confetti;
        }

        private readonly List<PlayerLook> looks = new List<PlayerLook>();

        private FxStyle FxOf(int player) => player >= 0 && player < looks.Count ? looks[player].Effect : Cosmetics.EffectStyle;
        private CelebrationStyle CelebrationOf(int player) =>
            player >= 0 && player < looks.Count ? looks[player].Celebration : CelebrationStyle.Confetti;

        /// <summary>
        /// You wear your own choices; friends online wear theirs (sent through the game room); computer
        /// players wear a random but steady mix, which also shows off what's in the Customise screen.
        /// </summary>
        private PlayerLook LookFor(int player)
        {
            var lib = CosmeticLibrary.Instance;
            if (player == localPlayer || lib == null)
            {
                return player == localPlayer ? new PlayerLook
                {
                    Banner = Cosmetics.Banner, NameStyle = Cosmetics.NameStyle, Font = Cosmetics.Font, Effect = Cosmetics.EffectStyle,
                    Celebration = Cosmetics.Celebration != null ? Cosmetics.Celebration.style : CelebrationStyle.Confetti
                } : new PlayerLook();
            }

            if (IsNetworked && player < net.Seats.Count && !net.Seats[player].IsBot)
            {
                LobbySeat seat = net.Seats[player];
                var fx = lib.Find(CosmeticCategory.Effects, seat.Effect) as EffectSkin;
                var win = lib.Find(CosmeticCategory.Celebration, seat.Celebration) as CelebrationSkin;
                return new PlayerLook
                {
                    Banner = lib.Find(CosmeticCategory.Banner, seat.Banner) as BannerSkin,
                    NameStyle = lib.Find(CosmeticCategory.NameStyle, seat.NameStyle) as NameStyle,
                    Font = lib.Find(CosmeticCategory.Font, seat.Font) as FontSkin,
                    Effect = fx != null ? fx.style : FxStyle.Classic,
                    Celebration = win != null ? win.style : CelebrationStyle.Confetti
                };
            }

            if (!Game.State.Players[player].IsBot) return new PlayerLook();

            // A computer player: the same look every game (seeded by its name), about half of it premium.
            int seed = 17;
            foreach (char ch in Game.State.Players[player].Name) seed = seed * 31 + ch;
            var rng = new System.Random(seed);
            T Pick<T>(List<T> items) where T : CosmeticItem
            {
                var choices = items.Where(i => !i.storeOnly).ToList();
                if (choices.Count == 0) return null;
                return rng.NextDouble() < 0.5 ? choices[0] : choices[rng.Next(choices.Count)];
            }
            var botFx = Pick(lib.effects);
            var botWin = Pick(lib.celebrations);
            return new PlayerLook
            {
                Banner = Pick(lib.banners), NameStyle = Pick(lib.nameStyles), Font = Pick(lib.fonts),
                Effect = botFx != null ? botFx.style : FxStyle.Classic,
                Celebration = botWin != null ? botWin.style : CelebrationStyle.Confetti
            };
        }

        // Computer players each have their own profile picture: cat, ghost, flame, moon, sun, clover...
        private static readonly int[] BotPictures = { 10, 11, 6, 8, 9, 12, 7, 13, 2 };

        /// <summary>A player's profile picture (AvatarArt index), or -1 for their initial.</summary>
        private int PictureOf(int player)
        {
            if (player == localPlayer) return GameSettings.AvatarIfUnlocked;
            if (IsNetworked && player < net.Seats.Count)
            {
                LobbySeat seat = net.Seats[player];
                if (!seat.IsBot) return seat.Avatar;
            }
            if (!Game.State.Players[player].IsBot) return -1;
            int botNumber = Game.State.Players.Take(player).Count(p => p.IsBot);
            return BotPictures[botNumber % BotPictures.Length];
        }
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
            coach?.Dismiss();
            coach = null;
            swapSlot = -1;
            rematchNames.Clear();
            rematchAsked = false;
            rematchLabel = null;
            foreach (var b in bubbles.Values) if (b != null) Destroy(b);
            bubbles.Clear();
            if (reactPanel != null) ToggleReactPanel();
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
                // Different computer names every game (never the same as yours).
                string[] botNames = BotNames.Pick(bots, new System.Random(GameLaunch.Seed != 0 ? GameLaunch.Seed : Environment.TickCount), GameLaunch.PlayerName);
                names.Add(GameLaunch.SpectateOnly ? "Bot" : GameLaunch.PlayerName);
                isBot.Add(GameLaunch.SpectateOnly);
                for (int i = 0; i < bots; i++)
                {
                    names.Add(botNames[i]);
                    isBot.Add(true);
                }
                localPlayer = GameLaunch.SpectateOnly ? -1 : 0;
            }

            int seed = GameLaunch.Seed != 0 ? GameLaunch.Seed : Environment.TickCount;
            bool tutorial = GameLaunch.Tutorial && !IsNetworked && !GameLaunch.SpectateOnly;
            if (tutorial)
            {
                // The guided first game: you and two easy computer players, with a coach.
                Game = TutorialSetup.Create(GameLaunch.PlayerName, new HouseRules(), seed);
                localPlayer = 0;
                coach = new TutorialCoach(this, hudRoot, localPlayer);
                botLevel = BotLevel.Easy;
            }
            else
            {
                // The game mode: your Single Player choice, or the host's setting online.
                HouseRules rules = IsNetHost ? GameModes.BuildRules() : (GameLaunch.Rules ?? new HouseRules());
                if (!IsNetHost) rules.SwapBeforePlay = GameLaunch.SwapMode && !GameLaunch.SpectateOnly;
                Game = PalaceGame.Create(names, isBot, rules, seed);
                botLevel = IsNetworked ? BotLevel.Normal : (BotLevel)Mathf.Clamp(GameLaunch.BotLevel, 0, 2);
            }
            isTutorial = tutorial;
            botRng = new System.Random(seed * 31 + 7);

            BuildTable();
            shownPile.Clear();
            shownDeck = Game.State.TotalCards;
            if (IsNetHost) Broadcast(Game.StartEvents);
            StartCoroutine(DealThenPlay(Game.StartEvents));
        }

        /// <summary>Seats in this screen's order (you at the bottom) plus your card taps and drags.</summary>
        private readonly Dictionary<int, int> lastTotals = new Dictionary<int, int>(); // cards each player had last refresh

        private void BuildTable()
        {
            lastTotals.Clear();
            int n = Game.State.Players.Count;
            var seatNames = new List<string>();
            for (int k = 0; k < n; k++) seatNames.Add(Game.State.Players[PlayerAtSeat(k)].Name);

            IReadOnlyList<SeatView> seats = table.BuildSeats(seatNames);
            for (int k = 0; k < n; k++)
            {
                seats[k].SetPlayer(seatNames[k], PlayerAtSeat(k)); // same colours on every device
                seats[k].SetPicture(AvatarArt.Get(PictureOf(PlayerAtSeat(k))));
            }
            // Everyone's look: your Customise choices, your friends' choices online, and a random mix for computers.
            looks.Clear();
            for (int p = 0; p < n; p++) looks.Add(LookFor(p));
            for (int k = 0; k < n; k++)
            {
                PlayerLook look = looks[PlayerAtSeat(k)];
                seats[k].ApplyFont(look.Font); // font first: outlines are built on the font that is showing
                if (look.Banner != null) seats[k].ApplyBanner(look.Banner);
                if (look.NameStyle != null) seats[k].ApplyNameStyle(look.NameStyle);
            }
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
                if (statusText != null) statusText.text = IsNetClient ? Loc.T("Waiting for the host to deal...") : "";
                return;
            }
            if (statusText != null) UpdateStatus();
            HandleKeyboard();
            coach?.Tick();
            bool singlePlayerPause = !IsNetworked && (paused || viewingPile || (coach != null && coach.Holding));
            if (busy || singlePlayerPause || Game.State.Phase == GamePhase.GameOver) return;
            if (Game.State.Phase == GamePhase.Swapping)
            {
                SwapPhaseComputers();
                return;
            }

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

            // A player who left (and autoplay tests) get a Normal bot; real bots play at the chosen level.
            BotLevel level = Game.State.Players[actor].IsBot ? botLevel : BotLevel.Normal;
            BotMove move = BotBrain.Decide(Game, actor, botRng, level);
            Execute(BotBrain.Execute(Game, actor, move));
        }

        // ------------------------------------------------------------------ cards in the air
        // Cards being drawn or picked up only appear in a hand (and in a hand count) once they land.

        private readonly HashSet<int> arriving = new HashSet<int>();                 // your cards still flying in
        private readonly Dictionary<int, int> pendingCards = new Dictionary<int, int>(); // player -> cards still flying in

        private void NoteArrivals(IReadOnlyList<GameEvent> events)
        {
            arriving.Clear();
            pendingCards.Clear();
            foreach (var e in events)
            {
                if (e.Type != GameEventType.CardsDrawn && e.Type != GameEventType.PileCollected && e.Type != GameEventType.FaceUpTaken) continue;
                pendingCards[e.Player] = (pendingCards.TryGetValue(e.Player, out int n) ? n : 0) + e.Cards.Count;
                if (e.Player == localPlayer) foreach (var c in e.Cards) arriving.Add(c.Id);
            }
        }

        /// <summary>One card has reached a hand: show it there (with a little pop if it's yours).</summary>
        private void Landed(int player, Card card)
        {
            if (pendingCards.TryGetValue(player, out int n)) pendingCards[player] = Mathf.Max(0, n - 1);
            bool mine = player == localPlayer && arriving.Remove(card.Id);
            RefreshAll();
            if (!mine || GameSettings.AnimationScale <= 0f) return;
            CardView view = hand.Find(card.Id);
            if (view != null) StartCoroutine(PopIn(view.transform));
        }

        /// <summary>Everything in this event has landed (also covers cards that didn't get their own flight).</summary>
        private void AllLanded(GameEvent e)
        {
            pendingCards.Remove(e.Player);
            if (e.Player == localPlayer) foreach (var c in e.Cards) arriving.Remove(c.Id);
            RefreshAll();
        }

        private static IEnumerator PopIn(Transform t)
        {
            yield return Tween.Run(0.22f, k => { if (t != null) t.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, UIMotion.BackOut(k)); }, x => x);
            if (t != null) t.localScale = Vector3.one;
        }

        /// <summary>Was this burn four of a kind (rather than a 10 or an Ace)?</summary>
        private bool IsFourOfAKind(List<Card> burned)
        {
            int n = burned.Count;
            if (n < 4 || !Game.Rules.FourOfAKindBurns) return false;
            Rank r = burned[n - 1].Rank;
            if (r == Rank.Ten) return false; // a 10 burns anyway
            for (int i = n - 4; i < n; i++) if (burned[i].Rank != r) return false;
            return true;
        }

        private BotLevel botLevel = BotLevel.Normal;
        private bool isTutorial;
        private TutorialCoach coach;

        // ==================================================================
        // Swap before play (optional mode)
        // ==================================================================

        private Button readyButton;
        private int swapSlot = -1; // a face-up card picked first, waiting for a hand card

        /// <summary>The local player may still swap cards.</summary>
        private bool LocalCanSwap =>
            localPlayer >= 0 && !busy && !awaitingHost && Game != null && Game.State.Phase == GamePhase.Swapping
            && !Game.State.Players[localPlayer].SwapReady;

        /// <summary>Computer players (and players who left) make their swaps one at a time, then press Ready.</summary>
        private void SwapPhaseComputers()
        {
            if (IsNetClient)
            {
                // Automated tests only: a client's own seat just presses Ready.
                if (AutoPlayHarness.Active && !HoldAutoPlay && LocalCanSwap && BotReady()) Submit(new NetCommand { Type = NetCommandType.SwapDone });
                return;
            }

            foreach (PlayerState p in Game.State.Players)
            {
                bool computer = p.IsBot || takenOver.Contains(p.Index) || (AutoPlayHarness.Active && p.Index == localPlayer && !HoldAutoPlay);
                if (!computer || p.SwapReady) continue;
                if (!BotReady()) return;

                var all = new CommandResult { Ok = true };
                BotLevel level = p.IsBot ? botLevel : BotLevel.Normal;
                foreach (var (handId, slot) in BotBrain.DecideSwaps(p, level, botRng))
                {
                    CommandResult r = Game.SwapCards(p.Index, handId, slot);
                    if (r.Ok) all.Events.AddRange(r.Events);
                }
                all.Events.AddRange(Game.FinishSwap(p.Index).Events);
                Execute(all);
                return; // one player per think, so it looks like people taking their time
            }
        }

        private void SwapTapHand(Card card)
        {
            if (!LocalCanSwap) return;
            if (swapSlot >= 0)
            {
                int slot = swapSlot;
                swapSlot = -1;
                SubmitSwap(card.Id, slot);
                return;
            }
            bool was = selected.Contains(card.Id);
            selected.Clear();
            if (!was) selected.Add(card.Id);
            selectedSource = PlaySource.Hand;
            AudioManager.Play(Sfx.Select, 0.6f);
            RefreshAll();
        }

        private void SwapTapFaceUp(int slot)
        {
            if (!LocalCanSwap || !Game.State.Players[localPlayer].FaceUp[slot].HasValue) return;
            if (selected.Count == 1)
            {
                int id = selected.First();
                SubmitSwap(id, slot);
                return;
            }
            swapSlot = swapSlot == slot ? -1 : slot;
            AudioManager.Play(Sfx.Select, 0.6f);
            RefreshAll();
        }

        private void SubmitSwap(int handCardId, int slot)
        {
            var c = new NetCommand { Type = NetCommandType.Swap, Slot = slot };
            c.CardIds.Add(handCardId);
            selected.Clear();
            Submit(c);
        }

        private void OnReadyPressed()
        {
            if (!LocalCanSwap) return;
            selected.Clear();
            swapSlot = -1;
            Submit(new NetCommand { Type = NetCommandType.SwapDone });
        }

        // ==================================================================
        // Quick reactions
        // ==================================================================

        private Button reactButton;
        private RectTransform reactPanel;
        private float reactReadyAt;
        private float botReactAt;
        private readonly Dictionary<int, GameObject> bubbles = new Dictionary<int, GameObject>();

        private void ToggleReactPanel()
        {
            if (reactPanel != null)
            {
                Destroy(reactPanel.gameObject);
                reactPanel = null;
                return;
            }
            // A tap anywhere outside the phrases closes it.
            reactPanel = UIKit.Layer(hudRoot, "Reactions", new Color(0f, 0f, 0f, 0.2f));
            reactPanel.gameObject.AddComponent<Button>().onClick.AddListener(ToggleReactPanel);
            var box = UIKit.Panel(reactPanel, new Vector2(860f, 440f), Vector2.zero);
            var brt = box.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-470f, -330f);

            // Scrolling list: emojis first, then the phrases.
            var view = UIBuild.Image("Viewport", box.transform, null, new Color(0f, 0f, 0f, 0.001f), new Vector2(830f, 410f), Vector2.zero);
            view.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            var content = UIBuild.Rect("Content", view.transform, new Vector2(830f, 10f), Vector2.zero);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            float y = -16f;
            int emojis = EmojiArt.Count;
            const int perRow = 6;
            for (int i = 0; i < emojis; i++)
            {
                int index = Reactions.Phrases.Length + i;
                int col = i % perRow, row = i / perRow;
                var b = UIKit.Button(content, "", new Vector2(118f, 104f), new Vector2((col - (perRow - 1) * 0.5f) * 130f, y - 52f - row * 114f),
                    () => SendReaction(index), false, 20f);
                Pin(b.transform);
                UIBuild.Image("Emoji", b.transform, EmojiArt.Get(i), Color.white, new Vector2(80f, 80f), Vector2.zero).raycastTarget = false;
            }
            y -= ((emojis + perRow - 1) / perRow) * 114f + 12f;
            for (int i = 0; i < Reactions.Phrases.Length; i++)
            {
                int index = i;
                int col = i % 3, row = i / 3;
                var b = UIKit.Button(content, Reactions.Phrases[i], new Vector2(260f, 72f), new Vector2((col - 1) * 272f, y - 36f - row * 84f),
                    () => SendReaction(index), false, 24f);
                Pin(b.transform);
            }
            y -= ((Reactions.Phrases.Length + 2) / 3) * 84f + 12f;
            content.sizeDelta = new Vector2(830f, -y);

            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 50f;

            // Children of the scrolling content are measured down from its top edge.
            void Pin(Transform t)
            {
                var rt = (RectTransform)t;
                Vector2 p = rt.anchoredPosition;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.anchoredPosition = p;
            }
        }

        private void SendReaction(int index)
        {
            if (reactPanel != null) ToggleReactPanel();
            if (localPlayer < 0 || Time.unscaledTime < reactReadyAt) return;
            reactReadyAt = Time.unscaledTime + 2f; // no spamming
            if (IsNetworked)
            {
                net.SendReaction(index); // the host shows it to everyone, including us
                return;
            }
            ShowReaction(localPlayer, index);
            // A computer player might answer, kindly.
            int reply = Reactions.BotReply(index, botRng);
            if (reply >= 0) StartCoroutine(BotReplyLater(reply));
        }

        private IEnumerator BotReplyLater(int index)
        {
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.8f, 1.6f));
            if (Game == null) yield break;
            var bots = Game.State.Players.Where(p => p.IsBot).ToList();
            if (bots.Count > 0) ShowReaction(bots[botRng.Next(bots.Count)].Index, index);
        }

        private void OnNetReaction(int player, int index)
        {
            if (this == null || Game == null || player < 0 || player >= Game.State.Players.Count) return;
            ShowReaction(player, index);
        }

        /// <summary>Computer players react now and then to what happens (host / single player only).</summary>
        private void MaybeBotReaction(GameEvent e)
        {
            if (IsNetClient || isTutorial || AutoPlayHarness.Active || Time.unscaledTime < botReactAt) return;
            var (who, index) = Reactions.BotReaction(e, Game.State, botRng);
            if (index < 0) return;
            botReactAt = Time.unscaledTime + 4f;
            if (IsNetHost) net.SendBotReaction(who, index);
            else ShowReaction(who, index);
        }

        /// <summary>A speech bubble over the player's seat for a few seconds.</summary>
        private void ShowReaction(int player, int index)
        {
            if (!GameSettings.ShowReactions || index < 0 || index >= Reactions.Count) return;
            if (bubbles.TryGetValue(player, out var old) && old != null) Destroy(old);

            Vector2 seat = SeatPointFor(player);
            Vector2 at = seat + new Vector2(0f, seat.y > 200f ? -150f : 130f); // below seats at the top, above the rest
            at.x = Mathf.Clamp(at.x, -760f, 760f);
            at.y = Mathf.Clamp(at.y, -420f, 440f);

            bool emoji = Reactions.IsEmoji(index);
            // A light cream speech bubble with a dark outline: readable on every table, dark or light.
            Vector2 size = emoji ? new Vector2(130f, 120f) : new Vector2(330f, 82f);
            var outline = UIBuild.Image("Bubble", tableHud, FxSprites.Bubble, new Color(0.16f, 0.10f, 0.04f, 0.9f), size + new Vector2(6f, 6f), at);
            outline.type = Image.Type.Sliced;
            outline.raycastTarget = false;
            var bubble = UIBuild.Image("Fill", outline.transform, FxSprites.Bubble, new Color(0.99f, 0.96f, 0.88f, 1f), size, Vector2.zero);
            bubble.type = Image.Type.Sliced;
            bubble.raycastTarget = false;
            if (emoji)
            {
                var img = UIBuild.Image("Emoji", bubble.transform, EmojiArt.Get(index - Reactions.Phrases.Length), Color.white, new Vector2(96f, 96f), Vector2.zero);
                img.raycastTarget = false;
            }
            else
            {
                var text = UIKit.Label(bubble.transform, Reactions.Phrases[index], 28f, new Vector2(310f, 70f), Vector2.zero, TextAlignmentOptions.Center, UIKit.Ink, true);
                text.raycastTarget = false;
            }
            bubbles[player] = outline.gameObject;
            AudioManager.Play(Sfx.Select, 0.5f);
            StartCoroutine(BubbleLife(outline.rectTransform));
        }

        private IEnumerator BubbleLife(RectTransform rt)
        {
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            yield return Tween.Run(0.18f, t => rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, t));
            yield return new WaitForSecondsRealtime(2.6f);
            if (rt == null) yield break;
            yield return Tween.Fade(group, 1f, 0f, 0.3f);
            if (rt != null) Destroy(rt.gameObject);
        }

        // ==================================================================
        // Rematch
        // ==================================================================

        private readonly List<string> rematchNames = new List<string>();
        private TextMeshProUGUI rematchLabel;
        private bool rematchAsked;

        private void OnRematchRequested(int player)
        {
            if (this == null || Game == null || player < 0 || player >= Game.State.Players.Count) return;
            string name = Game.State.Players[player].Name;
            if (!rematchNames.Contains(name)) rematchNames.Add(name);
            if (player != localPlayer) Toast(Loc.F("{0} wants a rematch!", name));
            if (rematchLabel != null) rematchLabel.text = Loc.T("Rematch:") + " " + string.Join(", ", rematchNames);
        }

        private void AskForRematch(Button button)
        {
            if (rematchAsked || !IsNetClient) return;
            rematchAsked = true;
            net.RequestRematch();
            UIKit.SetButtonLabel(button, "Asked!");
            button.interactable = false;
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
            if (speedButton != null) UIKit.SetButtonLabel(speedButton, Loc.F("Speed {0}x", Speeds[index]));
        }

        // ------------------------------------------------------------------ keyboard (PC)

        private readonly List<Card> shownHand = new List<Card>();   // the hand in on-screen order

        /// <summary>
        /// PC shortcuts: 1-9 / 0 pick a card, Enter or Space plays, P picks up, H / L call a 7,
        /// J Q K A or 2-9 choose an Ace's value (B burns), S sorts the hand, V views the pile,
        /// F speeds up the bots once you've finished, Esc opens the menu.
        /// </summary>
        private void HandleKeyboard()
        {
            if (coach != null && coach.Showing)
            {
                if (KeyInput.Confirm() || KeyInput.Escape()) coach.Dismiss(); // Enter / Space / Esc closes a tip
                return;
            }
            if (KeyInput.Escape())
            {
                if (pileViewer != null) ClosePileViewer();
                else if (resultsLayer == null) TogglePause();
                return;
            }
            if (paused || resultsLayer != null) return;
            if (Game != null && Game.State.Phase == GamePhase.Swapping)
            {
                if (KeyInput.Confirm()) OnReadyPressed(); // swapping is done by tapping cards
                return;
            }
            if (KeyInput.Pressed('v'))
            {
                if (pileViewer != null) ClosePileViewer();
                else OpenPileViewer();
                return;
            }
            if (pileViewer != null) return;
            if (KeyInput.Pressed('s'))
            {
                GameSettings.HandOrder = 1 - GameSettings.HandOrder;
                Toast(GameSettings.HandOrder == 0 ? Loc.T("Hand sorted by rank") : Loc.T("Hand sorted by suit"));
                RefreshAll();
            }
            if (KeyInput.Pressed('f') && CanFastForward) CycleSpeed();

            GameState s = Game.State;
            bool myChoice = !busy && localPlayer >= 0 && s.PendingPlayer == localPlayer;
            if (myChoice && s.Phase == GamePhase.AwaitingSevenCall)
            {
                if (KeyInput.Pressed('h')) OnSevenCall(SevenCall.Higher);
                else if (KeyInput.Pressed('l')) OnSevenCall(SevenCall.Lower);
                return;
            }
            if (myChoice && s.Phase == GamePhase.AwaitingAceChoice)
            {
                if (KeyInput.Pressed('b') && s.PendingAceBurnAllowed) { OnAceChoice(true, Rank.Ace); return; }
                foreach (Rank value in s.PendingAceValues)
                {
                    char key = value == Rank.Jack ? 'j' : value == Rank.Queen ? 'q' : value == Rank.King ? 'k' : value == Rank.Ace ? 'a' : (char)('0' + (int)value);
                    if ((int)value <= 9 || value >= Rank.Jack)
                        if (KeyInput.Pressed(key)) { OnAceChoice(false, value); return; }
                }
                return;
            }
            if (!LocalCanAct) return;

            PlayerState me = s.Players[localPlayer];
            int digit = KeyInput.Digit();
            if (digit > 0)
            {
                if (me.ActiveSource == PlaySource.Hand && digit <= shownHand.Count) ToggleSelection(shownHand[digit - 1], PlaySource.Hand);
                else if (me.ActiveSource == PlaySource.FaceUp && digit <= HouseRules.TableCards) OnFaceUpTapped(digit - 1);
                else if (me.ActiveSource == PlaySource.FaceDown && digit <= HouseRules.TableCards && me.FaceDown[digit - 1].HasValue) OnFaceDownTapped(digit - 1);
            }
            if (KeyInput.Pressed('p')) OnPickUpPressed();
            else if (KeyInput.Confirm())
            {
                if (selected.Count > 0) OnPlayPressed();
                else if (Game.MustPickUp(localPlayer)) OnPickUpPressed();
            }
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

        /// <summary>
        /// Dragging one of several selected hand cards: the others fan out behind it and travel with it,
        /// so it's clear they'll all be played. (If it isn't dropped on the pile, the hand lays them out again.)
        /// </summary>
        private void DragFollowers(CardView dragged)
        {
            if (localPlayer < 0 || selectedSource != PlaySource.Hand || selected.Count < 2 || !dragged.CurrentCard.HasValue) return;
            if (!selected.Contains(dragged.CurrentCard.Value.Id)) return;
            var lead = (RectTransform)dragged.transform;
            int k = 0;
            foreach (int id in selected)
            {
                if (id == dragged.CurrentCard.Value.Id) continue;
                CardView follower = hand.Find(id);
                if (follower == null) continue;
                k++;
                var rt = (RectTransform)follower.transform;
                rt.SetSiblingIndex(Mathf.Max(0, lead.GetSiblingIndex() - 1)); // just behind the card being dragged
                rt.anchoredPosition = lead.anchoredPosition + new Vector2(-22f * k, -6f * k);
                rt.localEulerAngles = new Vector3(0f, 0f, 5f * k);
            }
        }

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
            if (Game != null && Game.State.Phase == GamePhase.Swapping)
            {
                SwapTapHand(card);
                return;
            }
            if (!LocalCanAct || Game.State.Players[localPlayer].ActiveSource != PlaySource.Hand) return;
            ToggleSelection(card, PlaySource.Hand);
        }

        private void OnFaceUpTapped(int slot)
        {
            if (Game != null && Game.State.Phase == GamePhase.Swapping)
            {
                SwapTapFaceUp(slot);
                return;
            }
            if (!LocalCanAct) return;
            if (MustTakeFaceUp)
            {
                // Stuck on face-up cards: the tapped card goes into your hand with the pile.
                if (Game.State.Players[localPlayer].FaceUp[slot].HasValue)
                    Submit(new NetCommand { Type = NetCommandType.PickUp, Slot = slot });
                return;
            }
            PlayerState me = Game.State.Players[localPlayer];
            if (me.ActiveSource != PlaySource.FaceUp)
            {
                Toast(me.ActiveSource == PlaySource.Hand ? Loc.T("Play the cards in your hand first") : "");
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
                Toast(Loc.T("Face-down cards come last"));
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
            if (MustTakeFaceUp)
            {
                Toast(Loc.T("Tap the face-up card you want to take with the pile"));
                return;
            }
            Submit(new NetCommand { Type = NetCommandType.PickUp });
        }

        /// <summary>Your turn, nothing can be played, and you're on face-up cards: you choose one to take with the pile (Q11).</summary>
        private bool MustTakeFaceUp => LocalCanAct && Game.MustPickUp(localPlayer) && Game.PickUpNeedsFaceUpChoice(localPlayer);

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
            bool swapping = s.Phase == GamePhase.Swapping;
            bool canSwap = LocalCanSwap;
            bool takeChoice = MustTakeFaceUp; // face-up cards stay bright: you tap one to take with the pile

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
                    if (swapping)
                    {
                        bool canTap = mine && canSwap && p.FaceUp[k].HasValue;
                        up.SetInteractable(canTap);
                        up.SetSelected(canTap && swapSlot == k);
                        up.SetDimmed(false);
                        seat.FaceDownCards[k].SetInteractable(false);
                        continue;
                    }
                    bool upActive = mine && myTurn && mySource == PlaySource.FaceUp && p.FaceUp[k].HasValue;
                    up.SetInteractable(upActive);
                    up.SetSelected(upActive && selected.Contains(p.FaceUp[k].Value.Id));
                    up.SetDimmed(upActive && !takeChoice && !Game.CheckRank(p.FaceUp[k].Value.Rank).Legal);

                    CardView down = seat.FaceDownCards[k];
                    down.SetInteractable(mine && myTurn && mySource == PlaySource.FaceDown);
                }

                seat.SetHandCount(Mathf.Max(0, p.Hand.Count - (pendingCards.TryGetValue(i, out int flying) ? flying : 0)));
                seat.SetFinished(p.FinishPosition);

                // Down to the very last card (hand and table together): the big LAST CARD announcement.
                int total = p.TotalCards;
                if (lastTotals.TryGetValue(i, out int before) && before > 1 && total == 1 && !p.IsFinished && s.Phase != GamePhase.GameOver && s.Phase != GamePhase.Swapping)
                    StartCoroutine(LastCardBanner.Play(hudRoot, p.Name, mine));
                lastTotals[i] = total;
                seat.SetLastCard(total == 1 && !p.IsFinished);
                // While swapping, everyone who isn't ready yet is highlighted.
                seat.SetTurn(swapping ? !p.SwapReady : s.Phase != GamePhase.GameOver && i == s.ActingPlayer);
            }

            table.SetDeckCount(shownDeck);
            table.SetDiscardPile(shownPile);
            SevenCall seven = Game.ActiveSeven;
            string effect = seven == SevenCall.Higher ? Loc.T("Next: HIGHER than 7") : seven == SevenCall.Lower ? Loc.T("Next: LOWER than 7") : null;
            if (effect == null && s.TopRank == Rank.Ace && s.AceValue.HasValue && !busy)
                effect = Loc.F("Ace counts as {0}", Card.RankToLabel(s.AceValue.Value));
            table.SetEffect(effect);

            if (localPlayer >= 0)
            {
                var hold = s.Players[localPlayer].Hand.Where(c => !arriving.Contains(c.Id)); // cards still flying in aren't shown yet
                var cards = GameSettings.HandOrder == 1
                    ? hold.OrderBy(c => c.Suit).ThenBy(c => c.Rank).ToList()     // grouped by suit
                    : hold.OrderBy(c => c.Rank).ThenBy(c => c.Suit).ToList();    // low to high
                shownHand.Clear();
                shownHand.AddRange(cards);
                bool handActive = myTurn && mySource == PlaySource.Hand;
                if (swapping) hand.Show(cards, selected, canSwap ? (Func<Card, bool>)(c => true) : null, canSwap);
                else hand.Show(cards, selected, handActive ? (Func<Card, bool>)(c => Game.CheckRank(c.Rank).Legal) : null, handActive);
            }
            readyButton.gameObject.SetActive(canSwap);
            if (reactButton != null) reactButton.gameObject.SetActive(localPlayer >= 0 && GameSettings.ShowReactions && s.Phase != GamePhase.GameOver);

            playButton.gameObject.SetActive(myTurn && selected.Count > 0);
            if (selected.Count > 0) UIKit.SetButtonLabel(playButton, selected.Count == 1 ? "PLAY CARD" : Loc.F("PLAY {0} CARDS", selected.Count));
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
            readyButton.gameObject.SetActive(false);
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
            if (s.Phase == GamePhase.GameOver) { statusText.text = Loc.T("Game over"); return; }
            if (s.Phase == GamePhase.Swapping)
            {
                int waiting = s.Players.Count(p => !p.SwapReady);
                if (LocalCanSwap)
                    statusText.text = Loc.T(selected.Count > 0 ? "Now tap a face-up card to swap with it"
                        : swapSlot >= 0 ? "Now tap a card in your hand to swap with it"
                        : "Swap cards: tap a hand card, then a face-up card. Press READY when you're happy.");
                else statusText.text = waiting == 1 ? Loc.T("Waiting for 1 player to be ready...") : Loc.F("Waiting for {0} players to be ready...", waiting);
                return;
            }

            int actor = s.ActingPlayer;
            string name = s.Players[actor].Name;

            if (actor != localPlayer)
            {
                if (s.Phase == GamePhase.AwaitingSevenCall) statusText.text = Loc.F("{0} is calling higher or lower...", name);
                else if (s.Phase == GamePhase.AwaitingAceChoice) statusText.text = Loc.F("{0} is choosing how to use the Ace...", name);
                else statusText.text = Loc.F("{0}'s turn", name);
                return;
            }

            if (s.Phase == GamePhase.AwaitingSevenCall) { statusText.text = Loc.T("Call HIGHER or LOWER for the next player"); return; }
            if (s.Phase == GamePhase.AwaitingAceChoice) { statusText.text = Loc.T("Choose what your Ace counts as"); return; }
            if (busy) { statusText.text = ""; return; }

            PlayerState me = s.Players[localPlayer];
            string need = Loc.T(Game.Requirement());
            if (Game.MustPickUp(localPlayer) && Game.PickUpNeedsFaceUpChoice(localPlayer))
                statusText.text = Loc.T("No card can be played. Tap a face-up card to take it with the pile.");
            else if (Game.MustPickUp(localPlayer)) statusText.text = Loc.T("No card can be played. Pick up the pile.");
            else if (me.ActiveSource == PlaySource.FaceDown) statusText.text = Loc.T("Your turn: tap a face-down card");
            else if (me.ActiveSource == PlaySource.FaceUp) statusText.text = Loc.F("Your turn: play a face-up card ({0})", need);
            else statusText.text = Loc.F("Your turn: play {0}", need);
        }

        private void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            message = Loc.T(message); // rule messages come from the engine in English
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
            NoteArrivals(events);
            RefreshAll();

            foreach (var e in events)
            {
                yield return AnimateEvent(e, origins, onPile);
            }

            arriving.Clear(); // anything still in the air has landed now
            pendingCards.Clear();
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
            if (localPlayer >= 0)
            {
                if (e.Type == GameEventType.GameStarted) Progress.StartGame();
                Progress.OnLocalEvent(e, localPlayer); // stats, achievements and daily challenges
            }
            coach?.OnEvent(e);
            MaybeBotReaction(e);
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
                    float toScale = e.Player == localPlayer ? table.HandScale : 0.5f;
                    for (int i = 0; i < e.Cards.Count; i++)
                    {
                        shownDeck = Mathf.Max(0, shownDeck - 1);
                        table.SetDeckCount(shownDeck);
                        AudioManager.Play(Sfx.CardDraw, 0.6f);
                        Card drawn = e.Cards[i];
                        int drawer = e.Player;
                        StartCoroutine(FlyCard(null, table.DeckPoint, to, table.PileCardScale, toScale, 0.25f, () => Landed(drawer, drawn)));
                        yield return Tween.Wait(Tween.Scaled(0.07f));
                    }
                    yield return Tween.Wait(Tween.Scaled(0.2f));
                    AllLanded(e);
                    break;
                }

                case GameEventType.PileCollected:
                {
                    Vector2 to = e.Player == localPlayer ? table.HandCenter : SeatPointFor(e.Player);
                    int n = e.Cards.Count;
                    string what = e.Player == localPlayer
                        ? (n == 1 ? Loc.T("You pick up 1 card") : Loc.F("You pick up {0} cards", n))
                        : (n == 1 ? Loc.F("{0} picks up 1 card", who) : Loc.F("{0} picks up {1} cards", who, n));
                    if (e.Player == localPlayer) Haptics.Buzz();
                    AudioManager.Play(Sfx.PickUp);
                    table.Announce(what, new Color(1f, 0.75f, 0.6f), centre, 52f);

                    // Cards peel off the pile one after another so the size of the pick-up is obvious.
                    int shown = Mathf.Min(10, shownPile.Count);
                    for (int i = 0; i < shown; i++)
                    {
                        Card c = shownPile[shownPile.Count - 1];
                        shownPile.RemoveAt(shownPile.Count - 1);
                        table.SetDiscardPile(shownPile);
                        int picker = e.Player;
                        StartCoroutine(FlyCard(c, table.DiscardPoint, to, table.PileCardScale, 0.5f, 0.4f, () => Landed(picker, c)));
                        yield return Tween.Wait(Tween.Scaled(0.06f));
                    }
                    shownPile.Clear();
                    table.SetDiscardPile(shownPile);
                    yield return Tween.Wait(Tween.Scaled(0.45f));
                    AllLanded(e); // the rest of a big pile arrives together

                    // Give everyone a moment to take it in before the computer plays on.
                    if (!AutoPlayHarness.Active)
                    {
                        pauseUntil = Time.unscaledTime + GameSettings.PickUpPauseSeconds / Tween.SpeedMultiplier;
                        noticeMessage = Loc.F("{0}. Take a moment...", what);
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
                    Haptics.Buzz();
                    FxStyle burnStyle = FxOf(e.Player); // everyone burns in their own style
                    if (IsFourOfAKind(e.Cards)) table.Announce("FOUR OF A KIND!", UIKit.Gold, centre, 72f);
                    else if (burnStyle == FxStyle.Water) table.Announce("SPLASH!", new Color(0.6f, 0.9f, 1f), centre, 84f);
                    else table.Announce("BURN!", new Color(1f, 0.55f, 0.15f), centre, 84f);
                    yield return fx.Burn(burning, table.DiscardPoint, table.PileCardScale, burnStyle);
                    break;
                }

                case GameEventType.PlayersSkipped:
                {
                    AudioManager.Play(Sfx.Skip);
                    if (e.Players.Contains(localPlayer)) Haptics.Buzz();
                    table.Announce(e.Count > 1 ? Loc.F("SKIP x{0}", e.Count) : "SKIP", new Color(0.6f, 0.85f, 1f), centre, 72f);
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
                        table.Announce(Loc.F("ACE = {0}", Loc.T(RankName(e.AceValue))), UIKit.Gold, centre, 64f);
                        fx.Ripple(table.DiscardPoint, new Color(1f, 0.85f, 0.4f, 0.8f));
                        yield return Tween.Wait(Tween.Scaled(0.5f));
                    }
                    break;

                case GameEventType.PlayerFinished:
                {
                    if (e.Position == 1)
                    {
                        // The winner gets the big sweep-across banner (it plays its own sound) and their celebration.
                        fx.Celebrate(SeatPointFor(e.Player) + new Vector2(0f, 40f), CelebrationOf(e.Player));
                        RefreshAll();
                        yield return LastCardBanner.PlayWin(hudRoot, s.Players[e.Player].Name, e.Player == localPlayer);
                        break;
                    }
                    bool last = e.Position == s.Players.Count;
                    if (last)
                    {
                        // The Shedhead gets the big banner too (it plays its own sound).
                        RefreshAll();
                        yield return LastCardBanner.PlayShedhead(hudRoot, s.Players[e.Player].Name, e.Player == localPlayer);
                        break;
                    }
                    string text = e.Player == localPlayer
                        ? (last ? Loc.T("You're the Shedhead!") : Loc.F("You finish {0}!", Loc.Ordinal(e.Position)))
                        : (last ? Loc.F("{0} is the Shedhead!", who) : Loc.F("{0} finishes {1}!", who, Loc.Ordinal(e.Position)));
                    table.Announce(text, UIKit.Gold, table.DiscardPoint + new Vector2(-120f, -10f), 56f);
                    if (e.Player == localPlayer) AudioManager.Play(Sfx.Win, 0.8f);
                    RefreshAll();
                    yield return Tween.Wait(Mathf.Max(0.4f, Tween.Scaled(0.9f)));
                    break;
                }

                case GameEventType.FaceUpTaken:
                {
                    // The chosen face-up card flies into the hand, just before the pile follows it.
                    Card card = e.Cards[0];
                    int slot = e.Slots.Count > 0 ? e.Slots[0] : 0;
                    SeatView seat = SeatFor(e.Player);
                    Transform from = seat.FaceUpCards[slot].transform;
                    Vector2 to = e.Player == localPlayer ? table.HandCenter : SeatPointFor(e.Player);
                    float toScale = e.Player == localPlayer ? table.HandScale : ScaleOf(seat.transform) * SeatView.TableCardScale;
                    AudioManager.Play(Sfx.CardDraw, 0.7f);
                    yield return FlyCard(card, table.ToLocal(from), to, ScaleOf(from), toScale, 0.3f, () => Landed(e.Player, card));
                    AllLanded(e);
                    break;
                }

                case GameEventType.CardsSwapped:
                    // The table already shows the swapped cards; a little sound and pause sells it.
                    AudioManager.Play(Sfx.CardDraw, 0.6f);
                    yield return Tween.Wait(Mathf.Max(0.05f, Tween.Scaled(0.15f)));
                    break;

                case GameEventType.PlayerReady:
                    if (e.Player != localPlayer && Game.State.Phase == GamePhase.Swapping)
                    {
                        noticeMessage = Loc.F("{0} is ready", who);
                        noticeUntil = Time.unscaledTime + 1.5f;
                    }
                    break;

                case GameEventType.TurnChanged:
                    if (e.Player == localPlayer && s.Phase == GamePhase.Playing && Game.State.Current == localPlayer)
                    {
                        AudioManager.Play(Sfx.YourTurn, 0.6f);
                        Haptics.Buzz();
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
            readyButton = UIKit.Button(tableHud, "READY", new Vector2(300f, 84f), new Vector2(735f, -462f), OnReadyPressed, true, 32f);

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
            UIKit.Button(tableHud, "View pile", new Vector2(140f, 52f), table.DiscardPoint + new Vector2(CardView.Width * 0.5f * table.PileCardScale + 82f, 0f), OpenPileViewer, false, 22f);
            // The choice panels (HIGHER / LOWER, the Ace) sit over the pile: keep them on top so their buttons get the tap.
            sevenPanel.transform.SetAsLastSibling();
            acePanel.transform.SetAsLastSibling();

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

            // Quick reactions, just under the Menu button.
            reactButton = UIKit.Button(hudRoot, "React", new Vector2(150f, 64f), Vector2.zero, ToggleReactPanel, false, 26f);
            var rrt = (RectTransform)reactButton.transform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(1f, 1f);
            rrt.anchoredPosition = new Vector2(-95f, -124f);
            reactButton.gameObject.SetActive(false);

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

            UIKit.Label(panel.transform, n == 0 ? Loc.T("The pile is empty") : n == 1 ? Loc.T("The pile: 1 card") : Loc.F("The pile: {0} cards", n),
                48f, new Vector2(1400f, 70f), new Vector2(0f, 392f), TextAlignmentOptions.Center, UIKit.Gold, true);

            string info = Loc.F("Burned so far: {0}     Left in deck: {1}", Game.State.BurnedCount, shownDeck);
            if (n > 0)
            {
                Card top = shownPile[n - 1];
                string topText = Loc.F("Top card: {0}", Loc.T(RankName(top.Rank)).ToLowerInvariant());
                if (top.Rank == Rank.Ace && Game.State.AceValue.HasValue) topText += " " + Loc.F("(counts as {0})", Card.RankToLabel(Game.State.AceValue.Value));
                info = topText + "     " + Loc.F("Next card must be {0}", Loc.T(Game.Requirement())) + "     " + info;
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
                UIKit.Label(panel.transform, Loc.T("In the pile:") + "   " + string.Join("     ", counts), 28f, new Vector2(1480f, 50f),
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
            aceTitle.text = Loc.T(lowOnly ? "Your Ace counts as... (LOWER than 7 was called)" : "Your Ace counts as... (it must match or beat the pile)");
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
            var panel = UIKit.Panel(tableHud, new Vector2(720f, 240f), new Vector2(0f, 20f));
            UIKit.Label(panel.transform, title, 28f, new Vector2(660f, 60f), new Vector2(0f, 64f), TextAlignmentOptions.Center, UIKit.Text, true);
            UIKit.Button(panel.transform, a, new Vector2(300f, 90f), new Vector2(-160f, -40f), onA, true, 30f);
            UIKit.Button(panel.transform, b, new Vector2(300f, 90f), new Vector2(160f, -40f), onB, true, 30f);
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
            bool showProgress = localPlayer >= 0;
            var panel = UIKit.Panel(resultsLayer, new Vector2(760f, height), new Vector2(showProgress ? -300f : 0f, 0f));
            float top = height * 0.5f;

            if (showProgress)
            {
                int position = s.Players[localPlayer].FinishPosition;
                Progress.Report report = Progress.FinishGame(position, n, IsNetworked ? -1 : (int)botLevel, IsNetworked, isTutorial);
                if (isTutorial)
                {
                    GameSettings.TutorialDone = true;
                    GameLaunch.Tutorial = false; // "Play Again" starts a normal game
                    coach?.Dismiss();
                    coach = null;
                }
                BuildProgressPanel(report, Mathf.Max(height, 460f));
            }

            int winner = s.FinishOrder[0];
            string title = isTutorial ? Loc.T("Guided game complete!")
                : winner == localPlayer ? Loc.T("You win!") : Loc.F("{0} wins!", s.Players[winner].Name);
            UIKit.Label(panel.transform, title, 60f, new Vector2(700f, 80f), new Vector2(0f, top - 60f), TextAlignmentOptions.Center, UIKit.Gold, true);
            string subtitle = Loc.F("{0} players", n);
            if (localPlayer >= 0)
            {
                // Coins for finishing, spent on the Customise screen.
                int reward = Cosmetics.RewardFor(s.Players[localPlayer].FinishPosition, n);
                Cosmetics.AddCoins(reward);
                subtitle += "     " + Loc.F("+{0} coins  (you have {1})", reward, Cosmetics.Coins);
            }
            UIKit.Label(panel.transform, subtitle, 26f, new Vector2(700f, 40f), new Vector2(0f, top - 112f), TextAlignmentOptions.Center, UIKit.Muted);

            for (int i = 0; i < s.FinishOrder.Count; i++)
            {
                int p = s.FinishOrder[i];
                float y = top - 165f - i * 52f;
                Color c = i == 0 ? UIKit.Gold : (p == localPlayer ? new Color(0.6f, 0.85f, 1f) : UIKit.Text);
                string suffix = i == n - 1 ? "   - " + Loc.T("Shedhead!") : "";
                UIKit.Label(panel.transform, Loc.Ordinal(i + 1), 30f, new Vector2(120f, 48f), new Vector2(-230f, y), TextAlignmentOptions.Left, c, true);
                UIKit.Label(panel.transform, s.Players[p].Name + suffix, 30f, new Vector2(420f, 48f), new Vector2(60f, y), TextAlignmentOptions.Left, c, i == 0);
            }

            // Rematch: the same players again. Online, the host starts it and the others can ask for one.
            if (IsNetClient)
            {
                Button ask = null;
                ask = UIKit.Button(panel.transform, rematchAsked ? "Asked!" : "Ask for a rematch", new Vector2(300f, 80f), new Vector2(-165f, -top + 65f),
                    () => AskForRematch(ask), true, 26f);
                ask.interactable = !rematchAsked;
            }
            else
                UIKit.Button(panel.transform, isTutorial ? "Play a game" : "Rematch", new Vector2(300f, 80f), new Vector2(-165f, -top + 65f), StartNewGame, true);
            if (IsNetworked)
            {
                rematchLabel = UIKit.Label(panel.transform, rematchNames.Count > 0 ? Loc.T("Rematch:") + " " + string.Join(", ", rematchNames) : "",
                    22f, new Vector2(700f, 36f), new Vector2(0f, -top + 130f), TextAlignmentOptions.Center, UIKit.Gold);
            }
            UIKit.Button(panel.transform, IsNetworked ? "Leave game" : "Main Menu", new Vector2(300f, 80f), new Vector2(165f, -top + 65f), GoToMainMenu);
            AudioManager.Play(Sfx.Win);
        }

        /// <summary>Beside the results: level, the XP bar filling up, and everything earned this game.</summary>
        private void BuildProgressPanel(Progress.Report report, float height)
        {
            var panel = UIKit.Panel(resultsLayer, new Vector2(560f, height), new Vector2(390f, 0f));
            float top = height * 0.5f;
            var levelText = UIKit.Label(panel.transform, Loc.F("Level {0}", report.LevelBefore), 48f, new Vector2(500f, 64f),
                new Vector2(0f, top - 56f), TextAlignmentOptions.Center, UIKit.Gold, true);

            var barBg = UIBuild.Image("XpBar", panel.transform, UIKit.PanelSprite, new Color(0f, 0f, 0f, 0.55f), new Vector2(460f, 26f), new Vector2(0f, top - 112f));
            barBg.type = Image.Type.Sliced;
            var fill = UIBuild.Image("Fill", barBg.transform, UIKit.PrimarySprite != null ? UIKit.PrimarySprite : UIKit.PanelSprite,
                UIKit.PrimarySprite != null ? Color.white : UIKit.Gold, new Vector2(452f, 18f), Vector2.zero);
            fill.type = Image.Type.Sliced;
            var frt = fill.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 0.5f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = new Vector2(4f, 0f);
            void SetFill(float f) => frt.sizeDelta = new Vector2(Mathf.Max(14f, 452f * Mathf.Clamp01(f)), 18f);
            SetFill(report.FractionBefore);

            UIKit.Label(panel.transform, Loc.F("+{0} XP", report.Xp), 30f, new Vector2(500f, 44f), new Vector2(0f, top - 156f),
                TextAlignmentOptions.Center, UIKit.Text, true);

            float y = top - 210f;
            var lines = new List<string>(report.Lines);
            int left = Progress.DailiesLeft;
            lines.Add(left == 0 ? Loc.T("All daily challenges done. New ones tomorrow!")
                : left == 1 ? Loc.T("1 daily challenge left today") : Loc.F("{0} daily challenges left today", left));
            foreach (string line in lines.Take(6))
            {
                var t = UIKit.Label(panel.transform, line, 23f, new Vector2(500f, 56f), new Vector2(0f, y), TextAlignmentOptions.Center, UIKit.Muted);
                t.textWrappingMode = TextWrappingModes.Normal;
                y -= 60f;
            }

            StartCoroutine(FillXp());

            IEnumerator FillXp()
            {
                yield return new WaitForSecondsRealtime(0.4f);
                for (int level = report.LevelBefore; level <= report.Level; level++)
                {
                    float from = level == report.LevelBefore ? report.FractionBefore : 0f;
                    float to = level == report.Level ? report.Fraction : 1f;
                    yield return Tween.Run(0.6f, t => SetFill(Mathf.Lerp(from, to, t)));
                    if (level < report.Level)
                    {
                        levelText.text = Loc.F("Level {0}", level + 1);
                        AudioManager.Play(Sfx.Win, 0.6f);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ celebration banners

        private readonly Queue<(string head, string detail)> celebrations = new Queue<(string, string)>();
        private bool celebrating;

        private void OnCelebrate(string head, string detail)
        {
            if (this == null || hudRoot == null) return;
            celebrations.Enqueue((head, detail));
            if (!celebrating) StartCoroutine(ShowCelebrations());
        }

        /// <summary>A gold banner drops in at the top of the screen for each unlock, one at a time.</summary>
        private IEnumerator ShowCelebrations()
        {
            celebrating = true;
            while (celebrations.Count > 0)
            {
                var (head, detail) = celebrations.Dequeue();
                var banner = UIKit.Panel(hudRoot, new Vector2(820f, 104f), Vector2.zero, new Color(0.10f, 0.07f, 0.03f, 0.96f));
                var rt = banner.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                banner.raycastTarget = false;
                UIKit.Label(banner.transform, head, 30f, new Vector2(780f, 44f), new Vector2(0f, 20f), TextAlignmentOptions.Center, UIKit.Gold, true);
                UIKit.Label(banner.transform, detail, 24f, new Vector2(780f, 38f), new Vector2(0f, -22f), TextAlignmentOptions.Center, UIKit.Text);
                var group = banner.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                AudioManager.Play(Sfx.Win, 0.45f);
                Haptics.Buzz();

                yield return Tween.Run(0.3f, t => rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(60f, -70f, 1f - (1f - t) * (1f - t))));
                yield return new WaitForSecondsRealtime(2.4f);
                yield return Tween.Fade(group, 1f, 0f, 0.35f);
                Destroy(banner.gameObject);
            }
            celebrating = false;
        }
    }
}
