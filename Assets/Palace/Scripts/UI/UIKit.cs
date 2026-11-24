using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// Builds the game's menu widgets (buttons, sliders, toggles, text boxes) in one
    /// consistent style. Call UIKit.Init(tableTheme) once before building a screen.
    /// </summary>
    public static class UIKit
    {
        // Style colours: set from the TableTheme by Init (these are the classic defaults).
        public static Color Gold = new Color(0.95f, 0.76f, 0.30f);
        public static Color Dark = new Color(0.05f, 0.07f, 0.06f, 0.94f);
        public static Color DarkButton = new Color(0.10f, 0.14f, 0.12f, 0.96f);
        public static Color Ink = new Color(0.14f, 0.09f, 0.02f);
        public static Color Text = new Color(0.95f, 0.93f, 0.89f);
        public static Color Muted = new Color(0.68f, 0.72f, 0.66f);
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.72f);

        public static Sprite PanelSprite;
        public static Sprite CircleSprite;
        public static Sprite PrimarySprite;    // painted button art (optional)
        public static Sprite SecondarySprite;
        public static TMP_FontAsset Font;

        public static void Init(TableTheme theme)
        {
            if (theme == null) return;
            PanelSprite = theme.panel;
            CircleSprite = theme.circle;
            PrimarySprite = theme.buttonPrimary;
            SecondarySprite = theme.buttonSecondary;
            Font = theme.font;
            Gold = theme.uiAccentColor;
            Text = theme.uiTextColor;
            Muted = theme.uiMutedColor;
            Dark = theme.uiPanelColor;
            Ink = theme.uiInkColor;
        }

        /// <summary>Full-screen layer. With blockClicks it stops taps reaching anything underneath.</summary>
        public static RectTransform Layer(Transform parent, string name, Color color, bool blockClicks = true)
        {
            var img = UIBuild.StretchImage(name, parent, null, color);
            img.raycastTarget = blockClicks;
            return img.rectTransform;
        }

        public static Image Panel(Transform parent, Vector2 size, Vector2 position, Color? color = null)
        {
            var img = UIBuild.Image("Panel", parent, PanelSprite, color ?? Dark, size, position);
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Vector2 rectSize, Vector2 position,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null, bool bold = false)
        {
            return UIBuild.Text("Label", parent, text, size, color ?? Text, rectSize, position, align, Font, bold);
        }

        public static Button Button(Transform parent, string label, Vector2 size, Vector2 position, Action onClick,
            bool primary = false, float fontSize = 30f)
        {
            var img = UIBuild.Image("Button " + label, parent, PanelSprite, Color.white, size, position);
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;

            var button = img.gameObject.AddComponent<Button>();
            img.gameObject.AddComponent<ButtonBounce>(); // a little squish when pressed
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var text = UIBuild.Text("Text", img.transform, label, fontSize, Text, size - new Vector2(16f, 0f), Vector2.zero,
                TextAlignmentOptions.Center, Font, true);
            SetButtonStyle(button, primary);

            button.onClick.AddListener(() =>
            {
                AudioManager.Play(Sfx.Button, 0.7f);
                onClick?.Invoke();
            });
            return button;
        }

        /// <summary>A small round "i" button that opens a pop-up explaining something.</summary>
        public static Button InfoButton(Transform parent, Vector2 position, string title, string body)
        {
            var img = UIBuild.Image("Info", parent, CircleSprite, Gold, new Vector2(48f, 48f), position);
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            img.gameObject.AddComponent<ButtonBounce>(); // a little squish when pressed
            button.targetGraphic = img;
            UIBuild.Text("i", img.transform, "i", 32f, Ink, new Vector2(48f, 48f), Vector2.zero, TextAlignmentOptions.Center, Font, true, false);
            button.onClick.AddListener(() =>
            {
                AudioManager.Play(Sfx.Button, 0.7f);
                var canvas = button.GetComponentInParent<Canvas>();
                InfoPopup(canvas != null ? canvas.rootCanvas.transform : parent, title, body);
            });
            return button;
        }

        /// <summary>A pop-up with a title, some text and an OK button. Tapping outside also closes it.</summary>
        public static void InfoPopup(Transform parent, string title, string body)
        {
            var layer = Layer(parent, "InfoPopup", Scrim);
            layer.SetAsLastSibling();
            layer.gameObject.AddComponent<Button>().onClick.AddListener(() => UnityEngine.Object.Destroy(layer.gameObject));
            var panel = Panel(layer, new Vector2(980f, 520f), Vector2.zero);
            Label(panel.transform, title, 46f, new Vector2(900f, 64f), new Vector2(0f, 190f), TextAlignmentOptions.Center, Gold, true);
            var text = Label(panel.transform, body, 28f, new Vector2(900f, 290f), new Vector2(0f, 10f), TextAlignmentOptions.Center, Text);
            text.textWrappingMode = TextWrappingModes.Normal;
            Button(panel.transform, "OK", new Vector2(240f, 76f), new Vector2(0f, -190f), () => UnityEngine.Object.Destroy(layer.gameObject), true);
        }

        public const string SwapInfoTitle = "Swap before play";
        public const string SwapInfoText =
            "An optional game mode. After the deal, and before the first turn, everyone can swap cards between their hand and their face-up cards.\n\n" +
            "Tap a card in your hand, then one of your face-up cards, to swap them. Press READY when you're happy. Play starts when everyone is ready.\n\n" +
            "Tip: put strong cards (10s, 2s, Aces and high cards) face up for the end of the game, and keep low cards in your hand to play early.";

        public static void SetButtonStyle(Button button, bool primary)
        {
            var image = (Image)button.targetGraphic;
            if (PrimarySprite != null && SecondarySprite != null)
            {
                // Painted style: brass for the main action, wood for everything else.
                image.sprite = primary ? PrimarySprite : SecondarySprite;
                image.color = Color.white;
            }
            else image.color = primary ? Gold : DarkButton;

            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.color = primary ? Ink : Text;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = Loc.T(label);
        }

        public static Slider Slider(Transform parent, Vector2 size, Vector2 position, float min, float max, float value,
            bool wholeNumbers, Action<float> onChange)
        {
            var root = UIBuild.Rect("Slider", parent, size, position);
            var slider = root.gameObject.AddComponent<Slider>();

            var bg = UIBuild.Image("Background", root, PanelSprite, new Color(0f, 0f, 0f, 0.55f), new Vector2(size.x, 14f), Vector2.zero);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 3f;

            var fillArea = UIBuild.Stretch("Fill Area", root);
            fillArea.offsetMin = new Vector2(8f, size.y * 0.5f - 7f);
            fillArea.offsetMax = new Vector2(-8f, -(size.y * 0.5f - 7f));
            var fill = UIBuild.StretchImage("Fill", fillArea, PrimarySprite != null ? PrimarySprite : PanelSprite,
                PrimarySprite != null ? Color.white : Gold);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 3f;

            var handleArea = UIBuild.Stretch("Handle Area", root);
            handleArea.offsetMin = new Vector2(16f, 0f);
            handleArea.offsetMax = new Vector2(-16f, 0f);
            var handle = UIBuild.Image("Handle", handleArea, CircleSprite, Color.white, new Vector2(40f, 0f), Vector2.zero);
            handle.rectTransform.sizeDelta = new Vector2(40f, 0f);
            handle.raycastTarget = true;

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return slider;
        }

        /// <summary>A row of options where exactly one is highlighted (like a radio group).</summary>
        public static void Segmented(Transform parent, string[] options, int selected, Vector2 size, Vector2 position, Action<int> onChange)
        {
            var buttons = new Button[options.Length];
            float width = size.x / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var pos = position + new Vector2((i - (options.Length - 1) * 0.5f) * width, 0f);
                buttons[i] = Button(parent, options[i], new Vector2(width - 8f, size.y), pos, () =>
                {
                    for (int b = 0; b < buttons.Length; b++) SetButtonStyle(buttons[b], b == index);
                    onChange?.Invoke(index);
                }, i == selected, 24f);
            }
        }

        public static TMP_InputField Input(Transform parent, Vector2 size, Vector2 position, string value, string placeholder,
            int characterLimit, Action<string> onEndEdit)
        {
            var bg = UIBuild.Image("Input", parent, PanelSprite, new Color(0f, 0f, 0f, 0.5f), size, position);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            var viewport = UIBuild.Stretch("Text Area", bg.transform, 12f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var hint = UIBuild.Text("Placeholder", viewport, placeholder, 28f, Muted, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, Font);
            Fill(hint.rectTransform);
            hint.fontStyle = FontStyles.Italic;
            var text = UIBuild.Text("Text", viewport, "", 28f, Text, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, Font, true);
            Fill(text.rectTransform);
            text.overflowMode = TextOverflowModes.Overflow;

            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = characterLimit;
            input.targetGraphic = bg;
            input.text = value;
            input.onEndEdit.AddListener(v => onEndEdit?.Invoke(v));
            return input;
        }

        public static ScrollRect ScrollText(Transform parent, Vector2 size, Vector2 position, string text, float fontSize)
        {
            var view = UIBuild.Image("Scroll View", parent, null, new Color(0f, 0f, 0f, 0.001f), size, position);
            view.raycastTarget = true; // lets the player drag anywhere in the box
            view.gameObject.AddComponent<RectMask2D>();

            var body = UIBuild.Text("Content", view.transform, text, fontSize, Text, Vector2.zero, Vector2.zero, TextAlignmentOptions.TopLeft, Font);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            body.richText = true;
            var rt = body.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(8f, 0f);
            rt.offsetMax = new Vector2(-8f, 0f);
            rt.anchoredPosition = Vector2.zero;
            var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = rt;
            scroll.viewport = view.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return scroll;
        }

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
