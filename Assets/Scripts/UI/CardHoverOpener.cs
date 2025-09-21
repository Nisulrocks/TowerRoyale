using UnityEngine;
using UnityEngine.EventSystems;
using TR.Data;
using TR.Systems;

namespace TR.UI
{
    // Attach to a card tile or deck slot to show HoverCardDetailsUI on hover
    public class CardHoverOpener : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CardDefinition card;
        [SerializeField] private int level = 1;

        // Optional: small delay before hiding to prevent flicker when moving across items
        [SerializeField] private float hideDelay = 0.12f;
        private float _hideAt;
        private bool _over;

        public void Set(CardDefinition def, int lv)
        {
            card = def;
            level = Mathf.Max(1, lv);
        }

        private void Update()
        {
            if (!_over && _hideAt > 0f && Time.unscaledTime >= _hideAt)
            {
                _hideAt = 0f;
                HoverCardDetailsUI.Hide();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _over = true;
            _hideAt = 0f;
            if (card == null) return;
            HoverCardDetailsUI.Show(card, level);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _over = false;
            _hideAt = Time.unscaledTime + hideDelay;
        }
    }
}
