using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// The Progress screen: today's daily challenges and streak, every achievement with its
    /// progress, and lifetime stats. Rebuilt each time it opens so it is always up to date.
    /// </summary>
    public class ProgressMenu : MonoBehaviour
    {
        private static readonly string[] TabNames = { "Daily", "Achievements", "Stats" };

        private RectTransform content;
        private TextMeshProUGUI levelLabel, xpLabel;
        private Image xpFill;
        private GameObject screen;
        private int tab;

        public void Build(RectTransform menuRoot, Action onBack)
        {
            screen = menuRoot.gameObject;
            var panel = UIKit.Panel(menuRoot, new Vector2(1500f, 980f), Vector2.zero);
            Transform p = panel.transform;
            UIKit.Label(p, "Progress", 54f, new Vector2(600f, 80f), new Vector2(-380f, 425f), TextAlignmentOptions.Left, UIKit.Gold, true);

            levelLabel = UIKit.Label(p, "", 34f, new Vector2(300f, 50f), new Vector2(340f, 440f), TextAlignmentOptions.Right, UIKit.Gold, true);
            xpLabel = UIKit.Label(p, "", 22f, new Vector2(300f, 34f), new Vector2(340f, 400f), TextAlignmentOptions.Right, UIKit.Muted);
            xpFill = Bar(p, new Vector2(240f, 18f), new Vector2(580f, 420f));

            UIKit.Segmented(p, TabNames, 0, new Vector2(1100f, 64f), new Vector2(0f, 330f), i =>
            {
                tab = i;
                Refresh();
            });

            var view = UIBuild.Image("Viewport", p, null, new Color(0f, 0f, 0f, 0.001f), new Vector2(1420f, 660f), new Vector2(0f, -30f));
            view.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            content = UIBuild.Rect("Content", view.transform, new Vector2(1420f, 660f), Vector2.zero);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;

            UIKit.Button(p, "Back", new Vector2(220f, 70f), new Vector2(0f, -435f), () => onBack());
            Progress.Changed += OnChanged;
        }

        private void OnDestroy() => Progress.Changed -= OnChanged;

        private void OnChanged()
        {
            if (this != null && screen != null && screen.activeInHierarchy) Refresh();
        }

        public void Open() => Refresh();

        /// <summary>Testing only: show one tab for a screenshot.</summary>
        public void TestShow(int index)
        {
            tab = index;
            Refresh();
        }

        // ------------------------------------------------------------------ building

        private void Refresh()
        {
            foreach (Transform child in content) Destroy(child.gameObject);
            content.anchoredPosition = Vector2.zero;

            int level = Progress.LevelFor(Progress.TotalXp, out int into, out int needed);
            levelLabel.text = Loc.F("Level {0}", level);
            xpLabel.text = Loc.F("{0} / {1} XP", into, needed);
            SetBar(xpFill, 240f, into / (float)needed);

            float h = tab == 0 ? BuildDaily() : tab == 1 ? BuildAchievements() : BuildStats();
            content.sizeDelta = new Vector2(1420f, Mathf.Max(660f, h));
        }

        private float BuildDaily()
        {
            float y = -20f;
            int streak = Progress.DayStreak;
            Text(Loc.F("Day streak: {0}", streak), 34f, UIKit.Gold, new Vector2(0f, y - 25f), true);
            Text(Loc.F("Play every day for a bigger daily reward: {0} coins tomorrow.", Progress.StreakCoins(streak + 1)), 24f, UIKit.Muted, new Vector2(0f, y - 70f));
            y -= 120f;

            var dailies = Progress.Dailies;
            for (int i = 0; i < dailies.Length; i++)
            {
                var d = dailies[i];
                bool done = Progress.DailyDone(i);
                var card = UIKit.Panel(content, new Vector2(1240f, 120f), new Vector2(0f, y - 60f),
                    done ? new Color(0.22f, 0.32f, 0.16f, 0.95f) : new Color(0f, 0f, 0f, 0.35f));
                Pin(card.rectTransform);
                UIKit.Label(card.transform, d.Text, 30f, new Vector2(820f, 50f), new Vector2(-180f, 22f), TextAlignmentOptions.Left, UIKit.Text, true);
                int p = Progress.DailyProgress(i);
                var fill = Bar(card.transform, new Vector2(600f, 20f), new Vector2(-290f, -28f));
                SetBar(fill, 600f, p / (float)d.Target);
                UIKit.Label(card.transform, done ? Loc.T("Done!") : Loc.F("{0} / {1}", p, d.Target), 24f, new Vector2(200f, 34f),
                    new Vector2(130f, -28f), TextAlignmentOptions.Left, done ? UIKit.Gold : UIKit.Muted, true);
                UIKit.Label(card.transform, Loc.F("+{0} coins", d.Coins), 30f, new Vector2(260f, 50f), new Vector2(470f, 0f),
                    TextAlignmentOptions.Right, UIKit.Gold, true);
                y -= 136f;
            }

            string bonus = Progress.DailyBonusClaimed
                ? Loc.T("Daily bonus collected. Come back tomorrow for new challenges!")
                : Loc.F("Finish all 3 for a bonus: +{0} coins and +{1} XP", Progress.DailyBonusCoins, Progress.DailyBonusXp);
            Text(bonus, 26f, UIKit.Text, new Vector2(0f, y - 30f));
            TimeSpan left = Progress.UntilTomorrow;
            Text(Loc.F("New challenges in {0}h {1}m", (int)left.TotalHours, left.Minutes), 22f, UIKit.Muted, new Vector2(0f, y - 72f));
            return -(y - 110f);
        }

        private float BuildAchievements()
        {
            var list = Progress.Achievements;
            Text(Loc.F("{0} of {1} unlocked", Progress.UnlockedCount, list.Length), 28f, UIKit.Gold, new Vector2(0f, -36f), true);
            float top = -80f;
            for (int i = 0; i < list.Length; i++)
            {
                var a = list[i];
                bool unlocked = Progress.IsUnlocked(a);
                int col = i % 2, row = i / 2;
                var pos = new Vector2(col == 0 ? -355f : 355f, top - 60f - row * 132f);
                var card = UIKit.Panel(content, new Vector2(690f, 120f), pos,
                    unlocked ? new Color(0.30f, 0.24f, 0.10f, 0.95f) : new Color(0f, 0f, 0f, 0.35f));
                Pin(card.rectTransform);
                UIKit.Label(card.transform, a.Title, 28f, new Vector2(460f, 40f),
                    new Vector2(-100f, 30f), TextAlignmentOptions.Left, unlocked ? UIKit.Gold : UIKit.Text, true);
                var desc = UIKit.Label(card.transform, a.Description, 21f, new Vector2(460f, 30f), new Vector2(-100f, -4f), TextAlignmentOptions.Left, UIKit.Muted);
                desc.textWrappingMode = TextWrappingModes.Normal;
                int value = Mathf.Min(Progress.Get(a.Stat), a.Target);
                var fill = Bar(card.transform, new Vector2(380f, 14f), new Vector2(-140f, -38f));
                SetBar(fill, 380f, unlocked ? 1f : value / (float)a.Target);
                UIKit.Label(card.transform, unlocked ? Loc.T("Unlocked") : Loc.F("{0} / {1}", value, a.Target), 20f, new Vector2(150f, 28f),
                    new Vector2(135f, -38f), TextAlignmentOptions.Left, UIKit.Muted);
                UIKit.Label(card.transform, Loc.F("+{0} coins", a.Coins), 24f, new Vector2(170f, 40f), new Vector2(245f, 26f),
                    TextAlignmentOptions.Right, unlocked ? UIKit.Muted : UIKit.Gold, true);
            }
            int rows = (list.Length + 1) / 2;
            return -(top - rows * 132f - 20f);
        }

        private float BuildStats()
        {
            int games = Progress.Get(Progress.Stat.GamesPlayed), wins = Progress.Get(Progress.Stat.Wins);
            var rows = new (string label, string value)[]
            {
                ("Games played", games.ToString()),
                ("Wins", wins.ToString()),
                ("Win rate", games > 0 ? Mathf.RoundToInt(100f * wins / games) + "%" : "-"),
                ("Times you were the Shedhead", Progress.Get(Progress.Stat.Shedheads).ToString()),
                ("Current win streak", Progress.Get(Progress.Stat.WinStreak).ToString()),
                ("Best win streak", Progress.Get(Progress.Stat.BestWinStreak).ToString()),
                ("Wins against Hard computers", Progress.Get(Progress.Stat.HardWins).ToString()),
                ("Multiplayer games", Progress.Get(Progress.Stat.MultiplayerGames).ToString()),
                ("Cards played", Progress.Get(Progress.Stat.CardsPlayed).ToString()),
                ("Piles burned", Progress.Get(Progress.Stat.Burns).ToString()),
                ("Times you picked up", Progress.Get(Progress.Stat.PickUps).ToString()),
                ("Biggest pick-up", Progress.Get(Progress.Stat.BiggestPickUp).ToString()),
                ("Players skipped", Progress.Get(Progress.Stat.PlayersSkipped).ToString()),
                ("Daily challenges done", Progress.Get(Progress.Stat.DailiesDone).ToString()),
                ("Best day streak", Progress.Get(Progress.Stat.BestDayStreak).ToString()),
                ("Achievements", Progress.UnlockedCount + " / " + Progress.Achievements.Length)
            };
            float top = -30f;
            for (int i = 0; i < rows.Length; i++)
            {
                int col = i % 2, row = i / 2;
                float x = col == 0 ? -355f : 355f, y = top - 34f - row * 72f;
                var bg = UIKit.Panel(content, new Vector2(690f, 62f), new Vector2(x, y), new Color(0f, 0f, 0f, 0.3f));
                Pin(bg.rectTransform);
                UIKit.Label(bg.transform, rows[i].label, 26f, new Vector2(500f, 50f), new Vector2(-80f, 0f), TextAlignmentOptions.Left, UIKit.Text);
                UIKit.Label(bg.transform, rows[i].value, 30f, new Vector2(160f, 50f), new Vector2(250f, 0f), TextAlignmentOptions.Right, UIKit.Gold, true);
            }
            return -(top - (rows.Length + 1) / 2 * 72f - 20f);
        }

        // ------------------------------------------------------------------ helpers

        private void Text(string text, float size, Color color, Vector2 pos, bool bold = false)
        {
            var t = UIKit.Label(content, text, size, new Vector2(1300f, size * 1.6f), pos, TextAlignmentOptions.Center, color, bold);
            t.textWrappingMode = TextWrappingModes.Normal;
            Pin(t.rectTransform);
        }

        /// <summary>Positions inside the scrolling content are measured down from its top edge.</summary>
        private static void Pin(RectTransform rt)
        {
            Vector2 p = rt.anchoredPosition;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = p;
        }

        /// <summary>A thin progress bar; returns the fill image.</summary>
        private static Image Bar(Transform parent, Vector2 size, Vector2 pos)
        {
            var bg = UIBuild.Image("Bar", parent, UIKit.PanelSprite, new Color(0f, 0f, 0f, 0.55f), size, pos);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 3f;
            var fill = UIBuild.Image("Fill", bg.transform, UIKit.PrimarySprite != null ? UIKit.PrimarySprite : UIKit.PanelSprite,
                UIKit.PrimarySprite != null ? Color.white : UIKit.Gold, size - new Vector2(6f, 6f), Vector2.zero);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 3f;
            var rt = fill.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(3f, 0f);
            return fill;
        }

        private static void SetBar(Image fill, float width, float fraction)
        {
            var rt = fill.rectTransform;
            float inner = width - 6f;
            rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.y, inner * Mathf.Clamp01(fraction)), rt.sizeDelta.y);
            fill.enabled = fraction > 0.001f;
        }
    }
}
