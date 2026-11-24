using Palace.Core;

namespace Palace
{
    /// <summary>
    /// Quick reactions: a fixed set of friendly phrases (no free typing, so nothing to moderate).
    /// Players tap one and it pops up in a speech bubble over their seat. Computer players react
    /// now and then too, always kindly. Online, only the index is sent and the host checks it.
    /// </summary>
    public static class Reactions
    {
        /// <summary>The phrases, in button order. Add new ones at the end so online indexes stay the same.</summary>
        public static readonly string[] Phrases =
        {
            "Nice one!", "Well played!", "Unlucky!", "Ouch, sorry!",
            "Good game!", "So close!", "Big brain move!", "You're on fire!",
            "Not the pile!", "Respect!", "Hehe, oops!", "My turn to shine!"
        };

        /// <summary>Every reaction: the phrases first, then the emojis (EmojiArt), in that order.</summary>
        public static int Count => Phrases.Length + EmojiArt.Count;
        public static bool IsEmoji(int index) => index >= Phrases.Length;
        public static int Emoji(int emoji) => Phrases.Length + emoji;

        // Emoji numbers (see EmojiArt.Names).
        public const int Smile = 0, Laughing = 1, Wink = 2, Wow = 3, Aww = 4, Cool = 5, HeartEyes = 6, Heart = 7, Fire = 8, Crown = 9, Star = 10, Cards = 11;

        public const int NiceOne = 0, WellPlayed = 1, Unlucky = 2, OuchSorry = 3, GoodGame = 4, SoClose = 5,
            BigBrain = 6, OnFire = 7, NotThePile = 8, Respect = 9, Oops = 10, MyTurn = 11;

        /// <summary>
        /// How a computer player might react to something that just happened. Returns the reaction
        /// index and who says it, or index -1 for no reaction. Only kind or playful lines.
        /// </summary>
        public static (int player, int index) BotReaction(GameEvent e, GameState s, System.Random rng)
        {
            int Bot(int except)
            {
                // A random computer player still at the table, not the one the event is about.
                int n = s.Players.Count, start = rng.Next(n);
                for (int k = 0; k < n; k++)
                {
                    int i = (start + k) % n;
                    if (i != except && s.Players[i].IsBot) return i;
                }
                return -1;
            }

            int[] pick(params int[] options) => options;
            int[] lines;
            int who;
            switch (e.Type)
            {
                case GameEventType.PileBurned:
                    if (rng.NextDouble() > 0.35) return (-1, -1);
                    who = Bot(e.Player);
                    lines = pick(NiceOne, OnFire, BigBrain, Respect, Emoji(Fire), Emoji(Fire), Emoji(Wow));
                    break;
                case GameEventType.PileCollected:
                    if (e.Cards.Count < 5 || rng.NextDouble() > 0.5) return (-1, -1);
                    if (s.Players[e.Player].IsBot && rng.NextDouble() < 0.5) { who = e.Player; lines = pick(NotThePile, Oops); }
                    else { who = Bot(e.Player); lines = pick(Unlucky, OuchSorry, SoClose, Emoji(Aww), Emoji(Wow)); }
                    break;
                case GameEventType.PlayersSkipped:
                    if (!s.Players[e.Player].IsBot || rng.NextDouble() > 0.3) return (-1, -1);
                    who = e.Player;
                    lines = pick(Oops, OuchSorry, Emoji(Wink));
                    break;
                case GameEventType.PlayerFinished:
                    if (e.Position != 1 || rng.NextDouble() > 0.6) return (-1, -1);
                    who = Bot(e.Player);
                    lines = pick(WellPlayed, Respect, NiceOne, Emoji(Crown), Emoji(Star));
                    break;
                case GameEventType.GameOver:
                    if (rng.NextDouble() > 0.7) return (-1, -1);
                    who = Bot(-1);
                    lines = pick(GoodGame, WellPlayed, Emoji(Heart), Emoji(Smile));
                    break;
                default:
                    return (-1, -1);
            }
            return who < 0 ? (-1, -1) : (who, lines[rng.Next(lines.Length)]);
        }

        /// <summary>A computer player's friendly reply to a person's reaction (or -1 for none).</summary>
        public static int BotReply(int index, System.Random rng)
        {
            if (rng.NextDouble() > 0.35) return -1;
            switch (index)
            {
                case GoodGame: return GoodGame;
                case WellPlayed: case NiceOne: case Respect: case BigBrain: case OnFire: return rng.NextDouble() < 0.5 ? Respect : Oops;
                case Unlucky: case OuchSorry: case SoClose: return rng.NextDouble() < 0.5 ? MyTurn : SoClose;
                case NotThePile: return Unlucky;
                default: return rng.NextDouble() < 0.5 ? Oops : NiceOne;
            }
        }
    }
}
