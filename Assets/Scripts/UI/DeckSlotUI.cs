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
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private RectTransform emptyUI;

        private string _cardId;
        private System.Action<string> _onRemove;

        private void Awake()
        {
            EnsureLabels();
        }

        public void Bind(CardDefinition card, System.Action<string> onRemove)
        {
            _cardId = card != null ? card.CardId : null;
            _onRemove = onRemove;

            bool hasCard = card != null;

            if (icon) { icon.sprite = card?.Icon; icon.gameObject.SetActive(hasCard); }
            if (rarityStripe) rarityStripe.gameObject.SetActive(hasCard);
            if (levelText) levelText.gameObject.SetActive(hasCard);
            if (costText) costText.gameObject.SetActive(hasCard);
            if (removeButton) removeButton.gameObject.SetActive(hasCard);

            if (emptyUI) emptyUI.gameObject.SetActive(!hasCard);

            if (nameText)
            {
                if (hasCard)
                {
                    nameText.gameObject.SetActive(true);
                    nameText.text = card.DisplayName;
                }
                else
                {
                    nameText.text = "(Empty)";
                    nameText.gameObject.SetActive(emptyUI == null);
                }
            }

            if (hasCard)
            {
                if (rarityStripe && card.Rarity != null) rarityStripe.color = card.Rarity.Color;
                if (levelText) levelText.text = $"Lv {GetCardLevel(card)}";
                if (costText) costText.text = $"Cost: {GetCardCost(card)}";
            }

            if (removeButton)
            {
                removeButton.interactable = hasCard;
                removeButton.onClick.RemoveAllListeners();
                if (hasCard)
                    removeButton.onClick.AddListener(() => _onRemove?.Invoke(_cardId));
            }
        }

        private int GetCardLevel(CardDefinition card)
        {
            var progress = PlayerProfile.GetOrCreateCard(card.CardId);
            return Mathf.Max(1, progress != null ? progress.level : 1);
        }

        private int GetCardCost(CardDefinition card)
        {
            int level = GetCardLevel(card);
            return card.GetStatsForLevel(level).cost;
        }

        private void EnsureLabels()
        {
            if (levelText == null)
                levelText = CreateLabel("LevelText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(5f, -5f), new Vector2(55f, 25f), "Lv 1");
            if (costText == null)
                costText = CreateLabel("CostText", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-5f, 5f), new Vector2(55f, 25f), "0");
        }

        private TMP_Text CreateLabel(string labelName, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, string defaultText)
        {
            Transform parent = transform.Find("DeckSlot") ?? transform;
            GameObject go = new GameObject(labelName, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            go.transform.SetParent(parent, false);

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            go.AddComponent<CanvasRenderer>();
            var text = go.AddComponent<TextMeshProUGUI>();

            if (nameText != null)
            {
                text.font = nameText.font;
                text.fontSharedMaterial = nameText.fontSharedMaterial;
            }
            text.fontSize = 20f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 24f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = defaultText;

            go.transform.SetAsLastSibling();
            return text;
        }

        public void Clear()
        {
            Bind(null, _onRemove);
        }
    }
}
