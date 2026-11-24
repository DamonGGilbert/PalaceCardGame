using System;
using System.Collections.Generic;
using System.IO;
using Palace.Core;

namespace Palace.Net
{
    public enum MsgType : byte
    {
        Hello = 1,        // client -> host: my name
        Lobby = 2,        // host -> clients: who is in the room
        Ready = 3,        // client -> host: ready / not ready
        StartGame = 4,    // host -> clients: load the game scene
        InGame = 5,       // client -> host: game scene loaded, send me the table
        Update = 6,       // host -> client: events + the table as THIS player may see it
        Command = 7,      // client -> host: a move
        Error = 8,        // host -> client: that move was refused
        Closing = 9,      // host -> clients: the host has left
        Reaction = 10,    // client -> host: a quick reaction; host -> clients: who sent which reaction
        Rematch = 11      // client -> host: I want a rematch; host -> clients: who wants one
    }

    public sealed class LobbySeat
    {
        public ulong ClientId;   // network id (host = 0); unused for bots
        public string Name;
        public bool IsBot;
        public bool Ready;
    }

    public enum NetCommandType : byte { PlayCards, PlayFaceDown, PickUp, DeclareSeven, ChooseAce, Swap, SwapDone }

    public sealed class NetCommand
    {
        public NetCommandType Type;
        public PlaySource Source;
        public List<int> CardIds = new List<int>();
        public int Slot;
        public SevenCall Call;
        public bool Burn;
        public Rank AceValue = Rank.Ace;

        public CommandResult ApplyTo(PalaceGame game, int player)
        {
            switch (Type)
            {
                case NetCommandType.PlayCards: return game.PlayCards(player, Source, CardIds);
                case NetCommandType.PlayFaceDown: return game.PlayFaceDown(player, Slot);
                case NetCommandType.PickUp: return game.PickUp(player);
                case NetCommandType.DeclareSeven: return game.DeclareSeven(player, Call);
                case NetCommandType.Swap: return game.SwapCards(player, CardIds.Count > 0 ? CardIds[0] : -1, Slot);
                case NetCommandType.SwapDone: return game.FinishSwap(player);
                default: return game.ChooseAce(player, Burn, AceValue);
            }
        }
    }

    /// <summary>
    /// Turns messages into bytes and back. Also builds each player's PRIVATE view of the game:
    /// other players' hands and every face-down card are replaced by blank placeholders, so a
    /// player's device never receives information it shouldn't have (no cheating by snooping).
    /// </summary>
    public static class NetProtocol
    {
        // ------------------------------------------------------------------ lobby

        public static byte[] Hello(string name) => Build(MsgType.Hello, w => w.Write(name ?? "Player"));
        public static byte[] Ready(bool ready) => Build(MsgType.Ready, w => w.Write(ready));
        public static byte[] InGame() => Build(MsgType.InGame, w => { });
        public static byte[] Closing(string reason) => Build(MsgType.Closing, w => w.Write(reason ?? ""));
        public static byte[] Error(string error) => Build(MsgType.Error, w => w.Write(error ?? ""));

        /// <summary>A quick reaction (index into Reactions.Phrases) from a player; the host fills in who sent it.</summary>
        public static byte[] Reaction(int player, int index) => Build(MsgType.Reaction, w => { w.Write(player); w.Write((byte)index); });
        public static byte[] Rematch(int player) => Build(MsgType.Rematch, w => w.Write(player));

        public static byte[] Lobby(IReadOnlyList<LobbySeat> seats) => Build(MsgType.Lobby, w =>
        {
            w.Write(seats.Count);
            foreach (var s in seats)
            {
                w.Write(s.ClientId);
                w.Write(s.Name ?? "");
                w.Write(s.IsBot);
                w.Write(s.Ready);
            }
        });

        public static List<LobbySeat> ReadLobby(BinaryReader r)
        {
            int n = r.ReadInt32();
            var seats = new List<LobbySeat>(n);
            for (int i = 0; i < n; i++)
                seats.Add(new LobbySeat { ClientId = r.ReadUInt64(), Name = r.ReadString(), IsBot = r.ReadBoolean(), Ready = r.ReadBoolean() });
            return seats;
        }

