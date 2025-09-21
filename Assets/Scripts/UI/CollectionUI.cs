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
            // Show all cards we know about, but mark unowned as L0
            foreach (var card in GameDB.Cards)
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
