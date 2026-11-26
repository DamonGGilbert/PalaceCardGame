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
    public class CardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
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
            if (interactable && !eventData.dragging) Clicked?.Invoke(this);
        }

        // ------------------------------------------------------------------
        // Drag and drop
        // ------------------------------------------------------------------

        /// <summary>Asked when a drag starts; return false to refuse (e.g. the card can't be played).</summary>
        public Func<CardView, bool> CanDrag;
        public event Action<CardView, PointerEventData> DragMoved;
        public event Action<CardView, PointerEventData> DragEnded;

        private bool dragging;
        private Vector2 homePosition;
        private Vector2 grabOffset;
        private int homeSibling;
        private Vector3 homeScale;
        private float homeAngle;     // hand cards sit tilted in a fan
        private Coroutine returning;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!interactable || (CanDrag != null && !CanDrag(this)) || !(transform.parent is RectTransform parent))
            {
                eventData.pointerDrag = null; // cancel: no OnDrag / OnEndDrag for this gesture
                return;
            }

            if (returning != null) StopCoroutine(returning);
            dragging = true;
            var rt = (RectTransform)transform;
            homePosition = rt.anchoredPosition;
            homeSibling = rt.GetSiblingIndex();
            homeScale = rt.localScale;
            homeAngle = rt.localEulerAngles.z;
            rt.localEulerAngles = Vector3.zero; // held upright while dragging
            rt.SetAsLastSibling();
            rt.localScale = homeScale * 1.08f; // lift it slightly as it's picked up
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector2 local);
            grabOffset = local - homePosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            var parent = (RectTransform)transform.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
                ((RectTransform)transform).anchoredPosition = local - grabOffset;
            DragMoved?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            DragEnded?.Invoke(this, eventData);
        }

        /// <summary>Glide back to where the drag started (dropped somewhere that isn't the pile).</summary>
        public void ReturnHome()
        {
            var rt = (RectTransform)transform;
            rt.SetSiblingIndex(homeSibling);
            if (!gameObject.activeInHierarchy) { SnapHome(); return; }
            returning = StartCoroutine(Tween.Move(rt, rt.anchoredPosition, homePosition, Mathf.Max(0.08f, Tween.Scaled(0.18f)),
                rt.localScale.x, homeScale.x, 0f, Mathf.DeltaAngle(0f, homeAngle)));
        }

        /// <summary>Jump straight back to where the drag started.</summary>
        public void SnapHome()
        {
            var rt = (RectTransform)transform;
            rt.anchoredPosition = homePosition;
            rt.localScale = homeScale;
            rt.localEulerAngles = new Vector3(0f, 0f, homeAngle);
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
            EnsureClassicParts();

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
                new Vector2(0f, 14f), TextAlignmentOptions.Center, null, true, false);

            back = UIBuild.StretchImage("Back", visual, null, Color.white);
            dim = UIBuild.StretchImage("Dim", visual, null, new Color(0f, 0f, 0f, 0.45f));

            ApplyTheme();
        }

        private TextMeshProUGUI MakeRankText(string name, Transform parent, Vector2 position, Vector2 anchor)
        {
            var text = UIBuild.Text(name, parent, "", 34f, Color.black, new Vector2(50f, 40f), Vector2.zero,
                TextAlignmentOptions.Center, null, true, false);
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
            face.sprite = theme.Face;
            back.sprite = theme.CurrentBack;
            courtFrame.sprite = theme.outline;
            dim.sprite = theme.Face;

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
            Color ink = theme != null ? theme.InkFor(c.Suit) : (c.IsRed ? Color.red : Color.black);
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

            // Classic faces: compact corners, pip patterns and court pictures, like a real deck.
            EnsureClassicParts();
            Sprite courtArt = GameSettings.ClassicFaces && c.IsCourt ? CourtArt.Get(c.Rank, c.Suit) : null;
            bool classic = GameSettings.ClassicFaces && (!c.IsCourt || courtArt != null);
            LayoutCorners(classic && !GameSettings.LargeText); // Large: big corners on every style
            UIBuild.SetActive(courtPicture, courtArt != null);
            if (classic)
            {
                UIBuild.SetActive(centerLetter, false);
                UIBuild.SetActive(courtFrame, false);
                if (courtIcon != null) UIBuild.SetActive(courtIcon, false);
                bool pips = c.Rank >= Rank.Two && c.Rank <= Rank.Ten;
                ShowPips(pips ? (int)c.Rank : 0, suitSprite, ink);
                UIBuild.SetActive(suitCenter, c.Rank == Rank.Ace);
                if (courtArt != null) courtPicture.sprite = courtArt;
                if (c.Rank == Rank.Ace)
                {
                    suitCenter.rectTransform.sizeDelta = new Vector2(84f, 84f);
                    suitCenter.rectTransform.anchoredPosition = Vector2.zero;
                }
                return;
            }
            ShowPips(0, null, ink);
            UIBuild.SetActive(suitCenter, true);

            bool court = c.IsCourt;
            Sprite picture = court && theme != null ? theme.GetCourtIcon(c.Rank) : null;
            UIBuild.SetActive(centerLetter, court && picture == null);
            UIBuild.SetActive(courtFrame, court);
            if (picture != null) EnsureCourtIcon();
            if (courtIcon != null) UIBuild.SetActive(courtIcon, picture != null);

            if (court)
            {
                courtFrame.color = new Color(ink.r, ink.g, ink.b, 0.35f);
                if (picture != null)
                {
                    // Illustrated court card: crown / tiara / shield picture with the suit beneath.
                    courtIcon.sprite = picture;
                    courtIcon.color = ink;
                    suitCenter.rectTransform.sizeDelta = new Vector2(36f, 36f);
                    suitCenter.rectTransform.anchoredPosition = new Vector2(0f, -50f);
                }
                else
                {
                    centerLetter.text = c.RankLabel;
                    centerLetter.color = ink;
                    suitCenter.rectTransform.sizeDelta = new Vector2(38f, 38f);
                    suitCenter.rectTransform.anchoredPosition = new Vector2(0f, -42f);
                }
            }
            else
            {
                float size = c.Rank == Rank.Ace ? 96f : 70f;
                suitCenter.rectTransform.sizeDelta = new Vector2(size, size);
                suitCenter.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        [SerializeField] private Image courtIcon;
        [SerializeField] private Image courtPicture;
        [SerializeField] private RectTransform pipRoot;
        private readonly System.Collections.Generic.List<Image> pipImages = new System.Collections.Generic.List<Image>();
        private bool? compactCorners;

        // Traditional pip patterns: (column, row) with columns -1/0/1 and rows from +1 (top) to -1 (bottom).
        // Pips in the bottom half are drawn upside down, as on a real card.
        private static readonly Vector2[][] PipLayouts =
        {
            null, null,
            new[] { P(0, 1), P(0, -1) },                                                                 // 2
            new[] { P(0, 1), P(0, 0), P(0, -1) },                                                        // 3
            new[] { P(-1, 1), P(1, 1), P(-1, -1), P(1, -1) },                                            // 4
            new[] { P(-1, 1), P(1, 1), P(0, 0), P(-1, -1), P(1, -1) },                                   // 5
            new[] { P(-1, 1), P(1, 1), P(-1, 0), P(1, 0), P(-1, -1), P(1, -1) },                         // 6
            new[] { P(-1, 1), P(1, 1), P(0, 0.5f), P(-1, 0), P(1, 0), P(-1, -1), P(1, -1) },             // 7
            new[] { P(-1, 1), P(1, 1), P(0, 0.5f), P(-1, 0), P(1, 0), P(0, -0.5f), P(-1, -1), P(1, -1) }, // 8
            new[] { P(-1, 1), P(1, 1), P(-1, 1f / 3f), P(1, 1f / 3f), P(0, 0),
                    P(-1, -1f / 3f), P(1, -1f / 3f), P(-1, -1), P(1, -1) },                              // 9
            new[] { P(-1, 1), P(1, 1), P(0, 2f / 3f), P(-1, 1f / 3f), P(1, 1f / 3f),
                    P(-1, -1f / 3f), P(1, -1f / 3f), P(0, -2f / 3f), P(-1, -1), P(1, -1) }              // 10
        };

        private static Vector2 P(float column, float row) => new Vector2(column, row);

        /// <summary>The Classic court picture and pip holder, placed just under the corner labels.</summary>
        private void EnsureClassicParts()
        {
            if (courtPicture != null && pipRoot != null) return;
            int under = courtFrame.transform.GetSiblingIndex() + 1;
            if (courtPicture == null)
            {
                courtPicture = UIBuild.Image("CourtArt", face.transform, null, Color.white, new Vector2(78f, 125f), Vector2.zero);
                courtPicture.preserveAspect = true;
                courtPicture.raycastTarget = false;
                courtPicture.transform.SetSiblingIndex(under);
                UIBuild.SetActive(courtPicture, false);
            }
            if (pipRoot == null)
            {
                pipRoot = UIBuild.Rect("Pips", face.transform, new Vector2(Width, Height), Vector2.zero);
                pipRoot.SetSiblingIndex(under + 1);
            }
        }

        // With Large text the corners are big, so the pips move in a little to make room.
        private static float PipColumn => GameSettings.LargeText ? 22f : 25f;
        private static float PipTop => GameSettings.LargeText ? 60f : 66f;

        private void ShowPips(int count, Sprite suit, Color ink)
        {
            Vector2[] layout = count >= 2 && count <= 10 ? PipLayouts[count] : null;
            int n = layout?.Length ?? 0;
            while (pipImages.Count < n)
            {
                var img = UIBuild.Image("Pip", pipRoot, null, Color.white, new Vector2(24f, 24f), Vector2.zero);
                img.preserveAspect = true;
                img.raycastTarget = false;
                pipImages.Add(img);
            }
            float size = (count >= 7 ? 22f : 25f) - (GameSettings.LargeText ? 2f : 0f);
            for (int i = 0; i < pipImages.Count; i++)
            {
                var img = pipImages[i];
                bool on = i < n;
                UIBuild.SetActive(img, on);
                if (!on) continue;
                Vector2 p = layout[i];
                img.sprite = suit;
                img.color = ink;
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(p.x * PipColumn, p.y * PipTop);
                rt.localEulerAngles = new Vector3(0f, 0f, p.y < 0f ? 180f : 0f);
            }
        }

        /// <summary>Corner labels: compact (Classic, out of the way of the pips and picture) or big (Simple).</summary>
        private void LayoutCorners(bool compact)
        {
            if (compactCorners == compact) return;
            compactCorners = compact;
            float rankSize = compact ? 30f : 34f;
            Vector2 rankPos = compact ? new Vector2(17f, -21f) : new Vector2(26f, -25f);
            Vector2 suitPos = compact ? new Vector2(17f, -47f) : new Vector2(26f, -56f);
            Vector2 suitSize = compact ? new Vector2(20f, 20f) : new Vector2(24f, 24f);
            rankTop.fontSize = rankBottom.fontSize = rankSize;
            rankTop.characterSpacing = rankBottom.characterSpacing = compact ? -6f : 0f; // keeps "10" narrow
            rankTop.rectTransform.anchoredPosition = rankPos;
            rankBottom.rectTransform.anchoredPosition = -rankPos;
            suitTop.rectTransform.anchoredPosition = suitPos;
            suitBottom.rectTransform.anchoredPosition = -suitPos;
            suitTop.rectTransform.sizeDelta = suitBottom.rectTransform.sizeDelta = suitSize;
        }

        private void EnsureCourtIcon()
        {
            if (courtIcon != null) return;
            courtIcon = UIBuild.Image("CourtPicture", face.transform, null, Color.white, new Vector2(94f, 94f), new Vector2(0f, 14f));
            courtIcon.preserveAspect = true;
        }
    }
}