        /// <summary>Host tells a client which player number they are.</summary>
        public static byte[] StartGame(int yourPlayerIndex) => Build(MsgType.StartGame, w => w.Write(yourPlayerIndex));

        // ------------------------------------------------------------------ commands

        public static byte[] Command(NetCommand c) => Build(MsgType.Command, w =>
        {
            w.Write((byte)c.Type);
            w.Write((byte)c.Source);
            w.Write(c.CardIds.Count);
            foreach (int id in c.CardIds) w.Write(id);
            w.Write(c.Slot);
            w.Write((byte)c.Call);
            w.Write(c.Burn);
            w.Write((byte)c.AceValue);
        });

        public static NetCommand ReadCommand(BinaryReader r)
        {
            var c = new NetCommand
            {
                Type = (NetCommandType)r.ReadByte(),
                Source = (PlaySource)r.ReadByte()
            };
            int n = r.ReadInt32();
            if (n < 0 || n > 64) throw new InvalidDataException("Bad command");
            for (int i = 0; i < n; i++) c.CardIds.Add(r.ReadInt32());
            c.Slot = r.ReadInt32();
            c.Call = (SevenCall)r.ReadByte();
            c.Burn = r.ReadBoolean();
            c.AceValue = (Rank)r.ReadByte();
            return c;
        }

        // ------------------------------------------------------------------ game updates

        /// <summary>Events plus the full table, as seen by <paramref name="viewer"/>.</summary>
        public static byte[] Update(GameState state, IReadOnlyList<GameEvent> events, int viewer) => Build(MsgType.Update, w =>
        {
            w.Write(viewer);
            w.Write(events.Count);
            foreach (var e in events) WriteEvent(w, e, viewer);
            WriteState(w, state, viewer);
        });

        public static (int viewer, List<GameEvent> events, GameState state) ReadUpdate(BinaryReader r)
        {
            int viewer = r.ReadInt32();
            int n = r.ReadInt32();
            var events = new List<GameEvent>(n);
            for (int i = 0; i < n; i++) events.Add(ReadEvent(r));
            return (viewer, events, ReadState(r));
        }

        private static int hiddenId = -1;
        private static Card Hidden() => new Card(hiddenId--, Rank.Two, Suit.Clubs);

        private static void WriteEvent(BinaryWriter w, GameEvent e, int viewer)
        {
            w.Write((byte)e.Type);
            w.Write(e.Player);
            // Cards drawn from the deck are secret to everyone but the person drawing them.
            bool secret = e.Type == GameEventType.CardsDrawn && e.Player != viewer;
            w.Write(e.Cards.Count);
            foreach (var c in e.Cards) WriteCard(w, c, secret);
            WriteInts(w, e.Slots);
            WriteInts(w, e.Players);
            w.Write((byte)e.Source);
            w.Write((byte)e.Call);
            w.Write(e.Burn);
            w.Write((byte)e.AceValue);
            w.Write(e.Count);
            w.Write(e.Position);
            w.Write(e.Direction);
        }

        private static GameEvent ReadEvent(BinaryReader r)
        {
            var e = new GameEvent { Type = (GameEventType)r.ReadByte(), Player = r.ReadInt32() };
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) e.Cards.Add(ReadCard(r));
            e.Slots = ReadInts(r);
            e.Players = ReadInts(r);
            e.Source = (PlaySource)r.ReadByte();
            e.Call = (SevenCall)r.ReadByte();
            e.Burn = r.ReadBoolean();
            e.AceValue = (Rank)r.ReadByte();
            e.Count = r.ReadInt32();
            e.Position = r.ReadInt32();
            e.Direction = r.ReadInt32();
            return e;
        }

