using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using TR.Data;

namespace TR.UI
{
    
    public class ShopCardPointsItemUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image rarityStripe;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Button buyButton;
        [SerializeField] private GameObject soldBadge;

        [SerializeField] private CanvasGroup rootGroup;

        private System.Action _onClick;
        private Coroutine _flashCo;
        private Color _baseCostColor = Color.white;
        private Vector3 _baseButtonScale = Vector3.one;

        private void Awake()
        {
            if (costText != null) _baseCostColor = costText.color;
            if (buyButton != null) _baseButtonScale = buyButton.transform.localScale;
        }

        private void OnDisable()
        {
            StopFlash();
            RestoreVisualState();
        }

        public void Bind(CardDefinition card, int points, int cost, bool sold, System.Action onClick)
        {
            _onClick = onClick;
            if (icon) icon.sprite = card != null ? card.Icon : null;
            if (nameText) nameText.text = card != null ? card.DisplayName : "-";
            if (rarityStripe && card != null && card.Rarity != null) rarityStripe.color = card.Rarity.Color;
            if (pointsText) pointsText.text = $"+{points} pts";
            if (costText) costText.text = $"Cost: {cost}";
            if (costText) _baseCostColor = costText.color; 

            if (soldBadge) soldBadge.SetActive(sold);
            ApplySoldVisuals(sold);
            if (buyButton)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.interactable = !sold;
                if (!sold && _onClick != null) buyButton.onClick.AddListener(() => _onClick());
                _baseButtonScale = buyButton.transform.localScale; 
            }
        }

        private void ApplySoldVisuals(bool sold)
        {
            
            if (rootGroup != null)
            {
                rootGroup.alpha = sold ? 0.55f : 1f;
                rootGroup.interactable = !sold; 
                rootGroup.blocksRaycasts = !sold;
            }
            
            var dim = sold ? 0.5f : 1f;
            if (icon != null)
            {
                var c = icon.color; c.a = Mathf.Clamp01(dim); icon.color = c;
            }
            if (nameText != null)
            {
                nameText.color = new Color(nameText.color.r * dim, nameText.color.g * dim, nameText.color.b * dim, nameText.color.a);
            }
            if (pointsText != null)
            {
                pointsText.color = new Color(pointsText.color.r * dim, pointsText.color.g * dim, pointsText.color.b * dim, pointsText.color.a);
            }
            if (costText != null)
            {
                costText.color = new Color(costText.color.r * dim, costText.color.g * dim, costText.color.b * dim, costText.color.a);
            }
        }

        
        public void FlashInsufficientFunds()
        {
            if (!isActiveAndEnabled) return;
            StopFlash();
            _flashCo = StartCoroutine(FlashCo());
        }

        private IEnumerator FlashCo()
        {
            const float dur = 0.35f;
            float t = 0f;
            var origCost = _baseCostColor;
            var pulseColor = new Color(1f, 0.3f, 0.3f, origCost.a);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float ping = Mathf.PingPong(u * 2f, 1f); 
                if (costText) costText.color = Color.Lerp(origCost, pulseColor, ping);
                if (buyButton) buyButton.transform.localScale = _baseButtonScale * (1f + 0.06f * ping);
                yield return null;
            }
            RestoreVisualState();
            _flashCo = null;
        }

        private void StopFlash()
        {
            if (_flashCo != null)
            {
                StopCoroutine(_flashCo);
                _flashCo = null;
            }
        }

        private void RestoreVisualState()
        {
            if (costText) costText.color = _baseCostColor;
            if (buyButton) buyButton.transform.localScale = _baseButtonScale;
        }
    }
}
