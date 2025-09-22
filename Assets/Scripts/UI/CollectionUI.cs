using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Data;

namespace TR.UI
{
    // Displays owned cards with levels and lets you upgrade when possible.
    public class CollectionUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private CollectionItemUI itemPrefab;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text softCurrencyText; // shows current coins

        private readonly List<CollectionItemUI> _items = new();

        private void OnEnable()
        {
            Refresh();
            PlayerProfile.OnSoftCurrencyChanged += HandleSoftCurrencyChanged;
        }

        private void OnDisable()
        {
            PlayerProfile.OnSoftCurrencyChanged -= HandleSoftCurrencyChanged;
        }

        private void HandleSoftCurrencyChanged(int newBalance)
        {
            if (softCurrencyText)
            {
                softCurrencyText.text = $"Coins: {newBalance}";
            }
        }

        public void Refresh()
        {
            if (headerText) headerText.text = "Collection";
            if (softCurrencyText) softCurrencyText.text = $"Coins: {PlayerProfile.GetSoftCurrency()}";
            foreach (var it in _items) if (it) Destroy(it.gameObject);
            _items.Clear();

            GameDB.EnsureLoaded();
            // Compute rarity priority using order in GameDB.Rarities (lower index = more common)
            int GetRarityPriority(RarityDefinition r)
            {
                if (r == null) return int.MaxValue;
                var rs = GameDB.Rarities;
                for (int i = 0; i < rs.Count; i++) if (rs[i] == r) return i;
                return int.MaxValue - 1;
            }

            // Sort policy:
            // 1) Owned cards first (discovered)
            // 2) Then cards unlocked for current trophies but not owned yet
            // 3) Then cards locked by higher arenas
            // Within each group: sort by rarity (GameDB.Rarities order), then by display name
            var sorted = new List<CardDefinition>(GameDB.Cards);
            sorted.Sort((a, b) =>
            {
                var cpa = PlayerProfile.GetOrCreateCard(a.CardId);
                var cpb = PlayerProfile.GetOrCreateCard(b.CardId);
                bool aOwned = cpa.ownedCount > 0;
                bool bOwned = cpb.ownedCount > 0;
                if (aOwned != bOwned) return bOwned.CompareTo(aOwned); // true first

                bool aUnlockNow = a.IsUnlockedForPlayer();
                bool bUnlockNow = b.IsUnlockedForPlayer();
                // section: 0 owned, 1 unlocked-not-owned, 2 locked
                int aSection = aOwned ? 0 : (aUnlockNow ? 1 : 2);
                int bSection = bOwned ? 0 : (bUnlockNow ? 1 : 2);
                if (aSection != bSection) return aSection.CompareTo(bSection);

                int ar = GetRarityPriority(a.Rarity);
                int br = GetRarityPriority(b.Rarity);
                if (ar != br) return ar.CompareTo(br);

                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
            });

            // Build UI in sorted order
            foreach (var card in sorted)
            {
                var ui = Instantiate(itemPrefab, listRoot);
                var cp = PlayerProfile.GetOrCreateCard(card.CardId);
                ui.Bind(card);
                // No extra wiring needed: CardItemUI handles hover details
                _items.Add(ui);
            }
        }
    }
}
