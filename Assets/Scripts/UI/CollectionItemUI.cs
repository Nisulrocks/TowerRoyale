using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data;
using TR.Systems;

namespace TR.UI
{
    
    public class CollectionItemUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private CardItemUI cardItem;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TMP_Text upgradeButtonText;
        [SerializeField] private TMP_Text upgradeCostText;
        [Header("Upgrade Dot")]
        [SerializeField] private Image upgradeDot;
        [SerializeField] private TMP_Text upgradeDotText;
        [Tooltip("Optional transform to parent the dot to. If null, the dot is parented to this object.")]
        [SerializeField] private RectTransform upgradeDotParent;
        [SerializeField] private Color upgradeDotColor = new Color(1f, 0.92f, 0.016f, 1f);
        [SerializeField] private Color upgradeDotTextColor = Color.black;
        [SerializeField] private float upgradeDotFontSize = 14f;
        [SerializeField] private Vector2 upgradeDotOffset = new Vector2(-8f, -8f);
        [SerializeField] private Vector2 upgradeDotSize = new Vector2(20f, 20f);
        [Header("Arena Lock Overlay")]
        [Tooltip("Root GameObject for the arena lock overlay (e.g., a panel with text/icon)")]
        [SerializeField] private GameObject arenaLockOverlayRoot;

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
                
                cardItem?.SetDimmed(true);
                
                if (pointsText) pointsText.text = "Undiscovered";

                
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
                ShowUpgradeDot(false);
                return;
            }

            
            if (arenaLockOverlayRoot) arenaLockOverlayRoot.SetActive(false);

            
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
            ShowUpgradeDot(canUpgrade);

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
                Bind(_card); 
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
            
            if (_card == null || upgradeButton == null || upgradeButtonText == null) return;
            var cp = PlayerProfile.GetOrCreateCard(_card.CardId);
            bool discovered = cp.ownedCount > 0;
            if (!discovered)
            {
                upgradeButton.interactable = false;
                ShowUpgradeDot(false);
                return;
            }
            int level = Mathf.Max(1, cp.level);
            var rarity = _card.Rarity;
            if (rarity == null) { upgradeButton.interactable = false; ShowUpgradeDot(false); return; }
            int nextLevel = Mathf.Min(level + 1, rarity.MaxLevel);
            if (nextLevel <= level) { upgradeButton.interactable = false; ShowUpgradeDot(false); return; }
            int needed = rarity.GetPointsRequiredForLevel(nextLevel);
            int cost = rarity.GetUpgradeCostForLevel(nextLevel);
            bool can = (cp.points >= needed) && (newBalance >= cost);
            upgradeButton.interactable = can;
            upgradeButtonText.text = "Upgrade";
            if (upgradeCostText) upgradeCostText.text = $"Cost: {cost}";
            ShowUpgradeDot(can);
        }

        private void ShowUpgradeDot(bool available)
        {
            if (!available)
            {
                if (upgradeDot) upgradeDot.gameObject.SetActive(false);
                if (upgradeDotText) upgradeDotText.gameObject.SetActive(false);
                return;
            }
            EnsureUpgradeDot();
            if (upgradeDot) upgradeDot.gameObject.SetActive(true);
            if (upgradeDotText) upgradeDotText.gameObject.SetActive(true);
        }

        private void EnsureUpgradeDot()
        {
            if (upgradeDot != null) return;

            var go = new GameObject("UpgradeDot", typeof(RectTransform), typeof(Image));
            var parent = upgradeDotParent != null ? upgradeDotParent.transform : transform;
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.one;
            rt.anchorMax = Vector2.one;
            rt.pivot = Vector2.one;
            rt.anchoredPosition = upgradeDotOffset;
            rt.sizeDelta = upgradeDotSize;

            var img = go.GetComponent<Image>();
            img.color = upgradeDotColor;
            img.raycastTarget = false;
            img.sprite = CollectionUpgradeBadge.CreateCircleSprite(Color.white);

            upgradeDot = img;

            if (upgradeDotText == null)
            {
                var textGO = new GameObject("UpgradeDotText", typeof(RectTransform));
                textGO.transform.SetParent(go.transform, false);

                var textRT = textGO.GetComponent<RectTransform>();
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.offsetMin = Vector2.zero;
                textRT.offsetMax = Vector2.zero;

                var text = textGO.AddComponent<TextMeshProUGUI>();
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = upgradeDotFontSize;
                text.color = upgradeDotTextColor;
                text.raycastTarget = false;
                text.text = "!";

                upgradeDotText = text;
            }
        }
    }
}
