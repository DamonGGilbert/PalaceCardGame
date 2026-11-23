using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Creates CardView instances. Uses the Card prefab when one is assigned
    /// (keeping the prefab link while editing), otherwise builds a card from scratch.
    /// </summary>
    public static class CardFactory
    {
        public static CardView Spawn(CardView prefab, CardTheme theme, Transform parent)
        {
            CardView card;

            if (prefab == null)
            {
                var go = new GameObject("Card", typeof(RectTransform));
                go.layer = UIBuild.UILayer;
                go.transform.SetParent(parent, false);
                card = go.AddComponent<CardView>();
            }
#if UNITY_EDITOR
            else if (!Application.isPlaying)
            {
                var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
                card = go.GetComponent<CardView>();
            }
#endif
            else
            {
                card = Object.Instantiate(prefab, parent, false);
            }

            if (theme != null && card.Theme != theme) card.Theme = theme;
            card.EnsureBuilt();
            return card;
        }
    }
}
