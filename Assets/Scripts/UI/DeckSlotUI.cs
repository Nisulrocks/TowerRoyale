using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Data;
using TR.Systems;

namespace TR.UI
{
    public class DeckSlotUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image rarityStripe;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button removeButton;

        private string _cardId;
        private System.Action<string> _onRemove;

        public void Bind(CardDefinition card, System.Action<string> onRemove)
        {
            _cardId = card != null ? card.CardId : null;
            _onRemove = onRemove;
            if (icon) icon.sprite = card?.Icon;
            if (rarityStripe && card?.Rarity != null) rarityStripe.color = card.Rarity.Color;
            if (nameText) nameText.text = card != null ? card.DisplayName : "(Empty)";

            if (removeButton)
            {
                removeButton.interactable = card != null;
                removeButton.onClick.RemoveAllListeners();
                if (card != null)
                    removeButton.onClick.AddListener(() => _onRemove?.Invoke(_cardId));
            }
        }

        public void Clear()
        {
            Bind(null, _onRemove);
        }
    }
}
