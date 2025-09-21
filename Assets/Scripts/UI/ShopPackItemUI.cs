using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data;
using TR.Systems;

namespace TR.UI
{
    public class ShopPackItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private TMP_Text costText; // new
        [SerializeField] private Button openButton;

        private PackDefinition _pack;
        private string _packId;
        private System.Action<string> _onOpen;

        public void Bind(PackDefinition pack, System.Action<string> onOpen, int overrideCost = -1)
        {
            _pack = pack;
            _packId = pack != null ? pack.PackId : null;
            _onOpen = onOpen;
            if (nameText) nameText.text = pack != null ? pack.DisplayName : "(null)";
            if (descText) descText.text = pack != null ? $"Cards: {pack.CardsPerPack}" : "";
            int cost = GetEffectiveCost(overrideCost);
            if (costText)
            {
                costText.text = pack != null ? (cost <= 0 ? "Free" : $"Cost: {cost}") : "";
                costText.color = cost <= 0 ? new Color(0.8f, 1f, 0.8f, 1f) : costText.color;
            }
            if (openButton)
            {
                openButton.interactable = pack != null;
                openButton.onClick.RemoveAllListeners();
                if (pack != null)
                    openButton.onClick.AddListener(OnClickBuy);
            }
            RefreshAffordability(overrideCost);
        }

        private void OnEnable()
        {
            PlayerProfile.OnSoftCurrencyChanged += HandleCurrencyChanged;
            RefreshAffordability();
        }

        private void OnDisable()
        {
            PlayerProfile.OnSoftCurrencyChanged -= HandleCurrencyChanged;
        }

        private void HandleCurrencyChanged(int newBalance)
        {
            RefreshAffordability();
        }

        private void RefreshAffordability(int overrideCost = -1)
        {
            if (_pack == null || openButton == null) return;
            int balance = PlayerProfile.GetSoftCurrency();
            int cost = GetEffectiveCost(overrideCost);
            bool canBuy = balance >= cost;
            openButton.interactable = canBuy;
            if (costText)
            {
                costText.text = cost <= 0 ? "Free" : $"Cost: {cost}";
                costText.color = canBuy ? new Color(0.8f, 1f, 0.8f, 1f) : new Color(1f, 0.6f, 0.6f, 1f);
            }
        }

        private void OnClickBuy()
        {
            if (_pack == null) return;
            int cost = GetEffectiveCost();
            if (cost > 0 && !PlayerProfile.TrySpendSoftCurrency(cost))
            {
                // optional: feedback
                RefreshAffordability();
                return;
            }
            _onOpen?.Invoke(_packId);
        }

        private int _overrideCostCache = -1;
        private int GetEffectiveCost(int overrideCost = -1)
        {
            if (overrideCost >= 0) { _overrideCostCache = overrideCost; return overrideCost; }
            if (_overrideCostCache >= 0) return _overrideCostCache;
            return _pack != null ? _pack.Cost : 0;
        }

        // External control helpers (for Shop to present daily countdown state etc.)
        public void SetButtonInteractable(bool value)
        {
            if (openButton) openButton.interactable = value;
        }

        public void SetCostLabel(string text, Color color)
        {
            if (costText)
            {
                costText.text = text;
                costText.color = color;
            }
        }

        public TMP_Text CostText => costText;

        // Expose bound pack id for external systems (e.g., tutorial targeting)
        public string PackId => _packId;

        // Expose the open/buy button for precise targeting
        public Button OpenButton => openButton;
    }
}
