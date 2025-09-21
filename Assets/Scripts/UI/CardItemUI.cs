using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using TR.Data;
using TR.Systems;
using TR.Battle;

namespace TR.UI
{
    // Simple card tile for UI lists. Hook these references in a prefab.
    public class CardItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Refs")]
        [SerializeField] private Image icon;
        [SerializeField] private Image rarityStripe;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text costText; // only visible in battle scenes

        private string _cardId;
        private CardDefinition _def;
        private int _level;

        public void Bind(CardDefinition card, int level = 0)
        {
            _def = card;
            _level = level; // allow 0 for undiscovered
            _cardId = card != null ? card.CardId : null;
            if (icon) icon.sprite = card?.Icon;
            if (rarityStripe && card?.Rarity != null) rarityStripe.color = card.Rarity.Color;
            if (nameText) nameText.text = card != null ? card.DisplayName : "(null)";
            if (levelText) levelText.text = level > 0 ? $"Lv {level}" : string.Empty;

            // Show cost only during battle scenes
            bool inBattle = FindFirstObjectByType<BattleSceneController>(FindObjectsInactive.Include) != null;
            if (costText)
            {
                if (inBattle && card != null)
                {
                    int lvl = Mathf.Max(1, level);
                    costText.text = $"Cost: {card.GetStatsForLevel(lvl).cost}";
                    costText.gameObject.SetActive(true);
                }
                else
                {
                    costText.gameObject.SetActive(false);
                }
            }
        }

        public string CardId => _cardId;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_def == null)
            {
                // Try resolve from DB if only id present
                if (!string.IsNullOrEmpty(_cardId)) _def = GameDB.GetCardById(_cardId);
            }
            if (_def != null)
            {
                HoverCardDetailsUI.Show(_def, _level);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverCardDetailsUI.Hide();
        }

        // Grey-out visuals for undiscovered cards while keeping layout
        public void SetDimmed(bool dim)
        {
            float a = dim ? 0.45f : 1f;
            if (icon)
            {
                var c = icon.color; c.a = a; icon.color = c;
            }
            if (rarityStripe)
            {
                var c = rarityStripe.color; c.a = a; rarityStripe.color = c;
            }
            if (nameText)
            {
                var c = nameText.color; c.a = a; nameText.color = c;
            }
            if (levelText)
            {
                var c = levelText.color; c.a = a; levelText.color = c;
            }
            if (costText)
            {
                var c = costText.color; c.a = a; costText.color = c;
            }
        }
    }
}
