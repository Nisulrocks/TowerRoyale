using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data;
using TR.Systems;

namespace TR.UI
{
    public class DeckBuilderUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform collectionListRoot;
        [SerializeField] private CardItemUI collectionItemPrefab;
        [SerializeField] private Transform deckSlotsRoot;
        [SerializeField] private DeckSlotUI deckSlotPrefab;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text deckCountText;

        private readonly List<CardItemUI> _collectionItems = new();
        private readonly List<DeckSlotUI> _deckSlots = new();

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (headerText) headerText.text = $"Deck Builder (Max {DeckService.MaxDeckSize})";
            RefreshCollection();
            RefreshDeck();
        }

        private void RefreshCollection()
        {
            foreach (var it in _collectionItems) if (it) Destroy(it.gameObject);
            _collectionItems.Clear();

            foreach (var card in GameDB.Cards)
            {
                var cp = PlayerProfile.GetOrCreateCard(card.CardId);
                if (cp.ownedCount <= 0) continue; // show only owned
                var ui = Instantiate(collectionItemPrefab, collectionListRoot);
                ui.Bind(card, cp.level);
                // Add click handler to add/remove from deck (be robust if Button missing)
                var button = ui.GetComponent<Button>();
                if (button == null) button = ui.GetComponentInChildren<Button>();
                if (button == null)
                {
                    // Ensure there's a Graphic for the Button target
                    var img = ui.GetComponent<Image>();
                    if (img == null) img = ui.gameObject.AddComponent<Image>();
                    button = ui.gameObject.AddComponent<Button>();
                }
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnToggleCard(card.CardId));
                // No hover wiring here; CardItemUI handles hover details itself
                _collectionItems.Add(ui);
            }
        }

        private void RefreshDeck()
        {
            foreach (var it in _deckSlots) if (it) Destroy(it.gameObject);
            _deckSlots.Clear();

            var deck = DeckService.GetDeck();
            int i = 0;
            foreach (var cardId in deck)
            {
                var card = GameDB.GetCardById(cardId);
                var slot = Instantiate(deckSlotPrefab, deckSlotsRoot);
                slot.Bind(card, OnRemoveFromDeck);
                // No hover wiring here; if deck slot contains a CardItemUI, it will handle hover
                _deckSlots.Add(slot);
                i++;
            }
            // Fill remaining slots visually
            for (; i < DeckService.MaxDeckSize; i++)
            {
                var slot = Instantiate(deckSlotPrefab, deckSlotsRoot);
                slot.Clear();
                _deckSlots.Add(slot);
            }

            if (deckCountText) deckCountText.text = $"{deck.Count}/{DeckService.MaxDeckSize}";
        }

        private void OnToggleCard(string cardId)
        {
            if (DeckService.IsInDeck(cardId))
            {
                bool removed = DeckService.TryRemoveFromDeck(cardId);
                Debug.Log($"TR Deck: Remove {cardId} -> {removed}");
            }
            else
            {
                bool added = DeckService.TryAddToDeck(cardId);
                Debug.Log($"TR Deck: Add {cardId} -> {added} (count {DeckService.GetDeck().Count}/{DeckService.MaxDeckSize})");
            }
            RefreshDeck();
        }

        private void OnRemoveFromDeck(string cardId)
        {
            DeckService.TryRemoveFromDeck(cardId);
            RefreshDeck();
        }
    }
}
