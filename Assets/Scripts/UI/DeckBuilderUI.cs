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
        [Header("Sorting")]
        [Tooltip("Optional dropdown to control rarity order. If not assigned, defaults to ascending (Common -> Legendary).")]
        [SerializeField] private TMP_Dropdown raritySortDropdown;
        [Tooltip("If true, rarity order is reversed (Legendary -> Common)")]
        [SerializeField] private bool rarityDescending = false;
        [Header("Rarity Order Override")]
        [Tooltip("Optional explicit rarity order from lowest to highest (e.g., Common, Rare, Epic, Legendary). Leave empty to use GameDB order.")]
        [SerializeField] private List<RarityDefinition> rarityOrderOverride;

        private readonly List<CardItemUI> _collectionItems = new();
        private readonly List<DeckSlotUI> _deckSlots = new();

        private void OnEnable()
        {
            Refresh();
            SetupSortingUI();
            if (raritySortDropdown != null) raritySortDropdown.onValueChanged.AddListener(HandleRaritySortChanged);
        }

        private void OnDisable()
        {
            if (raritySortDropdown != null) raritySortDropdown.onValueChanged.RemoveListener(HandleRaritySortChanged);
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

            GameDB.EnsureLoaded();
            // Build rarity priority map if an explicit order is provided
            System.Collections.Generic.Dictionary<RarityDefinition, int> rarityPriorityOverrideMap = null;
            if (rarityOrderOverride != null && rarityOrderOverride.Count > 0)
            {
                rarityPriorityOverrideMap = new System.Collections.Generic.Dictionary<RarityDefinition, int>(rarityOrderOverride.Count);
                for (int i = 0; i < rarityOrderOverride.Count; i++)
                {
                    var r = rarityOrderOverride[i];
                    if (r != null && !rarityPriorityOverrideMap.ContainsKey(r)) rarityPriorityOverrideMap[r] = i;
                }
            }
            int GetRarityPriority(RarityDefinition r)
            {
                if (r == null) return int.MaxValue;
                if (rarityPriorityOverrideMap != null && rarityPriorityOverrideMap.TryGetValue(r, out var idx)) return idx;
                var rs = GameDB.Rarities;
                for (int i = 0; i < rs.Count; i++) if (rs[i] == r) return i;
                return int.MaxValue - 1;
            }

            // Collect owned cards only
            var owned = new List<CardDefinition>();
            foreach (var card in GameDB.Cards)
            {
                var cp = PlayerProfile.GetOrCreateCard(card.CardId);
                if (cp.ownedCount <= 0) continue; // show only owned
                owned.Add(card);
            }
            // Sort owned by rarity (asc/desc), then by display name
            owned.Sort((a, b) =>
            {
                int ar = GetRarityPriority(a.Rarity);
                int br = GetRarityPriority(b.Rarity);
                if (ar != br) return rarityDescending ? br.CompareTo(ar) : ar.CompareTo(br);
                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
            });

            foreach (var card in owned)
            {
                var cp = PlayerProfile.GetOrCreateCard(card.CardId);
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

        // ===== Sorting UI Helpers =====
        private void SetupSortingUI()
        {
            if (raritySortDropdown == null) return;
            if (raritySortDropdown.options == null || raritySortDropdown.options.Count == 0)
            {
                raritySortDropdown.options = new List<TMP_Dropdown.OptionData>
                {
                    new TMP_Dropdown.OptionData("Rarity: Common → Legendary"),
                    new TMP_Dropdown.OptionData("Rarity: Legendary → Common"),
                };
            }
            int desired = rarityDescending ? 1 : 0;
            if (raritySortDropdown.value != desired)
            {
                raritySortDropdown.SetValueWithoutNotify(desired);
            }
        }

        private void HandleRaritySortChanged(int index)
        {
            bool desc = (index == 1);
            if (desc != rarityDescending)
            {
                rarityDescending = desc;
                RefreshCollection();
            }
        }
    }
}
