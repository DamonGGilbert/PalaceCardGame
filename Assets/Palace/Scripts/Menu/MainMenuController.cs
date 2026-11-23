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
        private GameObject mainPanel, singlePanel, multiPanel, howPanel, settingsPanel, creditsPanel;
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
            GameSettings.ApplyFrameRate();

            felt = UIBuild.Image("Felt", root, tableTheme.CurrentTable, new Color(1f, 1f, 1f, 0.55f), new Vector2(1800f, 900f), new Vector2(0f, 20f));
            UIBuild.StretchImage("Shade", root, null, new Color(0f, 0f, 0f, 0.35f));

            BuildMain();
            BuildSinglePlayer();
            BuildMultiplayer();
            BuildHowToPlay();
            BuildSettings();
            BuildCredits();

            if (!GameLaunch.SplashShown) StartCoroutine(Splash());
            else Show(mainPanel);
            AudioManager.StartMusic();

            // Testing only: automated runs skip straight past the menu.
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-palace-menushots") >= 0) StartCoroutine(MenuShots());
            else if (Array.IndexOf(args, "-palace-autoplay") >= 0) SceneManager.LoadScene(GameLaunch.GameScene);
        }

        private void Show(GameObject panel)
        {
            foreach (var p in new[] { mainPanel, singlePanel, multiPanel, howPanel, settingsPanel, creditsPanel })
                p.SetActive(p == panel);
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
            var sub = UIBuild.Text("Subtitle", layer, "THE CLASSIC SHITHEAD CARD GAME", 34f, UIKit.Muted, new Vector2(1200f, 60f),
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
            var sub = UIKit.Label(p, "THE CLASSIC SHITHEAD CARD GAME", 30f, new Vector2(1200f, 50f), new Vector2(0f, 222f), TextAlignmentOptions.Center, UIKit.Muted, true);
            sub.characterSpacing = 6f;

            BuildFan(p, new Vector2(-600f, -60f), new[] { new Card(0, Rank.Queen, Suit.Hearts), new Card(1, Rank.King, Suit.Clubs) }, -1f);
            BuildFan(p, new Vector2(600f, -60f), new[] { new Card(2, Rank.Seven, Suit.Spades), new Card(3, Rank.Eight, Suit.Diamonds) }, 1f);

            var size = new Vector2(440f, 78f);
            UIKit.Button(p, "Single Player", size, new Vector2(0f, 110f), () => Show(singlePanel), true, 32f);
            UIKit.Button(p, "Multiplayer", size, new Vector2(0f, 18f), () => Show(multiPanel));
            UIKit.Button(p, "How to Play", size, new Vector2(0f, -74f), () => Show(howPanel));
            UIKit.Button(p, "Settings", size, new Vector2(0f, -166f), () => Show(settingsPanel));
            UIKit.Button(p, "Credits", size, new Vector2(0f, -258f), () => Show(creditsPanel));

            bool canQuit = Application.platform != RuntimePlatform.IPhonePlayer && Application.platform != RuntimePlatform.WebGLPlayer;
            if (canQuit) UIKit.Button(p, "Exit", size, new Vector2(0f, -350f), Quit);
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

            UIKit.Label(p, "Your name", 28f, new Vector2(300f, 50f), new Vector2(-250f, 150f), TextAlignmentOptions.Left, UIKit.Muted, true);
            UIKit.Input(p, new Vector2(420f, 64f), new Vector2(160f, 150f), GameSettings.PlayerName, "Enter your name", 14,
                v => GameSettings.PlayerName = v);

            opponentsLabel = UIKit.Label(p, "", 30f, new Vector2(700f, 50f), new Vector2(0f, 55f), TextAlignmentOptions.Center, UIKit.Text, true);
            UIKit.Slider(p, new Vector2(640f, 44f), new Vector2(0f, -5f), 1, 9, GameSettings.BotCount, true, v =>
            {
                GameSettings.BotCount = Mathf.RoundToInt(v);
                RefreshOpponents();
            });
            tableSizeLabel = UIKit.Label(p, "", 24f, new Vector2(700f, 40f), new Vector2(0f, -65f), TextAlignmentOptions.Center, UIKit.Muted);
            RefreshOpponents();

            UIKit.Button(p, "Start Game", new Vector2(340f, 84f), new Vector2(0f, -170f), StartSinglePlayer, true, 34f);
        }

        private void RefreshOpponents()
        {
            int bots = GameSettings.BotCount;
            int players = bots + 1;
            int decks = new HouseRules().DecksFor(players);
            opponentsLabel.text = "Computer opponents: " + bots;
            tableSizeLabel.text = players + " players at the table, " + decks + (decks == 1 ? " deck" : " decks");
        }

        private void StartSinglePlayer()
        {
            GameSettings.Save();
            GameLaunch.PlayerName = GameSettings.PlayerName;
            GameLaunch.BotCount = GameSettings.BotCount;
            GameLaunch.SpectateOnly = false;
            GameLaunch.Seed = 0;
            SceneManager.LoadScene(GameLaunch.GameScene);
        }

        // ------------------------------------------------------------------ multiplayer

        private void BuildMultiplayer()
        {
            Transform p = BuildScreen("Multiplayer", new Vector2(1000f, 680f), out multiPanel);

            var local = UIKit.Button(p, "Local network\n<size=22>Same Wi-Fi, no internet needed</size>", new Vector2(420f, 150f), new Vector2(-220f, 110f), null);
            var online = UIKit.Button(p, "Online\n<size=22>Friends anywhere, using a join code</size>", new Vector2(420f, 150f), new Vector2(220f, 110f), null);
            local.interactable = false;
            online.interactable = false;

            UIKit.Label(p, "Coming in the next update", 34f, new Vector2(900f, 50f), new Vector2(0f, -15f), TextAlignmentOptions.Center, UIKit.Gold, true);
            var note = UIKit.Label(p,
                "Multiplayer is the next development stage. The rules engine is already built so that " +
                "one device can host the game and send every move to the others. Online play will use " +
                "Unity Gaming Services (Lobby + Relay), which has a free tier. We'll confirm costs before setting it up.",
                24f, new Vector2(860f, 160f), new Vector2(0f, -120f), TextAlignmentOptions.Center, UIKit.Muted);
            note.textWrappingMode = TextWrappingModes.Normal;
        }

        // ------------------------------------------------------------------ how to play

        private void BuildHowToPlay()
        {
            Transform p = BuildScreen("How to Play", new Vector2(1200f, 900f), out howPanel, -390f);
            UIKit.ScrollText(p, new Vector2(1100f, 640f), new Vector2(0f, 10f), RulesText, 26f);
        }

        private const string RulesText =
            "<b><color=#F2C24D>Goal</color></b>\nGet rid of all your cards. The game continues until everyone has finished, so every place from 1st to last counts.\n\n" +
            "<b><color=#F2C24D>Setup</color></b>\nEveryone gets 3 face-down cards, 3 face-up cards on top of them, and 3 cards in hand. The rest form the deck. With 6 or more players two decks are used.\n\n" +
            "<b><color=#F2C24D>Your turn</color></b>\nPlay a card equal to or higher than the top card of the pile (2 low, Ace high). You may play several cards of the same rank together, but you don't have to. While the deck lasts, draw back up to 3 cards. If you can't play, you pick up the whole pile.\n\n" +
            "<b><color=#F2C24D>Special cards</color></b>\n" +
            "<b>2</b>  Reset. Can go on anything; the next player can play any card.\n" +
            "<b>7</b>  Call HIGHER or LOWER. The next player must play higher or lower than 7.\n" +
            "<b>8</b>  Skip. Each 8 skips one player (two 8s skip two players).\n" +
            "<b>10</b>  Burn. Removes the pile from the game and you go again.\n" +
            "<b>Queen</b>  Reverses the direction of play.\n" +
            "<b>Ace</b>  Choose what it counts as: any value that matches or beats the pile (on a Jack it can be J, Q, K or A, never 9). Or use it as a 10 to burn the pile.\n\n" +
            "<b><color=#F2C24D>The three stages</color></b>\n1. Play from your hand until the deck runs out and your hand is empty.\n" +
            "2. Then play your face-up cards.\n3. Finally your face-down cards, turned over one at a time without looking. If the card can't be played, you pick up the pile with it and must clear your hand again.\n\n" +
            "<b><color=#F2C24D>Controls</color></b>\nDrag a card onto the pile to play it. Or tap cards to select them (tap again to deselect) and press PLAY. To play several of the same rank, tap them all, then drag one. Cards you can't play are darkened; tap one to see why. The arrows around the table show the direction of play.";

        // ------------------------------------------------------------------ settings

        private void BuildSettings()
        {
            Transform p = BuildScreen("Settings", new Vector2(1100f, 860f), out settingsPanel, -370f);
            float y = 230f;
            const float step = 92f;
            const float labelX = -330f, controlX = 150f;
            var controlSize = new Vector2(600f, 60f);

            Row(p, "Music", y);
            UIKit.Slider(p, new Vector2(600f, 44f), new Vector2(controlX, y), 0f, 1f, GameSettings.MusicVolume, false, v => GameSettings.MusicVolume = v);
            y -= step;
            Row(p, "Sound effects", y);
            UIKit.Slider(p, new Vector2(600f, 44f), new Vector2(controlX, y), 0f, 1f, GameSettings.SfxVolume, false, v =>
            {
                GameSettings.SfxVolume = v;
            });
            y -= step;
            Row(p, "Animations", y);
            UIKit.Segmented(p, GameSettings.AnimationNames, (int)GameSettings.Animation, controlSize, new Vector2(controlX, y),
                i => GameSettings.Animation = (GameSettings.AnimationSpeed)i);
            y -= step;
            Row(p, "Frame rate", y);
            UIKit.Segmented(p, GameSettings.FrameRateNames, GameSettings.FrameRateIndex, controlSize, new Vector2(controlX, y),
                i => GameSettings.FrameRateIndex = i);
            y -= step;
            Row(p, "Table", y);
            UIKit.Segmented(p, GameSettings.TableColourNames, GameSettings.TableColour, controlSize, new Vector2(controlX, y), i =>
            {
                GameSettings.TableColour = i;
                felt.sprite = tableTheme.CurrentTable;
            });
            y -= step;
            Row(p, "Card back", y);
            UIKit.Segmented(p, GameSettings.CardBackNames, GameSettings.CardBack, controlSize, new Vector2(controlX, y), i =>
            {
                GameSettings.CardBack = i;
                foreach (var b in decorBacks) b.Theme = cardTheme; // re-applies the chosen back
            });

            void Row(Transform parent, string label, float rowY)
            {
                UIKit.Label(parent, label, 28f, new Vector2(300f, 50f), new Vector2(labelX, rowY), TextAlignmentOptions.Left, UIKit.Text, true);
            }

            UIKit.Label(p, "Changes are saved automatically.", 22f, new Vector2(900f, 40f), new Vector2(0f, -300f), TextAlignmentOptions.Center, UIKit.Muted);
        }

        // ------------------------------------------------------------------ credits

        private void BuildCredits()
        {
            Transform p = BuildScreen("Credits", new Vector2(1000f, 680f), out creditsPanel);
            var text = UIKit.Label(p,
                "<color=#F2C24D><b>PALACE</b></color>\n\n" +
                "Game design and house rules\n<b>Damon Gilbert</b>\n\n" +
                "Programming, art and sound\n<b>Built with Claude (Anthropic)</b>\n\n" +
                "Made with Unity\nFont: Liberation Sans (SIL Open Font License)\n" +
                "All card art, table art and sounds are generated in code for this game.",
                28f, new Vector2(900f, 440f), new Vector2(0f, 20f), TextAlignmentOptions.Center, UIKit.Text);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true;
        }

        /// <summary>Creates a titled panel with a Back button and returns the panel's transform.</summary>
        private Transform BuildScreen(string title, Vector2 size, out GameObject screen, float backY = 0f)
        {
            screen = UIBuild.Stretch(title, root).gameObject;
            var panel = UIKit.Panel(screen.transform, size, Vector2.zero);
            UIKit.Label(panel.transform, title, 54f, new Vector2(size.x - 60f, 80f), new Vector2(0f, size.y * 0.5f - 60f),
                TextAlignmentOptions.Center, UIKit.Gold, true);
            float y = backY != 0f ? backY : -size.y * 0.5f + 60f;
            UIKit.Button(panel.transform, "Back", new Vector2(220f, 70f), new Vector2(0f, y), () => Show(mainPanel));
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

            var shots = new (GameObject panel, string name)[]
            {
                (mainPanel, "menu_main"), (singlePanel, "menu_single"), (multiPanel, "menu_multiplayer"),
                (howPanel, "menu_howto"), (settingsPanel, "menu_settings"), (creditsPanel, "menu_credits")
            };
            foreach (var shot in shots)
            {
                Show(shot.panel);
                yield return new WaitForSecondsRealtime(0.4f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, shot.name + ".png"));
                yield return new WaitForSecondsRealtime(0.3f);
            }

            if (Array.IndexOf(args, "-palace-autoplay") >= 0) SceneManager.LoadScene(GameLaunch.GameScene);
            else Application.Quit();
        }
    }
}
