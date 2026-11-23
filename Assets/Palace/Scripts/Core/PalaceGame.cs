using System;
using System.Collections.Generic;

namespace Palace.Core
{
    public struct PlayCheck
    {
        public bool Legal;
        public string Reason;

        public static PlayCheck Ok => new PlayCheck { Legal = true };
        public static PlayCheck No(string reason) => new PlayCheck { Legal = false, Reason = reason };
    }

    /// <summary>
    /// THE RULES ENGINE. The single place where the school rules live.
    ///
    /// Every player action is a command (PlayCards, PlayFaceDown, PickUp, DeclareSeven,
    /// ChooseAce). The engine checks it, changes the GameState and returns a list of
    /// GameEvents describing what happened, which the Unity side animates. Nothing here
    /// knows about Unity, so it can be unit-tested and later run on a multiplayer host.
    /// </summary>
    public sealed class PalaceGame
    {
        public GameState State { get; }
        public HouseRules Rules { get; }
        public IReadOnlyList<GameEvent> StartEvents => startEvents;

        private readonly Random rng;
        private readonly List<GameEvent> startEvents = new List<GameEvent>();

        public PalaceGame(GameState state, HouseRules rules, int seed)
        {
            State = state;
            Rules = rules ?? new HouseRules();
            rng = new Random(seed);
        }

        // ==================================================================
        // Setting up
        // ==================================================================

        public static PalaceGame Create(IReadOnlyList<string> names, IReadOnlyList<bool> isBot, HouseRules rules, int seed)
        {
            int n = names.Count;
            if (n < 2 || n > 10) throw new ArgumentException("Palace needs 2-10 players.");

            rules = rules ?? new HouseRules();
            var state = new GameState();
            for (int i = 0; i < n; i++) state.Players.Add(new PlayerState(i, names[i], isBot[i]));

            var game = new PalaceGame(state, rules, seed);

            int id = 0;
            int decks = rules.DecksFor(n);
            for (int d = 0; d < decks; d++)
                for (int s = 0; s < 4; s++)
                    for (int r = 2; r <= 14; r++)
                        state.DrawPile.Add(new Card(id++, (Rank)r, (Suit)s));
            state.TotalCards = id;
            game.Shuffle(state.DrawPile);

            // Deal like at a real table: a round of face-down, a round of face-up, then hands.
            for (int slot = 0; slot < HouseRules.TableCards; slot++)
                foreach (var p in state.Players) p.FaceDown[slot] = game.DrawTop();
            for (int slot = 0; slot < HouseRules.TableCards; slot++)
                foreach (var p in state.Players) p.FaceUp[slot] = game.DrawTop();
            for (int k = 0; k < HouseRules.HandSize; k++)
                foreach (var p in state.Players) p.Hand.Add(game.DrawTop());

            state.Current = game.rng.Next(n); // Q6: random first player
            game.startEvents.Add(new GameEvent { Type = GameEventType.GameStarted });
            game.startEvents.Add(new GameEvent { Type = GameEventType.TurnChanged, Player = state.Current });
            return game;
        }

