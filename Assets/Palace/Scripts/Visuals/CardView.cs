using System;
using Palace.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Palace
{
    /// <summary>
    /// The visual for ONE card. It only displays what it is told to display:
    /// it never decides whether a play is legal (that is the rules engine's job).
    ///
    /// The child objects (face, back, corner labels...) are created automatically
    /// the first time the card is used, so the prefab only needs this component
    /// and a CardTheme.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardView : MonoBehaviour, IPointerClickHandler
    {
        public const float Width = 140f;
        public const float Height = 196f;

        [SerializeField] private CardTheme theme;
        [SerializeField] private float selectedLift = 28f;

        [Header("Built automatically - do not assign")]
        [SerializeField] private RectTransform visual;
        [SerializeField] private Image shadow;
        [SerializeField] private Image glow;
        [SerializeField] private Image face;
        [SerializeField] private Image back;
        [SerializeField] private Image courtFrame;
        [SerializeField] private Image suitTop;
        [SerializeField] private Image suitBottom;
        [SerializeField] private Image suitCenter;
        [SerializeField] private Image dim;
        [SerializeField] private TextMeshProUGUI rankTop;
        [SerializeField] private TextMeshProUGUI rankBottom;
        [SerializeField] private TextMeshProUGUI centerLetter;

        private Card? card;
        private bool faceUp;
        private bool selected;
        private bool dimmed;
        private bool interactable = true;

        /// <summary>Raised when the player taps/clicks this card.</summary>
        public event Action<CardView> Clicked;

        public Card? CurrentCard => card;
        public bool IsFaceUp => faceUp;
        public bool IsSelected => selected;

        public CardTheme Theme
        {
            get => theme;
            set
            {
                theme = value;
                EnsureBuilt();
                ApplyTheme();
                Refresh();
            }
        }

        private void Awake()
        {
            EnsureBuilt();
            ApplyTheme(); // picks up the card back chosen in Settings
            Refresh();
        }

        /// <summary>Show a specific card. faceUp = false shows its back (e.g. a face-down table card).</summary>
        public void ShowCard(Card value, bool showFace = true)
        {
            card = value;
            faceUp = showFace;
            Refresh();
        }

        /// <summary>Show a card back with no known value (an opponent's hand card, the deck...).</summary>
        public void ShowBack()
        {
            card = null;
            faceUp = false;
            Refresh();
        }

        public void SetFaceUp(bool showFace)
        {
            faceUp = showFace;
            Refresh();
        }

        public void SetSelected(bool value)
        {
            selected = value;
            Refresh();
        }

        /// <summary>Darkens the card, used to show that it cannot be played right now.</summary>
        public void SetDimmed(bool value)
        {
            dimmed = value;
            Refresh();
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            Refresh();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (interactable) Clicked?.Invoke(this);
        }

        // ------------------------------------------------------------------
        // Building and refreshing the visuals
        // ------------------------------------------------------------------

        public void EnsureBuilt()
        {
            if (visual != null) return;

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(Width, Height);

            // The shadow stays on the table when the card is lifted, which gives a sense of depth.
            shadow = UIBuild.Image("Shadow", transform, null, Color.white, new Vector2(Width + 24f, Height + 24f), new Vector2(3f, -6f));

            visual = UIBuild.Stretch("Visual", transform);
            glow = UIBuild.Image("Glow", visual, null, Color.white, new Vector2(Width + 40f, Height + 40f), Vector2.zero);

            face = UIBuild.StretchImage("Face", visual, null, Color.white);
            courtFrame = UIBuild.StretchImage("CourtFrame", face.transform, null, Color.white, 20f);

            var anchorTopLeft = new Vector2(0f, 1f);
            var anchorBottomRight = new Vector2(1f, 0f);

            rankTop = MakeRankText("RankTop", face.transform, new Vector2(26f, -25f), anchorTopLeft);
            suitTop = MakeImage("SuitTop", face.transform, new Vector2(24f, 24f), new Vector2(26f, -56f), anchorTopLeft);

            rankBottom = MakeRankText("RankBottom", face.transform, new Vector2(-26f, 25f), anchorBottomRight);
            rankBottom.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            suitBottom = MakeImage("SuitBottom", face.transform, new Vector2(24f, 24f), new Vector2(-26f, 56f), anchorBottomRight);
            suitBottom.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);

            suitCenter = UIBuild.Image("SuitCenter", face.transform, null, Color.white, new Vector2(72f, 72f), Vector2.zero);
            centerLetter = UIBuild.Text("CenterLetter", face.transform, "", 82f, Color.black, new Vector2(110f, 96f),
                new Vector2(0f, 14f), TextAlignmentOptions.Center, null, true);

            back = UIBuild.StretchImage("Back", visual, null, Color.white);
            dim = UIBuild.StretchImage("Dim", visual, null, new Color(0f, 0f, 0f, 0.45f));

            ApplyTheme();
        }

        private TextMeshProUGUI MakeRankText(string name, Transform parent, Vector2 position, Vector2 anchor)
        {
            var text = UIBuild.Text(name, parent, "", 34f, Color.black, new Vector2(50f, 40f), Vector2.zero,
                TextAlignmentOptions.Center, null, true);
            var rt = text.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static Image MakeImage(string name, Transform parent, Vector2 size, Vector2 position, Vector2 anchor)
        {
            var img = UIBuild.Image(name, parent, null, Color.white, size, Vector2.zero);
            img.rectTransform.anchorMin = anchor;
            img.rectTransform.anchorMax = anchor;
            img.rectTransform.anchoredPosition = position;
            img.preserveAspect = true;
            return img;
        }

        private void ApplyTheme()
        {
            if (theme == null || visual == null) return;

            shadow.sprite = theme.shadow;
            shadow.color = theme.shadowTint;
            glow.sprite = theme.glow;
            glow.color = theme.selectedGlow;
            face.sprite = theme.face;
            back.sprite = theme.CurrentBack;
            courtFrame.sprite = theme.outline;
            dim.sprite = theme.face;

            if (theme.rankFont != null)
            {
                rankTop.font = theme.rankFont;
                rankBottom.font = theme.rankFont;
                centerLetter.font = theme.rankFont;
            }
        }

        private void Refresh()
        {
            if (visual == null) return;

            bool showFace = faceUp && card.HasValue;
            UIBuild.SetActive(face, showFace);
            UIBuild.SetActive(back, !showFace);
            UIBuild.SetActive(glow, selected);
            UIBuild.SetActive(dim, dimmed);

            face.raycastTarget = interactable;
            back.raycastTarget = interactable;
            visual.anchoredPosition = selected ? new Vector2(0f, selectedLift) : Vector2.zero;

            if (!showFace) return;

            Card c = card.Value;
            Color ink = theme != null ? (c.IsRed ? theme.redSuit : theme.blackSuit) : (c.IsRed ? Color.red : Color.black);
            Sprite suitSprite = theme != null ? theme.GetSuitSprite(c.Suit) : null;

            rankTop.text = c.RankLabel;
            rankBottom.text = c.RankLabel;
            rankTop.color = ink;
            rankBottom.color = ink;

            foreach (var img in new[] { suitTop, suitBottom, suitCenter })
            {
                img.sprite = suitSprite;
                img.color = ink;
            }

            bool court = c.IsCourt;
            UIBuild.SetActive(centerLetter, court);
            UIBuild.SetActive(courtFrame, court);

            if (court)
            {
                centerLetter.text = c.RankLabel;
                centerLetter.color = ink;
                courtFrame.color = new Color(ink.r, ink.g, ink.b, 0.35f);
                suitCenter.rectTransform.sizeDelta = new Vector2(38f, 38f);
                suitCenter.rectTransform.anchoredPosition = new Vector2(0f, -42f);
            }
            else
            {
                float size = c.Rank == Rank.Ace ? 96f : 70f;
                suitCenter.rectTransform.sizeDelta = new Vector2(size, size);
                suitCenter.rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }
}