        private static void WriteState(BinaryWriter w, GameState s, int viewer)
        {
            w.Write(s.Players.Count);
            foreach (var p in s.Players)
            {
                w.Write(p.Name ?? "");
                w.Write(p.IsBot);
                w.Write(p.FinishPosition);
                w.Write(p.SwapReady);
                bool hideHand = p.Index != viewer;
                w.Write(p.Hand.Count);
                foreach (var c in p.Hand) WriteCard(w, c, hideHand);
                foreach (var c in p.FaceUp) WriteSlot(w, c, false);
                foreach (var c in p.FaceDown) WriteSlot(w, c, true); // nobody may see face-down cards
            }
            w.Write(s.DrawPile.Count);   // only the size of the deck, never its order
            w.Write(s.Discard.Count);
            foreach (var c in s.Discard) WriteCard(w, c, false);
            WriteInts(w, s.FinishOrder);
            w.Write(s.TotalCards);
            w.Write(s.BurnedCount);
            w.Write(s.Current);
            w.Write(s.Direction);
            w.Write((byte)s.ActiveSeven);
            w.Write((byte)s.Phase);
            w.Write(s.TurnNumber);
            w.Write(s.PendingPlayer);
            w.Write(s.PendingCount);
            w.Write(s.AceValue.HasValue);
            w.Write((byte)(s.AceValue ?? Rank.Ace));
            w.Write(s.PendingAceValues.Count);
            foreach (var v in s.PendingAceValues) w.Write((byte)v);
            w.Write(s.PendingAceBurnAllowed);
        }

        private static GameState ReadState(BinaryReader r)
        {
            var s = new GameState();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var p = new PlayerState(i, r.ReadString(), r.ReadBoolean()) { FinishPosition = r.ReadInt32(), SwapReady = r.ReadBoolean() };
                int h = r.ReadInt32();
                for (int k = 0; k < h; k++) p.Hand.Add(ReadCard(r));
                for (int k = 0; k < p.FaceUp.Length; k++) p.FaceUp[k] = ReadSlot(r);
                for (int k = 0; k < p.FaceDown.Length; k++) p.FaceDown[k] = ReadSlot(r);
                s.Players.Add(p);
            }
            int deck = r.ReadInt32();
            for (int i = 0; i < deck; i++) s.DrawPile.Add(Hidden());
            int d = r.ReadInt32();
            for (int i = 0; i < d; i++) s.Discard.Add(ReadCard(r));
            s.FinishOrder.AddRange(ReadInts(r));
            s.TotalCards = r.ReadInt32();
            s.BurnedCount = r.ReadInt32();
            s.Current = r.ReadInt32();
            s.Direction = r.ReadInt32();
            s.ActiveSeven = (SevenCall)r.ReadByte();
            s.Phase = (GamePhase)r.ReadByte();
            s.TurnNumber = r.ReadInt32();
            s.PendingPlayer = r.ReadInt32();
            s.PendingCount = r.ReadInt32();
            bool hasAce = r.ReadBoolean();
            var ace = (Rank)r.ReadByte();
            s.AceValue = hasAce ? ace : (Rank?)null;
            int pv = r.ReadInt32();
            for (int i = 0; i < pv; i++) s.PendingAceValues.Add((Rank)r.ReadByte());
            s.PendingAceBurnAllowed = r.ReadBoolean();
            return s;
        }

        // ------------------------------------------------------------------ helpers

        private static void WriteCard(BinaryWriter w, Card c, bool hidden)
        {
            if (hidden)
            {
                w.Write(-1);
                w.Write((byte)0);
                w.Write((byte)0);
                return;
            }
            w.Write(c.Id);
            w.Write((byte)c.Rank);
            w.Write((byte)c.Suit);
        }

        private static Card ReadCard(BinaryReader r)
        {
            int id = r.ReadInt32();
            byte rank = r.ReadByte();
            byte suit = r.ReadByte();
            return id < 0 ? Hidden() : new Card(id, (Rank)rank, (Suit)suit);
        }

        private static void WriteSlot(BinaryWriter w, Card? c, bool hidden)
        {
            w.Write(c.HasValue);
            if (c.HasValue) WriteCard(w, c.Value, hidden);
        }

        private static Card? ReadSlot(BinaryReader r) => r.ReadBoolean() ? ReadCard(r) : (Card?)null;

        private static void WriteInts(BinaryWriter w, List<int> values)
        {
            w.Write(values.Count);
            foreach (int v in values) w.Write(v);
        }

        private static List<int> ReadInts(BinaryReader r)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > 4096) throw new InvalidDataException("Bad list");
            var list = new List<int>(n);
            for (int i = 0; i < n; i++) list.Add(r.ReadInt32());
            return list;
        }

        private static byte[] Build(MsgType type, Action<BinaryWriter> body)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((byte)type);
                body(w);
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}
