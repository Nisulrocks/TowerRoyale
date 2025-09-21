using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TR.Systems;
using TR.Data;

namespace TR.Battle
{
    // Simple bottom bar that shows the player's deck and lets you select a card to place.
    public class BattleDeckBarUI : MonoBehaviour
    {
        [Header("UI")] 
        [SerializeField] private Transform deckRoot;          // parent for card UI elements
        [SerializeField] private TR.UI.CardItemUI cardItemPrefab; // reuse CardItemUI for display
        [Header("Placement Ref")]
        [SerializeField] private TowerPlacementController placement;

        private readonly List<TR.UI.CardItemUI> _items = new();
        public System.Action<CardDefinition> onSelectCard;

        public void BindFromPlayerDeck()
        {
            GameDB.EnsureLoaded();
            // clear old
            foreach (var it in _items) if (it) Destroy(it.gameObject);
            _items.Clear();

            var deck = PlayerProfile.Data.deck;
            Debug.Log($"[DeckBar] Binding deck with {deck.Count} entries");
            foreach (var cardId in deck)
            {
                var card = GameDB.GetCardById(cardId);
                if (card == null) continue;
                var cp = PlayerProfile.GetOrCreateCard(card.CardId);
                int level = Mathf.Max(1, cp.level);

                var ui = Instantiate(cardItemPrefab, deckRoot);
                ui.Bind(card, level);
                Debug.Log($"[DeckBar] Added card to bar: {card.DisplayName} (L{level})");

                // Choose the best raycast target GO to receive drag events
                UnityEngine.UI.Button btn = ui.GetComponentInChildren<UnityEngine.UI.Button>(true);
                GameObject attachGO = ui.gameObject;
                if (btn != null)
                {
                    if (btn.targetGraphic != null)
                    {
                        btn.targetGraphic.raycastTarget = true;
                        attachGO = btn.targetGraphic.gameObject;
                    }
                    else
                    {
                        attachGO = btn.gameObject;
                    }
                }
                // Ensure a raycastable Graphic on the chosen GO
                var graphic = attachGO.GetComponent<UnityEngine.UI.Graphic>();
                if (graphic == null) graphic = attachGO.AddComponent<UnityEngine.UI.Image>();
                graphic.raycastTarget = true;
                // Attach drag handler there
                var drag = attachGO.GetComponent<CardDragPlacement>();
                if (drag == null) drag = attachGO.AddComponent<CardDragPlacement>();
                drag.Init(card, placement);
                Debug.Log($"[DeckBar] Drag attached to GO={attachGO.name} for card {card.DisplayName}");

                _items.Add(ui);
            }
        }
    }
}
