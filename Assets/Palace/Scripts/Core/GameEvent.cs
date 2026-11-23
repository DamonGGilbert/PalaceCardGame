using System.Collections.Generic;

namespace Palace.Core
{
    public enum GameEventType
    {
        GameStarted,
        CardsPlayed,        // Player, Cards, Source, Slots (face-up/face-down slot indices)
        FaceDownRevealed,   // Player, Cards[0], Slots[0]
        PileCollected,      // Player, Cards (the whole pile)
        PileBurned,         // Player, Cards (burned)
        CardsDrawn,         // Player, Cards (only show values to the owner!)
        PlayersSkipped,     // Player (who played the 8s), Players (skipped, in order)
        DirectionReversed,  // Player, Count (queens), Direction (new)
        Reset,              // Player (played a 2)
        SevenCallNeeded,    // Player
        SevenDeclared,      // Player, Call
        AceChoiceNeeded,    // Player
        AceChosen,          // Player, Burn
        PlayerFinished,     // Player, Position
        TurnChanged,        // Player (whose turn it now is)
        GameOver            // Players (finishing order)
    }

    /// <summary>
    /// Something that happened, in order. The Unity side plays these back as animations
    /// and sounds. Later the same events can be sent over the network.
    /// </summary>
    public sealed class GameEvent
    {
        public GameEventType Type;
        public int Player = -1;
        public List<Card> Cards = new List<Card>();
        public List<int> Slots = new List<int>();
        public List<int> Players = new List<int>();
        public PlaySource Source;
        public SevenCall Call;
        public bool Burn;
        public int Count;
        public int Position;
        public int Direction;

        public override string ToString()
        {
            switch (Type)
            {
                case GameEventType.CardsPlayed: return $"P{Player} played {string.Join(" ", Cards)} from {Source}";
                case GameEventType.FaceDownRevealed: return $"P{Player} revealed {Cards[0]}";
                case GameEventType.PileCollected: return $"P{Player} picked up {Cards.Count}";
                case GameEventType.PileBurned: return $"P{Player} burned {Cards.Count}";
                case GameEventType.CardsDrawn: return $"P{Player} drew {Cards.Count}";
                case GameEventType.PlayersSkipped: return $"P{Player} skipped {string.Join(",", Players)}";
                case GameEventType.DirectionReversed: return $"P{Player} reversed x{Count} -> {Direction}";
                case GameEventType.SevenDeclared: return $"P{Player} called {Call}";
                case GameEventType.AceChosen: return $"P{Player} ace {(Burn ? "burn" : "high")}";
                case GameEventType.PlayerFinished: return $"P{Player} finished #{Position}";
                case GameEventType.TurnChanged: return $"Turn -> P{Player}";
                default: return $"{Type} P{Player}";
            }
        }
    }

    public sealed class CommandResult
    {
        public bool Ok;
        public string Error;
        public readonly List<GameEvent> Events = new List<GameEvent>();

        public static CommandResult Fail(string error) => new CommandResult { Ok = false, Error = error };
    }
}
