using System;
using System.Collections.Generic;
using Palace.Core;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// The local player's hand along the bottom of the screen. Cards overlap more as the
    /// hand grows (e.g. after picking up a big pile) so they always fit the hand area.
    /// Display only: taps are reported through CardTapped, and the controller decides what they mean.
    /// </summary>
    public sealed class HandView
    {
        private const float MaxSpacing = 150f;
        private const float AreaWidth = 1000f;
        private const float FanAngle = 12f;   // degrees at the ends of a full hand
        private const float FanDip = 34f;     // how far the end cards drop

        private readonly TableView table;
        private readonly List<CardView> pool = new List<CardView>();
        private readonly Dictionary<int, CardView> byId = new Dictionary<int, CardView>();

        public event Action<Card> CardTapped;

        /// <summary>Asked when the player starts dragging a hand card; return false to refuse.</summary>
        public Func<Card, bool> CanDrag;
        public event Action<CardView, UnityEngine.EventSystems.PointerEventData> CardDragged;
        public event Action<CardView, UnityEngine.EventSystems.PointerEventData> CardDropped;

        public HandView(TableView table)
        {
            this.table = table;
        }

        public CardView Find(int cardId) => byId.TryGetValue(cardId, out var v) ? v : null;

        public void Show(IReadOnlyList<Card> cards, ICollection<int> selected, Func<Card, bool> isPlayable, bool interactable)
        {
            while (pool.Count < cards.Count)
            {
                CardView view = CardFactory.Spawn(table.CardPrefab, table.CardTheme, table.HandRoot);
                view.Clicked += OnClicked;
                view.CanDrag = v => v.CurrentCard.HasValue && CanDrag != null && CanDrag(v.CurrentCard.Value);
                view.DragMoved += (v, e) => CardDragged?.Invoke(v, e);
                view.DragEnded += (v, e) => CardDropped?.Invoke(v, e);
                pool.Add(view);
            }

            byId.Clear();
            int n = cards.Count;
            float spacing = n > 1 ? Mathf.Min(MaxSpacing, (AreaWidth - CardView.Width) / (n - 1)) : 0f;

            for (int i = 0; i < pool.Count; i++)
            {
                CardView view = pool[i];
                bool active = i < n;
                view.gameObject.SetActive(active);
                if (!active) continue;

                Card card = cards[i];
                byId[card.Id] = view;
                view.name = "Hand " + card;
                view.ShowCard(card, true);
                view.SetSelected(selected != null && selected.Contains(card.Id));
                view.SetDimmed(isPlayable != null && !isPlayable(card));
                view.SetInteractable(interactable);

                // A gentle curved fan, like cards held in a hand: outer cards tilt out and dip down.
                float x = (i - (n - 1) * 0.5f) * spacing;
                float k = x / (AreaWidth * 0.5f);   // -1 .. 1 across the hand area
                var rt = (RectTransform)view.transform;
                rt.SetSiblingIndex(i);
                rt.anchoredPosition = new Vector2(x, -k * k * FanDip);
                rt.localScale = Vector3.one;
                rt.localEulerAngles = new Vector3(0f, 0f, -k * FanAngle);
            }
        }

        private void OnClicked(CardView view)
        {
            if (view.CurrentCard.HasValue) CardTapped?.Invoke(view.CurrentCard.Value);
        }
    }
}
