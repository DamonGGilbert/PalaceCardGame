using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Palace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// Splash screen and every menu screen: Main menu, Single Player, Multiplayer,
    /// How to Play, Settings and Credits. Built from code at start-up so there are no
    /// scene references to break. Visual style comes from TableTheme / CardTheme.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private TableTheme tableTheme;
        [SerializeField] private CardTheme cardTheme;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private RectTransform root;   // 1920x1080 reference area, centred on the canvas

        private Image felt;
        private readonly List<CardView> decorBacks = new List<CardView>();
        private GameObject mainPanel, singlePanel, multiPanel, howPanel, settingsPanel, creditsPanel, customisePanel, progressPanel;
        private ProgressMenu progressMenu;
        private TextMeshProUGUI badgeLevel, badgeStreak, dailiesBadgeText;
        private Image badgeFill;
        private GameObject dailiesBadge, popup;

        private void OpenProgress()
        {
            Show(progressPanel);
            progressMenu.Open();
        }

        /// <summary>Top-left badge (level, XP bar, day streak) and the "challenges left" dot on the Progress button.</summary>
        private void RefreshBadges()
        {
            if (this == null || badgeLevel == null) return;
            int level = Progress.LevelFor(Progress.TotalXp, out int into, out int needed);
            badgeLevel.text = Loc.F("Level {0}", level);
            badgeStreak.text = Loc.F("Day streak: {0}", Progress.DayStreak);
            var rt = badgeFill.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(12f, 254f * into / needed), rt.sizeDelta.y);
            int left = Progress.DailiesLeft;
            dailiesBadge.SetActive(left > 0);
            dailiesBadgeText.text = left.ToString();
        }
        private CustomiseMenu customise;
        private TextMeshProUGUI coinsLabel;

        private void OpenCustomise()
        {
            Show(customisePanel);
            customise.Open();
        }

        /// <summary>Something was bought or equipped: update the menu's felt, card backs and coins.</summary>
        private void OnCosmeticsChanged()
        {
            if (this == null) return;
            Cosmetics.ApplyThemes(tableTheme, cardTheme);
            if (felt != null) felt.sprite = tableTheme.CurrentTable;
            foreach (var b in decorBacks) if (b != null) b.Theme = cardTheme;
            if (coinsLabel != null) coinsLabel.text = Loc.F("{0} coins", Cosmetics.Coins);
        }

        private void OnDestroy()
        {
            Cosmetics.Changed -= OnCosmeticsChanged;
            CloudProgress.Changed -= RefreshCloud;
            Progress.Changed -= RefreshBadges;
        }
        private TextMeshProUGUI opponentsLabel, tableSizeLabel;

        public void Configure(TableTheme tTheme, CardTheme cTheme, CardView card, RectTransform menuRoot)
        {
            tableTheme = tTheme;
            cardTheme = cTheme;
            cardPrefab = card;
            root = menuRoot;
        }

        private void Start()
        {
            UIKit.Init(tableTheme);
            int langArg = Array.IndexOf(Environment.GetCommandLineArgs(), "-palace-lang"); // testing only
            if (langArg >= 0 && langArg + 1 < Environment.GetCommandLineArgs().Length && int.TryParse(Environment.GetCommandLineArgs()[langArg + 1], out int lang)) Loc.TestLanguage = lang;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-palace-showcase") >= 0) Cosmetics.ShowcaseForTesting();
            Cosmetics.ApplyThemes(tableTheme, cardTheme); // equipped table / deck / card back
            Cosmetics.Changed += OnCosmeticsChanged;
            GameSettings.ApplyFrameRate();

            felt = UIBuild.Image("Felt", root, tableTheme.CurrentTable, new Color(1f, 1f, 1f, 0.55f), new Vector2(1800f, 900f), new Vector2(0f, 20f));
            UIBuild.StretchImage("Shade", root, null, new Color(0f, 0f, 0f, 0.35f));

            BuildMain();
            BuildSinglePlayer();
            BuildMultiplayer();
            BuildHowToPlay();
            BuildSettings();
            BuildCredits();
            customisePanel = UIBuild.Stretch("CustomiseRoot", root).gameObject;
            customise = gameObject.AddComponent<CustomiseMenu>();
            customise.Build((RectTransform)customisePanel.transform, cardTheme, () => Show(mainPanel));
            customisePanel.SetActive(false);
            progressPanel = UIBuild.Stretch("ProgressRoot", root).gameObject;
            progressMenu = gameObject.AddComponent<ProgressMenu>();
            progressMenu.Build((RectTransform)progressPanel.transform, () => Show(mainPanel));
            progressPanel.SetActive(false);
            Progress.Changed += RefreshBadges;
            RefreshBadges();

            // The first visit each day pays the streak reward (not in automated test runs).
            string[] runArgs = Environment.GetCommandLineArgs();
            bool automated = Array.IndexOf(runArgs, "-palace-autoplay") >= 0 || Array.IndexOf(runArgs, "-palace-net") >= 0;
            int streakCoins = automated ? 0 : Progress.CheckIn();
            if (streakCoins > 0) StartCoroutine(DailyRewardWhenReady(streakCoins));

            if (!GameLaunch.SplashShown) StartCoroutine(Splash());
            else if (!string.IsNullOrEmpty(GameLaunch.MenuMessage)) OpenMultiplayer(GameLaunch.MenuMessage); // e.g. "The host left"
            else if (GameLaunch.ReopenSettingsTab >= 0) Show(settingsPanel); // back after a language / text size change
            else Show(mainPanel);
            GameLaunch.ReopenSettingsTab = -1;
            GameLaunch.MenuMessage = null;
            AudioManager.StartMusic();

            // Testing only: automated runs skip straight past the menu.
            string[] args = Environment.GetCommandLineArgs();
            int netArg = Array.IndexOf(args, "-palace-net");
            if (netArg >= 0 && netArg + 1 < args.Length && !Net.NetSession.Active)
            {
                int players = Array.IndexOf(args, "-palace-players") is int pi && pi >= 0 && pi + 1 < args.Length && int.TryParse(args[pi + 1], out int pc) ? pc : 4;
                OpenMultiplayer();
                StartCoroutine(multi.RunNetTest(args[netArg + 1], players));
            }
            else if (Array.IndexOf(args, "-palace-menushots") >= 0) StartCoroutine(MenuShots());
            else if (Array.IndexOf(args, "-palace-autoplay") >= 0 && netArg < 0) SceneManager.LoadScene(GameLaunch.GameScene);
        }

        private void Show(GameObject panel)
        {
            foreach (var p in new[] { mainPanel, singlePanel, multiPanel, howPanel, settingsPanel, creditsPanel, customisePanel, progressPanel })
                p.SetActive(p == panel);
            if (panel == mainPanel) RefreshBadges();
        }

        // ------------------------------------------------------------------ pop-ups

        /// <summary>A small dialog over the menu with one or two buttons. Each button closes it first.</summary>
        private void Popup(string title, string body, string a, Action onA, string b = null, Action onB = null)
        {
            ClosePopup();
            var layer = UIKit.Layer(root.parent, "Popup", UIKit.Scrim);
            popup = layer.gameObject;
            var panel = UIKit.Panel(layer, new Vector2(900f, 420f), Vector2.zero);
            UIKit.Label(panel.transform, title, 48f, new Vector2(820f, 70f), new Vector2(0f, 140f), TextAlignmentOptions.Center, UIKit.Gold, true);
            var text = UIKit.Label(panel.transform, body, 30f, new Vector2(820f, 160f), new Vector2(0f, 20f), TextAlignmentOptions.Center, UIKit.Text);
            text.textWrappingMode = TextWrappingModes.Normal;
            if (b == null)
                UIKit.Button(panel.transform, a, new Vector2(320f, 80f), new Vector2(0f, -140f), () => { ClosePopup(); onA?.Invoke(); }, true);
            else
            {
                UIKit.Button(panel.transform, a, new Vector2(360f, 80f), new Vector2(-195f, -140f), () => { ClosePopup(); onA?.Invoke(); }, true);
                UIKit.Button(panel.transform, b, new Vector2(360f, 80f), new Vector2(195f, -140f), () => { ClosePopup(); onB?.Invoke(); });
            }
        }

        private void ClosePopup()
        {
            if (popup != null) Destroy(popup);
            popup = null;
        }

        private IEnumerator DailyRewardWhenReady(int coins)
        {
            while (!mainPanel.activeSelf) yield return null; // after the splash
            yield return new WaitForSecondsRealtime(0.3f);
            int day = Progress.DayStreak;
            string body = (day <= 1 ? Loc.T("Welcome back!") : Loc.F("Day {0} in a row!", day)) + "\n" + Loc.F("+{0} coins", coins) + "\n\n"
                          + Loc.F("Come back tomorrow for {0} coins.", Progress.StreakCoins(day + 1));
            Popup(Loc.T("Daily reward"), body, "Collect", null);
            AudioManager.Play(Sfx.Win, 0.6f);
        }

        /// <summary>Single Player: the first time, offer the guided game.</summary>
        private void OpenSinglePlayer()
        {
            if (GameSettings.TutorialDone || GameSettings.TutorialOffered)
            {
                Show(singlePanel);
                return;
            }
            GameSettings.TutorialOffered = true;
            GameSettings.Save();
            Popup(Loc.T("New to Palace?"), Loc.T("Play a short guided game first. It explains every special card the moment it comes up."),
                "Teach me", StartTutorial, "I know how to play", () => Show(singlePanel));
        }

        private void StartTutorial()
        {
            GameSettings.Save();
            GameLaunch.PlayerName = GameSettings.PlayerName;
            GameLaunch.SpectateOnly = false;
            GameLaunch.Seed = 0;
            GameLaunch.BotCount = GameSettings.BotCount;   // used by "Play Again" afterwards
            GameLaunch.BotLevel = GameSettings.BotLevel;
            GameLaunch.Tutorial = true;
            SceneManager.LoadScene(GameLaunch.GameScene);
        }

        // ------------------------------------------------------------------ splash

        private IEnumerator Splash()
        {
            GameLaunch.SplashShown = true;
            Show(null);

            var layer = UIKit.Layer(root.parent, "Splash", new Color(0.02f, 0.03f, 0.03f, 1f));
            var group = layer.gameObject.AddComponent<CanvasGroup>();
            bool skip = false;
            var skipButton = layer.gameObject.AddComponent<Button>();
            skipButton.transition = Selectable.Transition.None;
            skipButton.onClick.AddListener(() => skip = true);

            var title = UIBuild.Text("Title", layer, "PALACE", 170f, UIKit.Gold, new Vector2(1200f, 220f), new Vector2(0f, 60f),
                TextAlignmentOptions.Center, UIKit.Font, true);
            title.characterSpacing = 18f;
            var sub = UIBuild.Text("Subtitle", layer, "THE CLASSIC SHEDHEAD CARD GAME", 34f, UIKit.Muted, new Vector2(1200f, 60f),
                new Vector2(0f, -70f), TextAlignmentOptions.Center, UIKit.Font, true);
            sub.characterSpacing = 8f;
            sub.alpha = 0f;

            var fan = new[] { new Card(0, Rank.Ten, Suit.Hearts), new Card(1, Rank.Ace, Suit.Spades), new Card(2, Rank.Two, Suit.Diamonds) };
            var cards = new List<RectTransform>();
            for (int i = 0; i < fan.Length; i++)
            {
                CardView v = CardFactory.Spawn(cardPrefab, cardTheme, layer);
                v.SetInteractable(false);
                v.ShowCard(fan[i], true);
                cards.Add((RectTransform)v.transform);
                v.transform.localScale = Vector3.zero;
            }

            var titleRt = title.rectTransform;
            titleRt.localScale = Vector3.one * 0.6f;
            title.alpha = 0f;
            yield return Tween.Run(0.6f, t => { titleRt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, t); title.alpha = t; });

            for (int i = 0; i < cards.Count && !skip; i++)
            {
                AudioManager.Play(Sfx.CardPlay, 0.7f);
                var rt = cards[i];
                float angle = (1 - i) * 14f;
                yield return Tween.Move(rt, new Vector2(0f, -600f), new Vector2((i - 1) * 90f, -250f + Mathf.Abs(i - 1) * 14f),
                    0.28f, 0.8f, 0.8f, 0f, angle);
            }

            yield return Tween.Run(0.4f, t => sub.alpha = t);
            float wait = 0f;
            while (wait < 1.2f && !skip)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            Show(mainPanel);
            layer.SetAsLastSibling();
            yield return Tween.Fade(group, 1f, 0f, 0.5f);
            Destroy(layer.gameObject);
        }

        // ------------------------------------------------------------------ main menu

        private void BuildMain()
        {
            mainPanel = UIBuild.Stretch("Main", root).gameObject;
            Transform p = mainPanel.transform;

            var title = UIKit.Label(p, "PALACE", 150f, new Vector2(1200f, 190f), new Vector2(0f, 330f), TextAlignmentOptions.Center, UIKit.Gold, true);
            title.characterSpacing = 16f;
            title.outlineWidth = 0.12f;
            title.outlineColor = new Color32(0, 0, 0, 180);
            var sub = UIKit.Label(p, "THE CLASSIC SHEDHEAD CARD GAME", 30f, new Vector2(1200f, 50f), new Vector2(0f, 222f), TextAlignmentOptions.Center, UIKit.Muted, true);
            sub.characterSpacing = 6f;

            BuildFan(p, new Vector2(-600f, -60f), new[] { new Card(0, Rank.Queen, Suit.Hearts), new Card(1, Rank.King, Suit.Clubs) }, -1f);
            BuildFan(p, new Vector2(600f, -60f), new[] { new Card(2, Rank.Seven, Suit.Spades), new Card(3, Rank.Eight, Suit.Diamonds) }, 1f);

            var size = new Vector2(440f, 68f);
            const float top = 160f, step = 77f;
            UIKit.Button(p, "Single Player", size, new Vector2(0f, top), OpenSinglePlayer, true, 32f);
            UIKit.Button(p, "Multiplayer", size, new Vector2(0f, top - step), () => OpenMultiplayer());
            var progressButton = UIKit.Button(p, "Progress", size, new Vector2(0f, top - step * 2), OpenProgress);
            UIKit.Button(p, "Customise", size, new Vector2(0f, top - step * 3), OpenCustomise);
            UIKit.Button(p, "How to Play", size, new Vector2(0f, top - step * 4), () => Show(howPanel));
            UIKit.Button(p, "Settings", size, new Vector2(0f, top - step * 5), () => Show(settingsPanel));
            UIKit.Button(p, "Credits", size, new Vector2(0f, top - step * 6), () => Show(creditsPanel));

            // Red dot with the number of daily challenges still to do.
            var dot = UIBuild.Image("DailiesLeft", progressButton.transform, UIKit.CircleSprite, new Color(0.80f, 0.16f, 0.12f, 1f),
                new Vector2(46f, 46f), new Vector2(200f, 22f));
            dailiesBadge = dot.gameObject;
            dailiesBadgeText = UIBuild.Text("Count", dot.transform, "", 26f, Color.white, new Vector2(46f, 46f), Vector2.zero,
                TextAlignmentOptions.Center, UIKit.Font, true, false);

            // Level badge, top left: tap it to open Progress.
            var badge = UIKit.Panel(p, new Vector2(340f, 108f), Vector2.zero);
            var brt = badge.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(200f, -66f);
            badge.gameObject.AddComponent<Button>().onClick.AddListener(OpenProgress);
            badgeLevel = UIKit.Label(badge.transform, "", 32f, new Vector2(300f, 44f), new Vector2(0f, 26f), TextAlignmentOptions.Left, UIKit.Gold, true);
            var barBg = UIBuild.Image("XpBar", badge.transform, UIKit.PanelSprite, new Color(0f, 0f, 0f, 0.55f), new Vector2(260f, 18f), new Vector2(-20f, -8f));
            barBg.type = Image.Type.Sliced;
            barBg.pixelsPerUnitMultiplier = 3f;
            badgeFill = UIBuild.Image("Fill", barBg.transform, UIKit.PrimarySprite != null ? UIKit.PrimarySprite : UIKit.PanelSprite,
                UIKit.PrimarySprite != null ? Color.white : UIKit.Gold, new Vector2(254f, 12f), Vector2.zero);
            badgeFill.type = Image.Type.Sliced;
            badgeFill.pixelsPerUnitMultiplier = 3f;
            var frt = badgeFill.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 0.5f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = new Vector2(3f, 0f);
            badgeStreak = UIKit.Label(badge.transform, "", 22f, new Vector2(300f, 32f), new Vector2(0f, -36f), TextAlignmentOptions.Left, UIKit.Muted);

            coinsLabel = UIKit.Label(p, "", 32f, new Vector2(400f, 50f), new Vector2(0f, 0f), TextAlignmentOptions.Right, UIKit.Gold, true);
            var crt = coinsLabel.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-230f, -50f);
            coinsLabel.text = Loc.F("{0} coins", Cosmetics.Coins);

            bool canQuit = Application.platform != RuntimePlatform.IPhonePlayer && Application.platform != RuntimePlatform.WebGLPlayer;
            if (canQuit) UIKit.Button(p, "Exit", size, new Vector2(0f, top - step * 7), Quit);
        }

        /// <summary>Decorative fan: a card back (shows the Settings choice) plus two face cards.</summary>
        private void BuildFan(Transform parent, Vector2 at, Card[] faces, float side)
        {
            CardView back = CardFactory.Spawn(cardPrefab, cardTheme, parent);
            back.SetInteractable(false);
            back.ShowBack();
            Place(back, at + new Vector2(-side * 70f, 10f), -side * 16f);
            decorBacks.Add(back);

            for (int i = 0; i < faces.Length; i++)
            {
                CardView v = CardFactory.Spawn(cardPrefab, cardTheme, parent);
                v.SetInteractable(false);
                v.ShowCard(faces[i], true);
                Place(v, at + new Vector2(side * i * 70f, -i * 8f), side * i * 14f);
            }
        }

        private static void Place(CardView v, Vector2 pos, float angle)
        {
            var rt = (RectTransform)v.transform;
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one * 1.1f;
            rt.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        private static void Quit()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ single player

        private void BuildSinglePlayer()
        {
            Transform p = BuildScreen("Single Player", new Vector2(900f, 640f), out singlePanel);

            UIKit.Label(p, "Your name", 28f, new Vector2(300f, 50f), new Vector2(-250f, 172f), TextAlignmentOptions.Left, UIKit.Muted, true);
            UIKit.Input(p, new Vector2(420f, 64f), new Vector2(160f, 172f), GameSettings.PlayerName, "Enter your name", 14,
                v => GameSettings.PlayerName = v);

            opponentsLabel = UIKit.Label(p, "", 30f, new Vector2(700f, 50f), new Vector2(0f, 98f), TextAlignmentOptions.Center, UIKit.Text, true);
            UIKit.Slider(p, new Vector2(640f, 44f), new Vector2(0f, 48f), 1, 9, GameSettings.BotCount, true, v =>
            {
                GameSettings.BotCount = Mathf.RoundToInt(v);
                RefreshOpponents();
            });
            tableSizeLabel = UIKit.Label(p, "", 24f, new Vector2(700f, 40f), new Vector2(0f, 4f), TextAlignmentOptions.Center, UIKit.Muted);
            RefreshOpponents();

            // How well the computers play (same rules at every level).
            UIKit.Label(p, "Computer skill", 28f, new Vector2(260f, 50f), new Vector2(-280f, -70f), TextAlignmentOptions.Left, UIKit.Muted, true);
            UIKit.Segmented(p, GameSettings.BotLevelNames, GameSettings.BotLevel, new Vector2(500f, 60f), new Vector2(130f, -70f), i =>
            {
                GameSettings.BotLevel = i;
                RefreshSkillNote();
            });
            skillNote = UIKit.Label(p, "", 22f, new Vector2(780f, 36f), new Vector2(0f, -118f), TextAlignmentOptions.Center, UIKit.Muted);
            RefreshSkillNote();

            UIKit.Button(p, "Start Game", new Vector2(340f, 80f), new Vector2(-185f, -180f), StartSinglePlayer, true, 34f);
            UIKit.Button(p, "Guided game", new Vector2(300f, 80f), new Vector2(180f, -180f), StartTutorial, false, 28f);
        }

        private TextMeshProUGUI skillNote;

        private void RefreshSkillNote()
        {
            int level = GameSettings.BotLevel;
            skillNote.text = Loc.T(level == 0 ? "Easy: relaxed computers that often waste their best cards."
                : level == 2 ? "Hard: computers that save their best cards and target whoever is winning."
                : "Normal: computers that play sensibly.");
        }

        private void RefreshOpponents()
        {
            int bots = GameSettings.BotCount;
            int players = bots + 1;
            int decks = new HouseRules().DecksFor(players);
            opponentsLabel.text = Loc.F("Computer opponents: {0}", bots);
            tableSizeLabel.text = decks == 1 ? Loc.F("{0} players at the table, 1 deck", players) : Loc.F("{0} players at the table, {1} decks", players, decks);
        }

        private void StartSinglePlayer()
        {
            GameSettings.Save();
            GameLaunch.PlayerName = GameSettings.PlayerName;
            GameLaunch.BotCount = GameSettings.BotCount;
            GameLaunch.BotLevel = GameSettings.BotLevel;
            GameLaunch.Tutorial = false;
            GameLaunch.SpectateOnly = false;
            GameLaunch.Seed = 0;
            SceneManager.LoadScene(GameLaunch.GameScene);
        }

        // ------------------------------------------------------------------ multiplayer

        private MultiplayerMenu multi;

        private void BuildMultiplayer()
        {
            multiPanel = UIBuild.Stretch("Multiplayer", root).gameObject;
            multi = gameObject.AddComponent<MultiplayerMenu>();
            multi.Build((RectTransform)multiPanel.transform, () => Show(mainPanel));
            multiPanel.SetActive(false);
        }

        private void OpenMultiplayer(string message = null)
        {
            Show(multiPanel);
            multi.Open(message);
        }

        // ------------------------------------------------------------------ how to play

        private void BuildHowToPlay()
        {
            Transform p = BuildScreen("How to Play", new Vector2(1200f, 900f), out howPanel, -390f);
            UIKit.ScrollText(p, new Vector2(1100f, 640f), new Vector2(0f, 10f), RulesText, 26f);
            ((RectTransform)lastBackButton.transform).anchoredPosition = new Vector2(-170f, -390f);
            UIKit.Button(p, "Play the guided game", new Vector2(380f, 70f), new Vector2(170f, -390f), StartTutorial, true, 28f);
        }

        public const string RulesText =
            "<b><color=#F2C24D>Goal</color></b>\nGet rid of all your cards. The game continues until everyone has finished, so every place from 1st to last counts.\n\n" +
            "<b><color=#F2C24D>Setup</color></b>\nEveryone gets 3 face-down cards, 3 face-up cards on top of them, and 3 cards in hand. The rest form the deck. With 6 or more players two decks are used.\n\n" +
            "<b><color=#F2C24D>Your turn</color></b>\nPlay a card equal to or higher than the top card of the pile (2 low, Ace high). You may play several cards of the same rank together, but you don't have to. While the deck lasts, draw back up to 3 cards. If you can't play, you pick up the whole pile.\n\n" +
            "<b><color=#F2C24D>Special cards</color></b>\n" +
            "<b>2</b>  Reset. Can go on anything; the next player can play any card.\n" +
            "<b>7</b>  Call HIGHER or LOWER. The next player must play higher or lower than 7.\n" +
            "<b>8</b>  Skip. Each 8 skips one player (two 8s skip two players).\n" +
            "<b>10</b>  Burn. Removes the pile from the game and you go again.\n" +
            "<b>Queen</b>  Reverses the direction of play.\n" +
            "<b>Ace</b>  Choose what it counts as: any value that matches or beats the pile (on a Jack it can be J, Q, K or A, never 9). On a LOWER 7 it can be a 2-6. Or use it as a 10 to burn the pile.\n\n" +
            "<b><color=#F2C24D>The three stages</color></b>\n1. Play from your hand until the deck runs out and your hand is empty.\n" +
            "2. Then play your face-up cards.\n3. Finally your face-down cards, turned over one at a time without looking. If the card can't be played, you pick up the pile with it and must clear your hand again.\n\n" +
            "<b><color=#F2C24D>Controls</color></b>\nDrag a card onto the pile to play it. Or tap cards to select them (tap again to deselect) and press PLAY. To play several of the same rank, tap them all, then drag one. Cards you can't play are darkened; tap one to see why. The arrows around the table show the direction of play. Tap the pile to see every card in it.\n\n" +
            "<b><color=#F2C24D>Keyboard (PC)</color></b>\n1-9 pick a card, Enter or Space plays, P picks up the pile, H or L calls higher or lower, J Q K A or 2-9 choose an Ace's value (B burns), S sorts your hand, V shows the pile, F speeds up once you've finished, Esc opens the menu.";

        // ------------------------------------------------------------------ settings

        private void BuildSettings()
        {
            Transform p = BuildScreen("Settings", new Vector2(1100f, 980f), out settingsPanel, -420f);
            const float step = 84f;
            const float labelX = -330f, controlX = 150f;
            var controlSize = new Vector2(600f, 60f);
            var sliderSize = new Vector2(600f, 44f);

            // Three tabs so nothing is cramped on a phone.
            var tabs = new RectTransform[3];
            for (int t = 0; t < tabs.Length; t++) tabs[t] = UIBuild.Stretch("Tab" + t, p);
            settingsTabs = tabs;
            int firstTab = Mathf.Max(0, GameLaunch.ReopenSettingsTab);
            UIKit.Segmented(p, new[] { "Sound & speed", "Display", "Cloud save" }, firstTab, new Vector2(900f, 62f), new Vector2(0f, 330f), ShowSettingsTab);

            // ---- Sound & speed
            Transform a = tabs[0];
            float y = 230f;
            Row(a, "Music", y);
            UIKit.Slider(a, sliderSize, new Vector2(controlX, y), 0f, 1f, GameSettings.MusicVolume, false, v => GameSettings.MusicVolume = v);
            y -= step;
            Row(a, "Music track", y);
            UIKit.Segmented(a, GameSettings.MusicTrackNames, GameSettings.MusicTrack, controlSize, new Vector2(controlX, y), i => GameSettings.MusicTrack = i);
            y -= step;
            Row(a, "Sound effects", y);
            UIKit.Slider(a, sliderSize, new Vector2(controlX, y), 0f, 1f, GameSettings.SfxVolume, false, v => GameSettings.SfxVolume = v);
            y -= step;
            Row(a, "Vibration", y);
            UIKit.Segmented(a, GameSettings.OnOffNames, GameSettings.Vibration ? 1 : 0, controlSize, new Vector2(controlX, y), i =>
            {
                GameSettings.Vibration = i == 1;
                Haptics.Buzz();
            });
            y -= step;
            Row(a, "Animations", y);
            UIKit.Segmented(a, GameSettings.AnimationNames, (int)GameSettings.Animation, controlSize, new Vector2(controlX, y),
                i => GameSettings.Animation = (GameSettings.AnimationSpeed)i);
            y -= step;
            Row(a, "Computer speed", y);
            UIKit.Segmented(a, GameSettings.BotSpeedNames, GameSettings.BotSpeed, controlSize, new Vector2(controlX, y), i => GameSettings.BotSpeed = i);

            // ---- Display
            Transform d = tabs[1];
            y = 230f;
            Row(d, "Language", y);
            UIKit.Segmented(d, GameSettings.LanguageNames, GameSettings.Language, controlSize, new Vector2(controlX, y), i =>
            {
                if (i == GameSettings.Language) return;
                GameSettings.Language = i;
                ReloadMenu(1);
            });
            y -= step;
            Row(d, "Text size", y);
            UIKit.Segmented(d, GameSettings.TextSizeNames, GameSettings.LargeText ? 1 : 0, controlSize, new Vector2(controlX, y), i =>
            {
                if ((i == 1) == GameSettings.LargeText) return;
                GameSettings.LargeText = i == 1;
                ReloadMenu(1);
            });
            y -= step;
            Row(d, "Card colours", y);
            UIKit.Segmented(d, GameSettings.CardColourNames, GameSettings.FourColourDeck ? 1 : 0, controlSize, new Vector2(controlX, y), i =>
            {
                GameSettings.FourColourDeck = i == 1;
                OnCosmeticsChanged();
            });
            y -= step;
            Row(d, "Hand order", y);
            UIKit.Segmented(d, GameSettings.HandOrderNames, GameSettings.HandOrder, controlSize, new Vector2(controlX, y), i => GameSettings.HandOrder = i);
            y -= step;
            Row(d, "Frame rate", y);
            UIKit.Segmented(d, GameSettings.FrameRateNames, GameSettings.FrameRateIndex, controlSize, new Vector2(controlX, y), i => GameSettings.FrameRateIndex = i);
            y -= step;
            var note = UIKit.Label(d, "4 colours: diamonds are blue and clubs are green, so every suit stands out.", 22f, new Vector2(960f, 60f),
                new Vector2(0f, y + 4f), TextAlignmentOptions.Center, UIKit.Muted);
            note.textWrappingMode = TextWrappingModes.Normal;
            UIKit.Label(d, "Table, cards and card backs are on the Customise screen.", 22f, new Vector2(960f, 40f),
                new Vector2(0f, y - 46f), TextAlignmentOptions.Center, UIKit.Muted);

            // ---- Cloud save
            Transform c = tabs[2];
            var intro = UIKit.Label(c, "Back up your coins, unlocks and purchases. Sign in with a free Unity account to take them to another phone or PC.",
                26f, new Vector2(900f, 110f), new Vector2(0f, 210f), TextAlignmentOptions.Center, UIKit.Text);
            intro.textWrappingMode = TextWrappingModes.Normal;
            cloudAccount = UIKit.Label(c, "", 26f, new Vector2(900f, 80f), new Vector2(0f, 90f), TextAlignmentOptions.Center, UIKit.Gold, true);
            cloudAccount.textWrappingMode = TextWrappingModes.Normal;
            cloudStatus = UIKit.Label(c, "", 24f, new Vector2(900f, 80f), new Vector2(0f, 10f), TextAlignmentOptions.Center, UIKit.Muted);
            cloudStatus.textWrappingMode = TextWrappingModes.Normal;
            cloudSignIn = UIKit.Button(c, "Sign in (Unity account)", new Vector2(420f, 76f), new Vector2(-225f, -110f), CloudProgress.SignInWithAccount, true, 28f);
            cloudSync = UIKit.Button(c, "Sync now", new Vector2(360f, 76f), new Vector2(225f, -110f), CloudProgress.SyncNow, false, 28f);
            CloudProgress.Changed += RefreshCloud;
            RefreshCloud();

            UIKit.Label(p, "Changes are saved automatically.", 22f, new Vector2(900f, 40f), new Vector2(0f, -345f), TextAlignmentOptions.Center, UIKit.Muted);
            ShowSettingsTab(firstTab);

            void Row(Transform parent, string label, float rowY)
            {
                UIKit.Label(parent, label, 28f, new Vector2(320f, 50f), new Vector2(labelX, rowY), TextAlignmentOptions.Left, UIKit.Text, true);
            }
        }

        private RectTransform[] settingsTabs;
        private TextMeshProUGUI cloudAccount, cloudStatus;
        private Button cloudSignIn, cloudSync;

        private void ShowSettingsTab(int index)
        {
            for (int t = 0; t < settingsTabs.Length; t++) settingsTabs[t].gameObject.SetActive(t == index);
            if (index == 2) RefreshCloud();
        }

        private void RefreshCloud()
        {
            if (this == null || cloudStatus == null) return;
            bool supported = CloudProgress.Supported;
            cloudAccount.text = Loc.T(!supported ? "Cloud save isn't included in this version yet."
                : CloudProgress.AccountSignedIn ? "Signed in with your Unity account."
                : "Not signed in: progress is backed up, but only this device can restore it.");
            cloudStatus.text = Loc.T(CloudProgress.Status);
            cloudSignIn.gameObject.SetActive(supported && !CloudProgress.AccountSignedIn);
            cloudSync.gameObject.SetActive(supported);
            cloudSignIn.interactable = cloudSync.interactable = !CloudProgress.Busy;
        }

        /// <summary>Language and text size are applied as screens are built, so rebuild the menu.</summary>
        private void ReloadMenu(int settingsTab)
        {
            GameSettings.Save();
            GameLaunch.ReopenSettingsTab = settingsTab;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ------------------------------------------------------------------ credits

        private void BuildCredits()
        {
            Transform p = BuildScreen("Credits", new Vector2(1000f, 680f), out creditsPanel);
            var text = UIKit.Label(p,
                "<color=#F2C24D><b>PALACE</b></color>\n\n" +
                Loc.T("Game design and house rules") + "\n<b>Damon Gilbert</b>\n\n" +
                Loc.T("Programming, art and sound") + "\n<b>" + Loc.T("Built with Claude (Anthropic)") + "</b>\n\n" +
                Loc.T("Made with Unity") + "\n" + Loc.T("Fonts: Alegreya and Liberation Sans (SIL Open Font License)") + "\n" +
                Loc.T("All card art, table art and sounds are generated in code for this game.") + "\n" +
                Loc.T("Translations: English, Polski, Español"),
                28f, new Vector2(900f, 440f), new Vector2(0f, 20f), TextAlignmentOptions.Center, UIKit.Text);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true;
        }

        private Button lastBackButton;

        /// <summary>Creates a titled panel with a Back button and returns the panel's transform.</summary>
        private Transform BuildScreen(string title, Vector2 size, out GameObject screen, float backY = 0f)
        {
            screen = UIBuild.Stretch(title, root).gameObject;
            var panel = UIKit.Panel(screen.transform, size, Vector2.zero);
            UIKit.Label(panel.transform, title, 54f, new Vector2(size.x - 60f, 80f), new Vector2(0f, size.y * 0.5f - 60f),
                TextAlignmentOptions.Center, UIKit.Gold, true);
            float y = backY != 0f ? backY : -size.y * 0.5f + 60f;
            lastBackButton = UIKit.Button(panel.transform, "Back", new Vector2(220f, 70f), new Vector2(0f, y), () => Show(mainPanel));
            screen.SetActive(false);
            return panel.transform;
        }

        // ------------------------------------------------------------------ testing only

        private IEnumerator MenuShots()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-palace-menushots");
            string dir = i + 1 < args.Length ? args[i + 1] : "menushots";
            Directory.CreateDirectory(dir);

            yield return new WaitForSecondsRealtime(1.2f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "splash.png"));
            while (!mainPanel.activeSelf) yield return null;
            yield return new WaitForSecondsRealtime(0.8f);
            if (popup != null) // the daily streak reward
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "menu_daily_reward.png"));
                yield return new WaitForSecondsRealtime(0.3f);
                ClosePopup();
            }

            var shots = new (GameObject panel, string name)[]
            {
                (mainPanel, "menu_main"), (customisePanel, "menu_customise"), (singlePanel, "menu_single"), (multiPanel, "menu_multiplayer"),
                (howPanel, "menu_howto"), (settingsPanel, "menu_settings"), (creditsPanel, "menu_credits")
            };
            foreach (var shot in shots)
            {
                Show(shot.panel);
                if (shot.panel == multiPanel) multi.Open();
                if (shot.panel == customisePanel) customise.Open();
                yield return new WaitForSecondsRealtime(0.4f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, shot.name + ".png"));
                yield return new WaitForSecondsRealtime(0.3f);
            }

            Show(customisePanel);
            customise.OpenShop();
            yield return new WaitForSecondsRealtime(0.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "menu_shop.png"));
            yield return new WaitForSecondsRealtime(0.3f);

            // The new screens: the other Settings tabs, and the scrolling flag items.
            Show(settingsPanel);
            for (int tab = 1; tab < 3; tab++)
            {
                ShowSettingsTab(tab);
                yield return new WaitForSecondsRealtime(0.4f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "menu_settings_" + tab + ".png"));
                yield return new WaitForSecondsRealtime(0.3f);
            }
            Show(customisePanel);
            foreach (var (cat, scrollTo, name) in new[] { (CosmeticCategory.Table, 0.5f, "tables"), (CosmeticCategory.Table, 0f, "tables_end"),
                         (CosmeticCategory.CardBack, 0f, "backs"), (CosmeticCategory.Banner, 0f, "banners") })
            {
                customise.TestShow(cat, scrollTo);
                yield return new WaitForSecondsRealtime(0.5f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "menu_customise_" + name + ".png"));
                yield return new WaitForSecondsRealtime(0.3f);
            }

            // Progress: daily challenges, achievements, stats; and the first-time guided game offer.
            Show(progressPanel);
            for (int tab = 0; tab < 3; tab++)
            {
                progressMenu.TestShow(tab);
                yield return new WaitForSecondsRealtime(0.4f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "menu_progress_" + tab + ".png"));
                yield return new WaitForSecondsRealtime(0.3f);
            }
            Show(mainPanel);
            Popup(Loc.T("New to Palace?"), Loc.T("Play a short guided game first. It explains every special card the moment it comes up."),
                "Teach me", null, "I know how to play", null);
            yield return new WaitForSecondsRealtime(0.4f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "menu_tutorial_offer.png"));
            yield return new WaitForSecondsRealtime(0.3f);
            ClosePopup();

            if (Array.IndexOf(args, "-palace-autoplay") >= 0) SceneManager.LoadScene(GameLaunch.GameScene);
            else Application.Quit();
        }
    }
}
