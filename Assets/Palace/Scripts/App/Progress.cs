using System;
using System.Collections.Generic;
using Palace.Core;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Everything that gives players a reason to come back, saved on the device (and in the cloud
    /// backup): lifetime stats, XP and levels, achievements, three daily challenges and a daily
    /// streak reward. All rewards are coins for the Customise screen; the rules of the game are
    /// never affected.
    ///
    /// The game reports the local player's moves with OnLocalEvent() as they are animated, and the
    /// result with FinishGame(). Unlocks raise Celebrate so the table can show a banner straight away.
    /// Every number worth tuning (XP, coin rewards, targets) is in the tables at the top.
    /// </summary>
    public static class Progress
    {
        public enum Stat
        {
            GamesPlayed, Wins, Shedheads, CardsPlayed, Burns, AceBurns, PickUps, BiggestPickUp, PlayersSkipped,
            BiggestSkip, Reverses, SevensCalled, MultiPlays, Quads, WinStreak, BestWinStreak, HardWins, CleanWins,
            BigTableWins, TenPlayerWins, ComebackWins, MultiplayerGames, DailiesDone, DayStreak, BestDayStreak,
            TutorialDone, Level
        }

        /// <summary>A challenge or achievement target: reach Target in one stat.</summary>
        public sealed class Goal
        {
            public string Id, Title, Description;
            public Stat Stat;
            public int Target, Coins;
            public Goal(string id, string title, string description, Stat stat, int target, int coins)
            {
                Id = id; Title = title; Description = description; Stat = stat; Target = target; Coins = coins;
            }
        }

        // ------------------------------------------------------------------ tuning

        public const int DailyBonusCoins = 75, DailyBonusXp = 50, TutorialXp = 60;
        public static int XpToNextLevel(int level) => 100 + 25 * (level - 1);
        public static int LevelUpCoins(int newLevel) => 40 + 10 * newLevel;
        public static int StreakCoins(int day) => 10 * Mathf.Clamp(day, 1, 7);

        public static readonly Goal[] Achievements =
        {
            new Goal("tutorial", "Class Dismissed", "Finish the guided game.", Stat.TutorialDone, 1, 100),
            new Goal("win1", "Winner Winner", "Win your first game.", Stat.Wins, 1, 50),
            new Goal("win10", "Card Shark", "Win 10 games.", Stat.Wins, 10, 150),
            new Goal("win50", "Top Dog", "Win 50 games.", Stat.Wins, 50, 400),
            new Goal("games10", "Regular", "Play 10 games.", Stat.GamesPlayed, 10, 75),
            new Goal("games50", "Pub Legend", "Play 50 games.", Stat.GamesPlayed, 50, 200),
            new Goal("games200", "Lifer", "Play 200 games.", Stat.GamesPlayed, 200, 500),
            new Goal("burn25", "Pyromaniac", "Burn the pile 25 times.", Stat.Burns, 25, 100),
            new Goal("burn100", "Inferno", "Burn the pile 100 times.", Stat.Burns, 100, 250),
            new Goal("streak3", "On Fire", "Win 3 games in a row.", Stat.BestWinStreak, 3, 150),
            new Goal("streak5", "Unstoppable", "Win 5 games in a row.", Stat.BestWinStreak, 5, 300),
            new Goal("bigtable", "Crowd Pleaser", "Win a game with 6 or more players.", Stat.BigTableWins, 1, 150),
            new Goal("fullhouse", "Full House", "Win a 10-player game.", Stat.TenPlayerWins, 1, 250),
            new Goal("hard1", "Giant Slayer", "Win against Hard computers.", Stat.HardWins, 1, 150),
            new Goal("hard10", "Boss Level", "Win 10 games against Hard computers.", Stat.HardWins, 10, 400),
            new Goal("clean", "Untouchable", "Win without picking up the pile once.", Stat.CleanWins, 1, 150),
            new Goal("comeback", "Comeback Kid", "Win after picking up 10 or more cards at once.", Stat.ComebackWins, 1, 200),
            new Goal("hoarder", "Hoarder", "Pick up 15 or more cards at once.", Stat.BiggestPickUp, 15, 50),
            new Goal("skip3", "Skip Day", "Skip 3 players with one play.", Stat.BiggestSkip, 3, 100),
            new Goal("quad", "Four of a Kind", "Play four cards of the same rank at once.", Stat.Quads, 1, 150),
            new Goal("social1", "Social Butterfly", "Finish a multiplayer game.", Stat.MultiplayerGames, 1, 75),
            new Goal("social10", "Party Animal", "Finish 10 multiplayer games.", Stat.MultiplayerGames, 10, 200),
            new Goal("shedhead", "Wear the Crown", "Be the Shedhead for the first time.", Stat.Shedheads, 1, 25),
            new Goal("daily10", "Daily Grind", "Complete 10 daily challenges.", Stat.DailiesDone, 10, 150),
            new Goal("days7", "Week Streak", "Play 7 days in a row.", Stat.BestDayStreak, 7, 200),
            new Goal("level10", "Double Digits", "Reach level 10.", Stat.Level, 10, 250),
            new Goal("cards1000", "Card Thrower", "Play 1,000 cards.", Stat.CardsPlayed, 1000, 200)
        };

        /// <summary>What a daily challenge counts.</summary>
        public enum Track
        {
            Games, Wins, Burns, Skips, Reverses, MultiPlays, TopHalf, NoPickUpGame, Sevens, AceBurns, BigTableGame,
            HardWin, SpecialCards
        }

        public sealed class Daily
        {
            public string Id, Text;
            public Track Track;
            public int Target, Coins;
            public Daily(string id, string text, Track track, int target, int coins)
            {
                Id = id; Text = text; Track = track; Target = target; Coins = coins;
            }
        }

        public static readonly Daily[] DailyPool =
        {
            new Daily("games3", "Play 3 games", Track.Games, 3, 40),
            new Daily("win1", "Win a game", Track.Wins, 1, 50),
            new Daily("win2", "Win 2 games", Track.Wins, 2, 80),
            new Daily("burn3", "Burn the pile 3 times", Track.Burns, 3, 50),
            new Daily("skip3", "Skip 3 players with 8s", Track.Skips, 3, 40),
            new Daily("rev2", "Reverse play twice with a Queen", Track.Reverses, 2, 40),
            new Daily("multi4", "Play 2 or more cards at once 4 times", Track.MultiPlays, 4, 40),
            new Daily("top2", "Finish in the top half twice", Track.TopHalf, 2, 60),
            new Daily("clean1", "Finish a game without picking up the pile", Track.NoPickUpGame, 1, 70),
            new Daily("seven3", "Play a 7 and make the call 3 times", Track.Sevens, 3, 40),
            new Daily("ace1", "Burn the pile with an Ace", Track.AceBurns, 1, 50),
            new Daily("big1", "Finish a game with 5 or more players", Track.BigTableGame, 1, 50),
            new Daily("hard1", "Beat the Hard computers", Track.HardWin, 1, 90),
            new Daily("special5", "Play 5 special cards (2, 7, 8, 10, Q or A)", Track.SpecialCards, 5, 40)
        };

        // ------------------------------------------------------------------ events

        /// <summary>Something worth a banner: (headline, detail), e.g. ("Achievement unlocked!", "Pyromaniac  +100 coins").</summary>
        public static event Action<string, string> Celebrate;
        public static event Action Changed;

        // ------------------------------------------------------------------ stats, XP, levels

        public static int Get(Stat s) => PlayerPrefs.GetInt("palace.stat." + s, 0);

        private static void Set(Stat s, int value) => PlayerPrefs.SetInt("palace.stat." + s, value);
        private static void Add(Stat s, int amount = 1) => Set(s, Get(s) + amount);
        private static void Max(Stat s, int value) { if (value > Get(s)) Set(s, value); }

        public static int TotalXp => PlayerPrefs.GetInt("palace.xp", 0);

        public static int LevelFor(int xp, out int into, out int needed)
        {
            int level = 1;
            needed = XpToNextLevel(level);
            while (xp >= needed)
            {
                xp -= needed;
                level++;
                needed = XpToNextLevel(level);
            }
            into = xp;
            return level;
        }

        public static int Level => LevelFor(TotalXp, out _, out _);

        /// <summary>0..1 progress through the current level.</summary>
        public static float LevelFraction
        {
            get
            {
                LevelFor(TotalXp, out int into, out int needed);
                return into / (float)needed;
            }
        }

        private static void AddXp(int amount)
        {
            if (amount <= 0) return;
            int before = Level;
            PlayerPrefs.SetInt("palace.xp", TotalXp + amount);
            gameXp += amount;
            int after = Level;
            for (int l = before + 1; l <= after; l++)
            {
                int coins = LevelUpCoins(l);
                Cosmetics.AddCoins(coins);
                Earned(Loc.F("Level {0}!", l), Loc.F("+{0} coins", coins));
            }
            Set(Stat.Level, after);
        }

        // ------------------------------------------------------------------ one game

        private static bool inGame, pickedUp, bigPickUp;
        private static int gameXp;
        private static readonly List<string> gameLines = new List<string>();

        /// <summary>Call when a game starts on this device with a seat for the local player.</summary>
        public static void StartGame()
        {
            inGame = true;
            pickedUp = bigPickUp = false;
            gameXp = 0;
            gameLines.Clear();
            RollDailiesIfNeeded();
        }

        /// <summary>Something the local player did. Called as each event is shown on the table.</summary>
        public static void OnLocalEvent(GameEvent e, int localPlayer)
        {
            if (!inGame || e.Player != localPlayer) return;
            switch (e.Type)
            {
                case GameEventType.CardsPlayed:
                {
                    int n = e.Cards.Count;
                    Add(Stat.CardsPlayed, n);
                    if (n >= 2) { Add(Stat.MultiPlays); Bump(Track.MultiPlays); }
                    if (n >= 4) Add(Stat.Quads);
                    Rank r = e.Cards[0].Rank;
                    if (r == Rank.Two || r == Rank.Seven || r == Rank.Eight || r == Rank.Ten || r == Rank.Queen || r == Rank.Ace)
                        Bump(Track.SpecialCards, n);
                    break;
                }
                case GameEventType.PileBurned:
                    Add(Stat.Burns);
                    Bump(Track.Burns);
                    break;
                case GameEventType.AceChosen:
                    if (e.Burn) { Add(Stat.AceBurns); Bump(Track.AceBurns); }
                    break;
                case GameEventType.PlayersSkipped:
                    Add(Stat.PlayersSkipped, e.Players.Count);
                    Max(Stat.BiggestSkip, e.Players.Count);
                    Bump(Track.Skips, e.Players.Count);
                    break;
                case GameEventType.DirectionReversed:
                    Add(Stat.Reverses);
                    Bump(Track.Reverses);
                    break;
                case GameEventType.SevenDeclared:
                    Add(Stat.SevensCalled);
                    Bump(Track.Sevens);
                    break;
                case GameEventType.PileCollected:
                    pickedUp = true;
                    if (e.Cards.Count >= 10) bigPickUp = true;
                    Add(Stat.PickUps);
                    Max(Stat.BiggestPickUp, e.Cards.Count);
                    break;
                default:
                    return;
            }
            CheckAchievements();
            Save();
        }

        /// <summary>What this game earned, for the results screen.</summary>
        public sealed class Report
        {
            public int Xp, LevelBefore, Level;
            public float FractionBefore, Fraction;
            public readonly List<string> Lines = new List<string>();
        }

        /// <summary>
        /// The local player finished in <paramref name="position"/> of <paramref name="players"/>.
        /// botLevel is -1 for multiplayer games.
        /// </summary>
        public static Report FinishGame(int position, int players, int botLevel, bool multiplayer, bool tutorial)
        {
            var report = new Report();
            int xpBefore = TotalXp - gameXp; // XP already earned this game (mid-game level-ups) is shown as part of it
            report.LevelBefore = LevelFor(xpBefore, out int into0, out int need0);
            report.FractionBefore = into0 / (float)need0;
            if (!inGame) StartGame();
            inGame = false;

            bool won = position == 1;
            Add(Stat.GamesPlayed);
            Bump(Track.Games);
            if (multiplayer) Add(Stat.MultiplayerGames);
            if (position == players) Add(Stat.Shedheads);
            if (players >= 5) Bump(Track.BigTableGame);
            if (position <= players / 2) Bump(Track.TopHalf);
            if (!pickedUp) Bump(Track.NoPickUpGame);
            if (tutorial) Set(Stat.TutorialDone, 1);

            if (won)
            {
                Add(Stat.Wins);
                Bump(Track.Wins);
                Add(Stat.WinStreak);
                Max(Stat.BestWinStreak, Get(Stat.WinStreak));
                if (botLevel == (int)BotLevel.Hard && !multiplayer) { Add(Stat.HardWins); Bump(Track.HardWin); }
                if (!pickedUp) Add(Stat.CleanWins);
                if (bigPickUp) Add(Stat.ComebackWins);
                if (players >= 6) Add(Stat.BigTableWins);
                if (players >= 10) Add(Stat.TenPlayerWins);
            }
            else Set(Stat.WinStreak, 0);

            // XP: everyone gets some, more for finishing higher.
            int xp = tutorial ? TutorialXp : 20 + Mathf.RoundToInt(30f * (players - position) / Mathf.Max(1, players - 1));
            if (won && Get(Stat.WinStreak) >= 2) xp += 10; // streak bonus
            AddXp(xp);

            CheckAchievements();
            Save();

            report.Xp = gameXp;
            report.Level = LevelFor(TotalXp, out int into1, out int need1);
            report.Fraction = into1 / (float)need1;
            report.Lines.AddRange(gameLines);
            return report;
        }

        // ------------------------------------------------------------------ achievements

        public static bool IsUnlocked(Goal a) => PlayerPrefs.GetInt("palace.ach." + a.Id, 0) == 1;

        public static int UnlockedCount
        {
            get
            {
                int n = 0;
                foreach (var a in Achievements) if (IsUnlocked(a)) n++;
                return n;
            }
        }

        private static void CheckAchievements()
        {
            foreach (var a in Achievements)
            {
                if (IsUnlocked(a) || Get(a.Stat) < a.Target) continue;
                PlayerPrefs.SetInt("palace.ach." + a.Id, 1);
                Cosmetics.AddCoins(a.Coins);
                Earned(Loc.T("Achievement unlocked!"), Loc.T(a.Title) + "  " + Loc.F("+{0} coins", a.Coins));
            }
        }

        // ------------------------------------------------------------------ daily challenges

        /// <summary>Days since 1 Jan 2024 in the player's own time zone (new challenges at local midnight).</summary>
        public static int Today => (int)(DateTime.Now.Date - new DateTime(2024, 1, 1)).TotalDays;

        public static TimeSpan UntilTomorrow => DateTime.Now.Date.AddDays(1) - DateTime.Now;

        /// <summary>Today's three challenges.</summary>
        public static Daily[] Dailies
        {
            get
            {
                RollDailiesIfNeeded();
                var ids = PlayerPrefs.GetString("palace.daily.ids", "").Split(',');
                var list = new List<Daily>();
                foreach (string id in ids)
                    foreach (var d in DailyPool)
                        if (d.Id == id) list.Add(d);
                return list.ToArray();
            }
        }

        public static int DailyProgress(int slot) => PlayerPrefs.GetInt("palace.daily.p" + slot, 0);
        public static bool DailyDone(int slot) => PlayerPrefs.GetInt("palace.daily.done" + slot, 0) == 1;
        public static bool DailyBonusClaimed => PlayerPrefs.GetInt("palace.daily.bonus", 0) == 1;

        public static int DailiesLeft
        {
            get
            {
                int n = 0;
                var d = Dailies;
                for (int i = 0; i < d.Length; i++) if (!DailyDone(i)) n++;
                return n;
            }
        }

        private static void RollDailiesIfNeeded()
        {
            int today = Today;
            if (PlayerPrefs.GetInt("palace.daily.day", -1) == today && PlayerPrefs.HasKey("palace.daily.ids")) return;

            // The same three for everyone on a given day, never three of the same kind.
            var rng = new System.Random(today * 7919 + 17);
            var picked = new List<Daily>();
            while (picked.Count < 3)
            {
                Daily d = DailyPool[rng.Next(DailyPool.Length)];
                if (picked.Exists(p => p.Track == d.Track)) continue;
                picked.Add(d);
            }
            PlayerPrefs.SetInt("palace.daily.day", today);
            PlayerPrefs.SetString("palace.daily.ids", string.Join(",", picked.ConvertAll(p => p.Id)));
            for (int i = 0; i < 3; i++)
            {
                PlayerPrefs.SetInt("palace.daily.p" + i, 0);
                PlayerPrefs.SetInt("palace.daily.done" + i, 0);
            }
            PlayerPrefs.SetInt("palace.daily.bonus", 0);
        }

        private static void Bump(Track track, int amount = 1)
        {
            var d = Dailies;
            bool completedOne = false;
            for (int i = 0; i < d.Length; i++)
            {
                if (d[i].Track != track || DailyDone(i)) continue;
                int p = Mathf.Min(d[i].Target, DailyProgress(i) + amount);
                PlayerPrefs.SetInt("palace.daily.p" + i, p);
                if (p < d[i].Target) continue;
                PlayerPrefs.SetInt("palace.daily.done" + i, 1);
                Add(Stat.DailiesDone);
                Cosmetics.AddCoins(d[i].Coins);
                Earned(Loc.T("Daily challenge complete!"), Loc.T(d[i].Text) + "  " + Loc.F("+{0} coins", d[i].Coins));
                completedOne = true;
            }

            if (completedOne && !DailyBonusClaimed && DailiesLeft == 0)
            {
                PlayerPrefs.SetInt("palace.daily.bonus", 1);
                Cosmetics.AddCoins(DailyBonusCoins);
                Earned(Loc.T("All 3 daily challenges done!"), Loc.F("+{0} coins", DailyBonusCoins) + "  " + Loc.F("+{0} XP", DailyBonusXp));
                AddXp(DailyBonusXp);
            }
        }

        // ------------------------------------------------------------------ daily streak

        public static int DayStreak => Get(Stat.DayStreak);

        /// <summary>
        /// Call when the menu opens. The first visit of each day pays a streak reward (more for each
        /// day in a row, up to day 7). Returns the coins paid, or 0 if already collected today.
        /// </summary>
        public static int CheckIn()
        {
            int today = Today;
            int last = PlayerPrefs.GetInt("palace.streak.last", -10);
            if (last == today) return 0;

            int streak = last == today - 1 ? DayStreak + 1 : 1;
            Set(Stat.DayStreak, streak);
            Max(Stat.BestDayStreak, streak);
            PlayerPrefs.SetInt("palace.streak.last", today);
            int coins = StreakCoins(streak);
            Cosmetics.AddCoins(coins);
            CheckAchievements();
            Save();
            return coins;
        }

        // ------------------------------------------------------------------ saving and cloud

        private static void Earned(string headline, string detail)
        {
            gameLines.Add(headline + "  " + detail);
            Celebrate?.Invoke(headline, detail);
        }

        private static void Save()
        {
            PlayerPrefs.Save();
            CloudProgress.MarkLocalChange();
            Changed?.Invoke();
        }

        [Serializable]
        private class SaveData
        {
            public int xp, streakLast;
            public List<string> stats = new List<string>();        // "Name=value"
            public List<string> achievements = new List<string>(); // unlocked ids
        }

        public static string ExportJson()
        {
            var data = new SaveData { xp = TotalXp, streakLast = PlayerPrefs.GetInt("palace.streak.last", -10) };
            foreach (Stat s in Enum.GetValues(typeof(Stat))) data.stats.Add(s + "=" + Get(s));
            foreach (var a in Achievements) if (IsUnlocked(a)) data.achievements.Add(a.Id);
            return JsonUtility.ToJson(data);
        }

        /// <summary>Cloud restore: unlocked achievements are always kept; stats come from whichever copy has more XP.</summary>
        public static void ImportJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch (ArgumentException) { return; }
            if (data == null) return;

            foreach (string id in data.achievements) PlayerPrefs.SetInt("palace.ach." + id, 1);
            if (data.xp > TotalXp)
            {
                PlayerPrefs.SetInt("palace.xp", data.xp);
                PlayerPrefs.SetInt("palace.streak.last", Mathf.Max(data.streakLast, PlayerPrefs.GetInt("palace.streak.last", -10)));
                foreach (string pair in data.stats)
                {
                    int eq = pair.IndexOf('=');
                    if (eq > 0 && Enum.TryParse(pair.Substring(0, eq), out Stat s) && int.TryParse(pair.Substring(eq + 1), out int v)) Set(s, v);
                }
            }
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
