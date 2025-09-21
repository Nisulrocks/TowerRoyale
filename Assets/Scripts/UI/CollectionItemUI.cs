using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data;
using TR.Systems;

namespace TR.UI
{
    // Composite UI element: shows a card and exposes an Upgrade button.
    public class CollectionItemUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private CardItemUI cardItem;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TMP_Text upgradeButtonText;
        [SerializeField] private TMP_Text upgradeCostText; // new: shows cost separately
        [Header("Arena Lock Overlay")]
        [Tooltip("Root GameObject for the arena lock overlay (e.g., a panel with text/icon)")]
        [SerializeField] private GameObject arenaLockOverlayRoot;
        [Tooltip("Text element inside the arena lock overlay to show unlock info")]
        [SerializeField] private TMP_Text arenaLockOverlayText;

        private CardDefinition _card;
        private int _neededPoints;
        private int _cost;

        public void Bind(CardDefinition card)
        {
            _card = card;
            var cp = PlayerProfile.GetOrCreateCard(card.CardId);
            bool discovered = cp.ownedCount > 0;
            int level = discovered ? Mathf.Max(1, cp.level) : 0;
            cardItem?.Bind(card, level);
            if (!discovered)
            {
                // Dim visuals and disable upgrade UI for undiscovered cards
                cardItem?.SetDimmed(true);
                // Always keep pointsText for undiscovered as a simple label
                if (pointsText) pointsText.text = "Undiscovered";

                // Show or hide the dedicated arena-lock overlay
                bool gated = _card != null && _card.UnlockArena != null && !_card.IsUnlockedForPlayer();
                if (arenaLockOverlayRoot)
                {
                    arenaLockOverlayRoot.SetActive(gated);
                    if (gated && arenaLockOverlayText)
                    {
                        string arenaName = _card.UnlockArena != null ? _card.UnlockArena.DisplayName : "Arena";
                        int req = _card.RequiredTrophies;
                        arenaLockOverlayText.text = $"Unlocks at {arenaName} ({req} trophies)";
                    }
                }
                if (upgradeButton)
                {
                    upgradeButton.interactable = false;
                    upgradeButton.onClick.RemoveAllListeners();
                    upgradeButton.gameObject.SetActive(false);
                }
                if (upgradeButtonText) upgradeButtonText.text = "Upgrade";
                if (upgradeCostText) upgradeCostText.text = string.Empty;
                return;
            }

            // Hide arena lock overlay for discovered cards
            if (arenaLockOverlayRoot) arenaLockOverlayRoot.SetActive(false);

            // Points status
            var rarity = card.Rarity;
            int nextLevel = Mathf.Min(level + 1, rarity != null ? rarity.MaxLevel : level);
            int needed = rarity != null && nextLevel > level ? rarity.GetPointsRequiredForLevel(nextLevel) : 0;
            int cost = rarity != null && nextLevel > level ? rarity.GetUpgradeCostForLevel(nextLevel) : 0;
            _neededPoints = needed; _cost = cost;
            if (pointsText)
            {
                if (nextLevel > level)
                    pointsText.text = $"Pts {cp.points}/{needed}";
                else
                    pointsText.text = "Max";
            }

            bool canUpgrade = rarity != null && nextLevel > level && cp.points >= needed && PlayerProfile.GetSoftCurrency() >= cost;
            if (upgradeButton)
            {
                upgradeButton.gameObject.SetActive(true);
                upgradeButton.interactable = canUpgrade;
                upgradeButton.onClick.RemoveAllListeners();
                upgradeButton.onClick.AddListener(OnUpgrade);
            }
            if (upgradeButtonText)
            {
                upgradeButtonText.text = nextLevel > level ? "Upgrade" : "Maxed";
            }
            if (upgradeCostText)
            {
                upgradeCostText.text = nextLevel > level ? $"Cost: {cost}" : string.Empty;
            }
        }

        private void OnUpgrade()
        {
            if (_card == null) return;
            var cp = PlayerProfile.GetOrCreateCard(_card.CardId);
            int before = cp.level;
            if (CollectionService.TryPurchaseUpgrade(_card, cp))
            {
                PlayerProfile.Save();
                Bind(_card); // refresh visuals
            }
        }

        public CardDefinition Card => _card;

        private void OnEnable()
        {
            PlayerProfile.OnSoftCurrencyChanged += HandleCurrencyChanged;
        }

        private void OnDisable()
        {
            PlayerProfile.OnSoftCurrencyChanged -= HandleCurrencyChanged;
        }

        private void HandleCurrencyChanged(int newBalance)
        {
            // Recompute interactability quickly without full bind
            if (_card == null || upgradeButton == null || upgradeButtonText == null) return;
            var cp = PlayerProfile.GetOrCreateCard(_card.CardId);
            bool discovered = cp.ownedCount > 0;
            if (!discovered)
            {
                upgradeButton.interactable = false;
                return;
            }
            int level = Mathf.Max(1, cp.level);
            var rarity = _card.Rarity;
            if (rarity == null) { upgradeButton.interactable = false; return; }
            int nextLevel = Mathf.Min(level + 1, rarity.MaxLevel);
            if (nextLevel <= level) { upgradeButton.interactable = false; return; }
            int needed = rarity.GetPointsRequiredForLevel(nextLevel);
            int cost = rarity.GetUpgradeCostForLevel(nextLevel);
            bool can = (cp.points >= needed) && (newBalance >= cost);
            upgradeButton.interactable = can;
            upgradeButtonText.text = "Upgrade";
            if (upgradeCostText) upgradeCostText.text = $"Cost: {cost}";
        }
    }
}
