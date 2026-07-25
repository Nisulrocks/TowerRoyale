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
        [SerializeField] private TMP_Text costText; 
        [SerializeField] private Image packArtImage;
        [SerializeField] private Button openButton;

        [Header("Unlock")]
        [SerializeField] private GameObject lockedOverlayRoot;
        [SerializeField] private TMP_Text lockedDescriptionText;
        [SerializeField] private Button unlockButton;
        [SerializeField] private Color unlockCostColor = new Color(1f, 0.84f, 0f, 1f);

        [Header("Quantity")]
        [SerializeField] private GameObject quantityRoot;
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private int maxOpenQuantity = 5;

        private PackDefinition _pack;
        private string _packId;
        private System.Action<string, int> _onOpen;
        private int _selectedQuantity = 1;
        private int _maxQuantity = 1;
        private string _buttonLabelOverride;

        public void Bind(PackDefinition pack, System.Action<string, int> onOpen, int overrideCost = -1, int maxQuantity = 1, int defaultQuantity = 1)
        {
            _pack = pack;
            _packId = pack != null ? pack.PackId : null;
            _onOpen = onOpen;
            _maxQuantity = maxQuantity;
            _selectedQuantity = Mathf.Clamp(defaultQuantity, 1, Mathf.Max(1, _maxQuantity));
            _overrideCostCache = -1;

            if (nameText) nameText.text = pack != null ? pack.DisplayName : "(null)";
            if (descText) descText.text = pack != null ? $"Cards: {pack.CardsPerPack}" : "";
            if (packArtImage && pack != null)
            {
                Sprite art = pack.ShopPackArt;
                if (art != null) packArtImage.sprite = art;
            }

            if (minusButton)
            {
                minusButton.onClick.RemoveAllListeners();
                minusButton.onClick.AddListener(DecreaseQuantity);
            }
            if (plusButton)
            {
                plusButton.onClick.RemoveAllListeners();
                plusButton.onClick.AddListener(IncreaseQuantity);
            }
            if (openButton)
            {
                openButton.onClick.RemoveAllListeners();
                if (pack != null)
                    openButton.onClick.AddListener(OnClickBuy);
            }

            _ = GetEffectiveCost(overrideCost);
            RefreshLockState();
        }

        private void OnEnable()
        {
            PlayerProfile.OnSoftCurrencyChanged += HandleCurrencyChanged;
            PlayerProfile.OnTrophiesChanged += HandleTrophiesChanged;
            RefreshLockState();
        }

        private void OnDisable()
        {
            PlayerProfile.OnSoftCurrencyChanged -= HandleCurrencyChanged;
            PlayerProfile.OnTrophiesChanged -= HandleTrophiesChanged;
        }

        private void HandleCurrencyChanged(int newBalance)
        {
            RefreshAffordability();
        }

        private void HandleTrophiesChanged(int newTrophies)
        {
            RefreshLockState();
        }

        private void RefreshLockState()
        {
            if (_pack == null) return;

            bool arenaUnlocked = _pack.IsUnlockedForPlayer();
            bool fullyUnlocked = _pack.IsFullyUnlockedForPlayer();
            bool requiresUnlock = _pack.UnlockArena != null || _pack.UnlockCost > 0;
            bool showPackInfo = !requiresUnlock || fullyUnlocked;

            if (lockedOverlayRoot)
            {
                lockedOverlayRoot.SetActive(requiresUnlock && !fullyUnlocked);
            }

            if (descText) descText.gameObject.SetActive(showPackInfo);
            if (costText) costText.gameObject.SetActive(showPackInfo && fullyUnlocked);
            if (openButton) openButton.gameObject.SetActive(showPackInfo && fullyUnlocked);

            if (lockedDescriptionText)
            {
                if (requiresUnlock && !fullyUnlocked)
                {
                    if (!arenaUnlocked)
                    {
                        string arenaName = _pack.UnlockArena != null ? _pack.UnlockArena.DisplayName : "Arena";
                        lockedDescriptionText.text = $"LOCKED\nunlock at {arenaName} ({_pack.RequiredTrophies} trophies)";
                    }
                    else
                    {
                        string hex = ColorUtility.ToHtmlStringRGBA(unlockCostColor);
                        lockedDescriptionText.text = $"Purchase this Pack for <color=#{hex}>{_pack.UnlockCost}</color>";
                    }
                }
                else
                {
                    lockedDescriptionText.text = string.Empty;
                }
            }

            if (unlockButton)
            {
                bool showUnlock = requiresUnlock && !fullyUnlocked;
                unlockButton.gameObject.SetActive(showUnlock);
                unlockButton.onClick.RemoveAllListeners();
                if (showUnlock)
                    unlockButton.onClick.AddListener(OnClickUnlock);
            }

            if (requiresUnlock)
            {
                _buttonLabelOverride = fullyUnlocked ? null : "Locked";
            }

            RefreshAffordability();
        }

        private void RefreshAffordability(int overrideCost = -1)
        {
            if (_pack == null || openButton == null) return;

            bool fullyUnlocked = _pack.IsFullyUnlockedForPlayer();
            bool arenaUnlocked = _pack.IsUnlockedForPlayer();
            int balance = PlayerProfile.GetSoftCurrency();

            if (unlockButton)
            {
                unlockButton.interactable = arenaUnlocked && !fullyUnlocked && balance >= _pack.UnlockCost;
            }

            if (!fullyUnlocked)
            {
                openButton.interactable = false;
                if (costText) costText.gameObject.SetActive(false);
            }
            else
            {
                int cost = GetEffectiveCost(overrideCost);
                int max = ComputeMaxQuantity(cost);
                _selectedQuantity = Mathf.Clamp(_selectedQuantity, 1, max);
                int totalCost = cost * _selectedQuantity;
                bool canBuy = balance >= totalCost && _selectedQuantity > 0;
                openButton.interactable = canBuy;
                if (costText)
                {
                    costText.gameObject.SetActive(true);
                    costText.text = cost <= 0 ? "Free" : $"Cost: {totalCost}";
                    costText.color = canBuy ? new Color(0.8f, 1f, 0.8f, 1f) : new Color(1f, 0.6f, 0.6f, 1f);
                }
            }

            RefreshQuantityUI();
        }

        private void DecreaseQuantity()
        {
            if (_pack == null || !_pack.IsFullyUnlockedForPlayer()) return;
            if (_selectedQuantity > 1)
            {
                _selectedQuantity--;
                RefreshQuantityUI();
                RefreshAffordability();
            }
        }

        private void IncreaseQuantity()
        {
            if (_pack == null || !_pack.IsFullyUnlockedForPlayer()) return;
            int max = ComputeMaxQuantity(GetEffectiveCost());
            if (_selectedQuantity < max)
            {
                _selectedQuantity++;
                RefreshQuantityUI();
                RefreshAffordability();
            }
        }

        private void RefreshQuantityUI()
        {
            bool fullyUnlocked = _pack != null && _pack.IsFullyUnlockedForPlayer();
            int max = ComputeMaxQuantity(GetEffectiveCost());
            if (quantityText != null) quantityText.text = _selectedQuantity.ToString();
            if (minusButton != null) minusButton.interactable = fullyUnlocked && _selectedQuantity > 1;
            if (plusButton != null) plusButton.interactable = fullyUnlocked && _selectedQuantity < max;
            if (quantityRoot != null) quantityRoot.SetActive(fullyUnlocked && max > 1);
            RefreshOpenButtonLabel();
        }

        private void RefreshOpenButtonLabel()
        {
            if (openButton == null) return;
            var btnText = openButton.GetComponentInChildren<TMP_Text>();
            if (btnText == null) return;
            if (!string.IsNullOrEmpty(_buttonLabelOverride))
            {
                btnText.text = _buttonLabelOverride;
                return;
            }
            int max = ComputeMaxQuantity(GetEffectiveCost());
            btnText.text = (max > 1) ? $"Open {_selectedQuantity}" : "Open";
        }

        private int ComputeMaxQuantity(int cost)
        {
            int cap = _maxQuantity > 0 ? _maxQuantity : int.MaxValue;
            if (cost > 0)
                cap = Mathf.Min(cap, Mathf.Max(0, PlayerProfile.GetSoftCurrency() / cost));
            cap = Mathf.Min(cap, Mathf.Max(1, maxOpenQuantity));
            return Mathf.Max(1, cap);
        }

        private void OnClickBuy()
        {
            if (_pack == null || !_pack.IsFullyUnlockedForPlayer()) return;
            int cost = GetEffectiveCost();
            int totalCost = cost * _selectedQuantity;
            if (totalCost > 0 && !PlayerProfile.TrySpendSoftCurrency(totalCost))
            {
                RefreshAffordability();
                return;
            }
            _onOpen?.Invoke(_packId, _selectedQuantity);
        }

        private void OnClickUnlock()
        {
            if (_pack == null) return;
            if (_pack.IsFullyUnlockedForPlayer()) return;
            if (!_pack.IsUnlockedForPlayer()) return;

            int cost = _pack.UnlockCost;
            if (cost > 0 && !PlayerProfile.TrySpendSoftCurrency(cost))
            {
                RefreshAffordability();
                return;
            }

            PlayerProfile.UnlockPack(_pack.PackId);
            RefreshLockState();
        }

        private int _overrideCostCache = -1;
        private int GetEffectiveCost(int overrideCost = -1)
        {
            if (overrideCost >= 0) { _overrideCostCache = overrideCost; return overrideCost; }
            if (_overrideCostCache >= 0) return _overrideCostCache;
            return _pack != null ? _pack.Cost : 0;
        }

        
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

        public void SetButtonLabel(string text)
        {
            _buttonLabelOverride = text;
            RefreshOpenButtonLabel();
        }

        public TMP_Text CostText => costText;

        
        public string PackId => _packId;

        
        public Button OpenButton => openButton;
    }
}