        private void Shuffle(List<Card> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        private Card DrawTop()
        {
            var pile = State.DrawPile;
            Card c = pile[pile.Count - 1];
            pile.RemoveAt(pile.Count - 1);
            return c;
        }

        // ==================================================================
        // Questions the UI and bots can ask
        // ==================================================================

        /// <summary>Is a card of this rank playable on the current pile? Includes a reason if not.</summary>
        public PlayCheck CheckRank(Rank rank)
        {
            if (rank == Rank.Two) return PlayCheck.Ok;            // 2 goes on anything
            if (rank == Rank.Ten) return CheckBurn();
            if (rank == Rank.Ace)
            {
                PlayCheck high = CheckValue(14);
                if (high.Legal) return high;
                PlayCheck burn = CheckBurn();
                return burn.Legal ? burn : high;
            }
            return CheckValue((int)rank);
        }

        public bool AceHighLegal => CheckValue(14).Legal;
        public bool AceBurnLegal => CheckBurn().Legal;

        /// <summary>The 7 restriction currently in force (only while that 7 is on top).</summary>
        public SevenCall ActiveSeven => State.TopRank == Rank.Seven ? State.ActiveSeven : SevenCall.None;

        public bool HasLegalPlay(int player)
        {
            PlayerState p = State.Players[player];
            switch (p.ActiveSource)
            {
                case PlaySource.Hand:
                    foreach (var c in p.Hand) if (CheckRank(c.Rank).Legal) return true;
                    return false;
                case PlaySource.FaceUp:
                    foreach (var c in p.FaceUp) if (c.HasValue && CheckRank(c.Value.Rank).Legal) return true;
                    return false;
                default:
                    return true; // turning over a face-down card is always allowed
            }
        }

        /// <summary>True when the player has no legal card and must collect the pile.</summary>
        public bool MustPickUp(int player)
        {
            return State.Phase == GamePhase.Playing && State.Current == player
                   && State.Players[player].ActiveSource != PlaySource.FaceDown
                   && !HasLegalPlay(player);
        }

        /// <summary>Who would play after this player in the current direction.</summary>
        public int NextActiveFrom(int player)
        {
            int n = State.Players.Count;
            for (int k = 1; k <= n; k++)
            {
                int j = Mod(player + State.Direction * k, n);
                if (!State.Players[j].IsFinished) return j;
            }
            return player;
        }

        /// <summary>Short description of what the next card must be, e.g. "9 or higher".</summary>
        public string Requirement()
        {
            SevenCall seven = ActiveSeven;
            if (seven == SevenCall.Higher) return "HIGHER than 7";
            if (seven == SevenCall.Lower) return "LOWER than 7";
            Rank? top = State.TopRank;
            if (!top.HasValue || top == Rank.Two) return "any card";
            return Card.RankToLabel(top.Value) + " or higher";
        }

        private PlayCheck CheckValue(int value)
        {
            SevenCall seven = ActiveSeven;
            if (seven == SevenCall.Higher)
                return value > 7 ? PlayCheck.Ok : PlayCheck.No("Must be HIGHER than 7");
            if (seven == SevenCall.Lower)
                return value < 7 ? PlayCheck.Ok : PlayCheck.No("Must be LOWER than 7");

            Rank? top = State.TopRank;
            if (!top.HasValue || value >= (int)top.Value) return PlayCheck.Ok;
            return PlayCheck.No("Needs " + Card.RankToLabel(top.Value) + " or higher");
        }

        private PlayCheck CheckBurn()
        {
            if (!Rules.TenPlayableOnAnything) return CheckValue(10);
            if (ActiveSeven == SevenCall.Lower && !Rules.BurnAllowedOnLowerSeven)
                return PlayCheck.No("Must be LOWER than 7");
            return PlayCheck.Ok;
        }

        // ==================================================================
        // Commands
        // ==================================================================

        /// <summary>Play one or more cards of the same rank from the hand or the face-up cards.</summary>
        public CommandResult PlayCards(int player, PlaySource source, IReadOnlyList<int> cardIds)
        {
            if (State.Phase != GamePhase.Playing) return CommandResult.Fail("Waiting for a choice to be made");
            if (player != State.Current) return CommandResult.Fail("It isn't your turn");
            if (source == PlaySource.FaceDown) return CommandResult.Fail("Face-down cards are turned over one at a time");
            if (cardIds == null || cardIds.Count == 0) return CommandResult.Fail("Select a card first");

            PlayerState p = State.Players[player];
            if (source != p.ActiveSource)
                return CommandResult.Fail(p.ActiveSource == PlaySource.Hand ? "Play the cards in your hand first" : "You have no face-up cards left");

            var cards = new List<Card>();
            var slots = new List<int>();
            foreach (int id in cardIds)
            {
                bool found = false;
                if (source == PlaySource.Hand)
                {
                    foreach (var c in p.Hand)
                        if (c.Id == id && !cards.Contains(c)) { cards.Add(c); found = true; break; }
                }
                else
                {
                    for (int s = 0; s < p.FaceUp.Length; s++)
                        if (p.FaceUp[s].HasValue && p.FaceUp[s].Value.Id == id && !slots.Contains(s))
                        { cards.Add(p.FaceUp[s].Value); slots.Add(s); found = true; break; }
                }
                if (!found) return CommandResult.Fail("That card isn't available");
            }

            Rank rank = cards[0].Rank;
            foreach (var c in cards)
                if (c.Rank != rank) return CommandResult.Fail("Multiple cards must all be the same rank");

            PlayCheck check = CheckRank(rank);
            if (!check.Legal) return CommandResult.Fail(check.Reason);
            bool aceHigh = AceHighLegal, aceBurn = AceBurnLegal;

            if (source == PlaySource.Hand) foreach (var c in cards) p.Hand.Remove(c);
            else foreach (int s in slots) p.FaceUp[s] = null;
            State.Discard.AddRange(cards);

            var result = new CommandResult { Ok = true };
            result.Events.Add(new GameEvent
            {
                Type = GameEventType.CardsPlayed, Player = player, Source = source,
                Cards = new List<Card>(cards), Slots = slots
            });
            Resolve(result, player, rank, cards.Count, aceHigh, aceBurn);
            return result;
        }

        /// <summary>Turn over one face-down card. Played if legal, otherwise the pile is collected with it.</summary>
        public CommandResult PlayFaceDown(int player, int slot)
        {
            if (State.Phase != GamePhase.Playing) return CommandResult.Fail("Waiting for a choice to be made");
            if (player != State.Current) return CommandResult.Fail("It isn't your turn");
            PlayerState p = State.Players[player];
            if (p.ActiveSource != PlaySource.FaceDown) return CommandResult.Fail("Play your other cards first");
            if (slot < 0 || slot >= p.FaceDown.Length || !p.FaceDown[slot].HasValue) return CommandResult.Fail("No card there");

            Card card = p.FaceDown[slot].Value;
            p.FaceDown[slot] = null;

            var result = new CommandResult { Ok = true };
            result.Events.Add(new GameEvent
            {
                Type = GameEventType.FaceDownRevealed, Player = player, Source = PlaySource.FaceDown,
                Cards = new List<Card> { card }, Slots = new List<int> { slot }
            });

            PlayCheck check = CheckRank(card.Rank);
            bool aceHigh = AceHighLegal, aceBurn = AceBurnLegal;
            State.Discard.Add(card);

            if (check.Legal)
            {
                result.Events.Add(new GameEvent
                {
                    Type = GameEventType.CardsPlayed, Player = player, Source = PlaySource.FaceDown,
                    Cards = new List<Card> { card }, Slots = new List<int> { slot }
                });
                Resolve(result, player, card.Rank, 1, aceHigh, aceBurn);
            }
            else
            {
                // Illegal: the revealed card goes into the hand along with the whole pile.
                Collect(result, player);
                SetTurn(result, NextActiveFrom(player));
            }
            return result;
        }

        /// <summary>Collect the whole discard pile. Only allowed when no card can be played (Q9).</summary>
        public CommandResult PickUp(int player)
        {
            if (State.Phase != GamePhase.Playing) return CommandResult.Fail("Waiting for a choice to be made");
            if (player != State.Current) return CommandResult.Fail("It isn't your turn");
            if (State.Discard.Count == 0) return CommandResult.Fail("There is nothing to pick up");
            if (State.Players[player].ActiveSource == PlaySource.FaceDown) return CommandResult.Fail("Turn over a face-down card");
            if (HasLegalPlay(player)) return CommandResult.Fail("You have a card you can play");

            var result = new CommandResult { Ok = true };
            Collect(result, player);
            SetTurn(result, NextActiveFrom(player)); // Q8: play passes on
            return result;
        }

        public CommandResult DeclareSeven(int player, SevenCall call)
        {
            if (State.Phase != GamePhase.AwaitingSevenCall || player != State.PendingPlayer)
                return CommandResult.Fail("No 7 to call");
            if (call == SevenCall.None) return CommandResult.Fail("Choose HIGHER or LOWER");

            var result = new CommandResult { Ok = true };
            State.ActiveSeven = call;
            State.Phase = GamePhase.Playing;
            State.PendingPlayer = -1;
            result.Events.Add(new GameEvent { Type = GameEventType.SevenDeclared, Player = player, Call = call });
            SetTurn(result, NextActiveFrom(player));
            return result;
        }

        public CommandResult ChooseAce(int player, bool burn)
        {
            if (State.Phase != GamePhase.AwaitingAceChoice || player != State.PendingPlayer)
                return CommandResult.Fail("No Ace choice to make");

            var result = new CommandResult { Ok = true };
            State.Phase = GamePhase.Playing;
            State.PendingPlayer = -1;
            result.Events.Add(new GameEvent { Type = GameEventType.AceChosen, Player = player, Burn = burn });
            ApplyEffects(result, player, Rank.Ace, State.PendingCount, burn);
            return result;
        }

        // ==================================================================
        // What happens after cards land on the pile
        // ==================================================================

        private void Resolve(CommandResult result, int player, Rank rank, int count, bool aceHigh, bool aceBurn)
        {
            Replenish(result, player);
            CheckFinished(result, player);
            if (CheckGameOver(result)) return;

            bool fourOfAKind = Rules.FourOfAKindBurns && TopRunLength() >= 4;

            if (rank == Rank.Seven && !fourOfAKind)
            {
                State.Phase = GamePhase.AwaitingSevenCall;
                State.PendingPlayer = player;
                State.PendingCount = count;
                result.Events.Add(new GameEvent { Type = GameEventType.SevenCallNeeded, Player = player });
                return;
            }

            if (rank == Rank.Ace && !fourOfAKind)
            {
                if (aceHigh && aceBurn)
                {
                    State.Phase = GamePhase.AwaitingAceChoice;
                    State.PendingPlayer = player;
                    State.PendingCount = count;
                    result.Events.Add(new GameEvent { Type = GameEventType.AceChoiceNeeded, Player = player });
                    return;
                }
                ApplyEffects(result, player, rank, count, !aceHigh && aceBurn);
                return;
            }

            ApplyEffects(result, player, rank, count, rank == Rank.Ten || fourOfAKind);
        }

        private void ApplyEffects(CommandResult result, int player, Rank rank, int count, bool burn)
        {
            State.ActiveSeven = SevenCall.None;
            PlayerState p = State.Players[player];

            if (burn)
            {
                var burned = new List<Card>(State.Discard);
                State.BurnedCount += burned.Count;
                State.Discard.Clear();
                result.Events.Add(new GameEvent { Type = GameEventType.PileBurned, Player = player, Cards = burned });
                // Burning earns another turn (unless that was the player's last card).
                SetTurn(result, p.IsFinished ? NextActiveFrom(player) : player);
                return;
            }

            int next;
            switch (rank)
            {
                case Rank.Eight:
                {
                    // Each 8 skips one player (wrapping round the table if needed).
                    var skipped = new List<int>();
                    int pos = player;
                    for (int i = 0; i < count; i++)
                    {
                        pos = NextActiveFrom(pos);
                        skipped.Add(pos);
                    }
                    next = NextActiveFrom(pos);
                    result.Events.Add(new GameEvent { Type = GameEventType.PlayersSkipped, Player = player, Players = skipped, Count = count });
                    break;
                }
                case Rank.Queen:
                    if (count % 2 == 1) State.Direction = -State.Direction;
                    result.Events.Add(new GameEvent
                    {
                        Type = GameEventType.DirectionReversed, Player = player, Count = count, Direction = State.Direction
                    });
                    next = NextActiveFrom(player);
                    break;
                case Rank.Two:
                    result.Events.Add(new GameEvent { Type = GameEventType.Reset, Player = player });
                    next = NextActiveFrom(player);
                    break;
                default:
                    next = NextActiveFrom(player);
                    break;
            }

            SetTurn(result, next);
        }

        private void Replenish(CommandResult result, int player)
        {
            PlayerState p = State.Players[player];
            var drawn = new List<Card>();
            while (p.Hand.Count < HouseRules.HandSize && State.DrawPile.Count > 0)
            {
                Card c = DrawTop();
                p.Hand.Add(c);
                drawn.Add(c);
            }
            if (drawn.Count > 0)
                result.Events.Add(new GameEvent { Type = GameEventType.CardsDrawn, Player = player, Cards = drawn });
        }

        private void Collect(CommandResult result, int player)
        {
            var pile = new List<Card>(State.Discard);
            State.Players[player].Hand.AddRange(pile);
            State.Discard.Clear();
            State.ActiveSeven = SevenCall.None;
            result.Events.Add(new GameEvent { Type = GameEventType.PileCollected, Player = player, Cards = pile });
        }

        private void CheckFinished(CommandResult result, int player)
        {
            PlayerState p = State.Players[player];
            if (p.IsFinished || p.TotalCards > 0) return;
            State.FinishOrder.Add(player);
            p.FinishPosition = State.FinishOrder.Count;
            result.Events.Add(new GameEvent { Type = GameEventType.PlayerFinished, Player = player, Position = p.FinishPosition });
        }

        private bool CheckGameOver(CommandResult result)
        {
            if (State.ActivePlayerCount > 1) return false;

            foreach (var p in State.Players)
            {
                if (p.IsFinished) continue;
                State.FinishOrder.Add(p.Index);
                p.FinishPosition = State.FinishOrder.Count;
                result.Events.Add(new GameEvent { Type = GameEventType.PlayerFinished, Player = p.Index, Position = p.FinishPosition });
            }

            State.Phase = GamePhase.GameOver;
            State.PendingPlayer = -1;
            result.Events.Add(new GameEvent { Type = GameEventType.GameOver, Players = new List<int>(State.FinishOrder) });
            return true;
        }

        private void SetTurn(CommandResult result, int next)
        {
            State.Current = next;
            State.TurnNumber++;
            result.Events.Add(new GameEvent { Type = GameEventType.TurnChanged, Player = next });
        }

        private int TopRunLength()
        {
            var d = State.Discard;
            if (d.Count == 0) return 0;
            int n = 1;
            for (int i = d.Count - 2; i >= 0 && d[i].Rank == d[d.Count - 1].Rank; i--) n++;
            return n;
        }

        private static int Mod(int a, int n) => ((a % n) + n) % n;
    }
}
