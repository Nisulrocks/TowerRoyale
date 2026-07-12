using System.Collections.Generic;
using UnityEngine;
using TMPro;
using TR.Systems;
using TR.Data;

namespace TR.Battle
{
    
    public class PartnerDeckBarUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform deckRoot;             
        [SerializeField] private TR.UI.CardItemUI cardItemPrefab; 
        [Tooltip("Optional label (e.g. \"Partner's Deck\") shown/hidden along with the partner deck.")]
        [SerializeField] private TMP_Text label;

        [Header("Ghost Appearance")]
        [Tooltip("Uniform scale applied to the mirrored partner deck (smaller than the local deck).")]
        [SerializeField] private float scale = 0.6f;
        [Tooltip("Overall transparency of the mirrored partner deck (0 = invisible, 1 = opaque).")]
        [Range(0f, 1f)]
        [SerializeField] private float alpha = 1;

        private readonly List<TR.UI.CardItemUI> _items = new();
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (deckRoot == null) deckRoot = transform;
            EnsureCanvasGroup();
            
            SetVisible(false);
        }

        private void EnsureCanvasGroup()
        {
            if (deckRoot == null) deckRoot = transform;
            _canvasGroup = deckRoot.GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = deckRoot.gameObject.AddComponent<CanvasGroup>();
            
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        
        
        public void SetVisible(bool visible)
        {
            EnsureCanvasGroup();
            _canvasGroup.alpha = visible ? alpha : 0f;
            if (label != null) label.gameObject.SetActive(visible);
        }

        
        public void BindFromPartnerDeck(string[] cardIds, int[] levels)
        {
            if (cardItemPrefab == null)
            {
                Debug.LogWarning("[PartnerDeckBar] No cardItemPrefab assigned; cannot show partner deck.");
                return;
            }
            if (deckRoot == null) deckRoot = transform;
            GameDB.EnsureLoaded();

            
            if (!deckRoot.gameObject.activeSelf) deckRoot.gameObject.SetActive(true);

            
            foreach (var it in _items) if (it) Destroy(it.gameObject);
            _items.Clear();

            if (cardIds == null)
            {
                Debug.LogWarning("[PartnerDeckBar] Received null partner deck.");
                return;
            }

            int added = 0;
            for (int i = 0; i < cardIds.Length; i++)
            {
                var card = GameDB.GetCardById(cardIds[i]);
                if (card == null) continue;
                int level = (levels != null && i < levels.Length) ? Mathf.Max(1, levels[i]) : 1;

                var ui = Instantiate(cardItemPrefab, deckRoot);
                ui.gameObject.SetActive(true);
                ui.Bind(card, level);
                
                ui.transform.localScale = Vector3.one * scale;
                
                ui.SetDimmed(true);
                _items.Add(ui);
                added++;
            }

            Debug.Log($"[PartnerDeckBar] Bound partner deck: {added}/{cardIds.Length} cards under '{deckRoot.name}'.");
            SetVisible(true);
        }
    }
}
