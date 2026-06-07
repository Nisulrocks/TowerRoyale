using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Data;

namespace TR.UI
{
    
    public class CollectionUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private CollectionItemUI itemPrefab;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text softCurrencyText; 
        [Header("Sorting")]
        [Tooltip("Optional dropdown to control rarity order. If not assigned, defaults to ascending (Common -> Legendary).")]
        [SerializeField] private TMP_Dropdown raritySortDropdown;
        [Tooltip("If true, rarity order is reversed (Legendary -> Common)")]
        [SerializeField] private bool rarityDescending = false;
        [Header("Rarity Order Override")]
        [Tooltip("Optional explicit rarity order from lowest to highest (e.g., Common, Rare, Epic, Legendary). Leave empty to use GameDB order.")]
        [SerializeField] private List<RarityDefinition> rarityOrderOverride;
        [Header("Search")]
        [Tooltip("Optional input field to filter cards by name (case-insensitive)")]
        [SerializeField] private TMP_InputField searchInput;
        [Tooltip("Debounce time before applying search filter (seconds)")]
        [SerializeField] private float searchDebounceSeconds = 0.08f;
        [Tooltip("When typing continuously (e.g., holding backspace), apply updates at most this often (seconds)")]
        [SerializeField] private float searchThrottleSeconds = 0.10f;

        private readonly List<CollectionItemUI> _items = new();
        
        private readonly System.Collections.Generic.Dictionary<CollectionItemUI, Coroutine> _moveCoByItem = new();
        private readonly System.Collections.Generic.Dictionary<CollectionItemUI, Coroutine> _appearCoByItem = new();

        private void Awake()
        {
            
            GameDB.EnsureLoaded();
        }

        private void OnEnable()
        {
            HoverCardDetailsUI.SetCollectionContext(true);
            Refresh();
            PlayerProfile.OnSoftCurrencyChanged += HandleSoftCurrencyChanged;
            SetupSortingUI();
            if (raritySortDropdown != null) raritySortDropdown.onValueChanged.AddListener(HandleRaritySortChanged);
            if (searchInput != null) searchInput.onValueChanged.AddListener(HandleSearchChanged);
        }

        private void OnDisable()
        {
            HoverCardDetailsUI.SetCollectionContext(false);
            PlayerProfile.OnSoftCurrencyChanged -= HandleSoftCurrencyChanged;
            if (raritySortDropdown != null) raritySortDropdown.onValueChanged.RemoveListener(HandleRaritySortChanged);
            if (searchInput != null) searchInput.onValueChanged.RemoveListener(HandleSearchChanged);
            CancelSearchDebounce();
        }

        private void HandleSoftCurrencyChanged(int newBalance)
        {
            if (softCurrencyText)
            {
                softCurrencyText.text = $"Coins: {newBalance}";
            }
        }

        public void Refresh()
        {
            if (headerText) headerText.text = "Collection";
            if (softCurrencyText) softCurrencyText.text = $"Coins: {PlayerProfile.GetSoftCurrency()}";

            GameDB.EnsureLoaded();
            bool searching = !string.IsNullOrWhiteSpace(_searchQuery);
            
            StopAllItemAnimations();
            
            
            System.Collections.Generic.Dictionary<RarityDefinition, int> rarityPriorityOverride = null;
            if (rarityOrderOverride != null && rarityOrderOverride.Count > 0)
            {
                rarityPriorityOverride = new System.Collections.Generic.Dictionary<RarityDefinition, int>(rarityOrderOverride.Count);
                for (int i = 0; i < rarityOrderOverride.Count; i++)
                {
                    var rr = rarityOrderOverride[i];
                    if (rr != null && !rarityPriorityOverride.ContainsKey(rr)) rarityPriorityOverride[rr] = i;
                }
            }

            int GetRarityPriority(RarityDefinition r)
            {
                if (r == null) return int.MaxValue;
                if (rarityPriorityOverride != null && rarityPriorityOverride.TryGetValue(r, out var idx))
                    return idx;
                var rs = GameDB.Rarities;
                for (int i = 0; i < rs.Count; i++) if (rs[i] == r) return i;
                return int.MaxValue - 1;
            }

            
            
            
            
            
            var sorted = new List<CardDefinition>(GameDB.Cards);
            
            if (searching)
            {
                string q = _searchQuery.Trim();
                sorted.RemoveAll(card => card == null || string.IsNullOrEmpty(card.DisplayName) ||
                    card.DisplayName.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0);
            }
            sorted.Sort((a, b) =>
            {
                var cpa = PlayerProfile.GetOrCreateCard(a.CardId);
                var cpb = PlayerProfile.GetOrCreateCard(b.CardId);
                bool aOwned = cpa.ownedCount > 0;
                bool bOwned = cpb.ownedCount > 0;
                if (aOwned != bOwned) return bOwned.CompareTo(aOwned); 

                bool aUnlockNow = a.IsUnlockedForPlayer();
                bool bUnlockNow = b.IsUnlockedForPlayer();
                
                int aSection = aOwned ? 0 : (aUnlockNow ? 1 : 2);
                int bSection = bOwned ? 0 : (bUnlockNow ? 1 : 2);
                if (aSection != bSection) return aSection.CompareTo(bSection);

                int ar = GetRarityPriority(a.Rarity);
                int br = GetRarityPriority(b.Rarity);
                if (ar != br)
                {
                    return rarityDescending ? br.CompareTo(ar) : ar.CompareTo(br);
                }

                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
            });

            
            bool firstBuild = _items.Count == 0;
            var existingById = new Dictionary<string, CollectionItemUI>();
            foreach (var it in _items)
            {
                if (it != null && it.Card != null && !string.IsNullOrEmpty(it.Card.CardId))
                    existingById[it.Card.CardId] = it;
            }

            var ordered = new List<CollectionItemUI>(sorted.Count);
            var newlyCreated = new HashSet<CollectionItemUI>();
            var seenIds = new HashSet<string>();
            foreach (var card in sorted)
            {
                if (card == null || string.IsNullOrEmpty(card.CardId)) continue;
                if (!seenIds.Add(card.CardId)) continue; 
                if (!existingById.TryGetValue(card.CardId, out var ui) || ui == null)
                {
                    ui = Instantiate(itemPrefab, listRoot);
                    ui.Bind(card);
                    _items.Add(ui);
                    newlyCreated.Add(ui);
                }
                else
                {
                    
                    ui.Bind(card);
                }
                ui.gameObject.SetActive(true);
                ordered.Add(ui);
            }

            
            if (searching)
            {
                var keep = new HashSet<CollectionItemUI>(ordered);
                foreach (var it in _items)
                {
                    if (it == null) continue;
                    if (!keep.Contains(it)) it.gameObject.SetActive(false);
                }
                for (int i = 0; i < ordered.Count; i++)
                {
                    ordered[i].transform.SetSiblingIndex(i);
                }
                Canvas.ForceUpdateCanvases();
                var rtS = listRoot as RectTransform;
                if (rtS != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rtS);
                return;
            }

            if (firstBuild)
            {
                
                foreach (var ui in ordered)
                {
                    ui.transform.SetSiblingIndex(ordered.IndexOf(ui));
                }
                Canvas.ForceUpdateCanvases();
                var rootRt0 = listRoot as RectTransform;
                if (rootRt0 != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt0);
                return;
            }

            
            var oldPos = new Dictionary<RectTransform, Vector3>(ordered.Count);
            foreach (var ui in ordered)
            {
                var rt = ui.transform as RectTransform;
                if (rt != null) oldPos[rt] = rt.position;
            }

            
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].transform.SetSiblingIndex(i);
            }

            
            Canvas.ForceUpdateCanvases();
            var rootRt = listRoot as RectTransform;
            if (rootRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);

            
            foreach (var ui in ordered)
            {
                var rt = ui.transform as RectTransform; if (rt == null) continue;
                var le = ui.GetComponent<LayoutElement>();
                if (le == null) le = ui.gameObject.AddComponent<LayoutElement>();
                Vector3 startPos;
                bool isNew = newlyCreated.Contains(ui);
                if (!oldPos.TryGetValue(rt, out startPos)) startPos = rt.position;
                Vector3 targetPos = rt.position;

                le.ignoreLayout = true;
                if (isNew)
                {
                    
                    rt.position = targetPos;
                    rt.localScale = Vector3.one * 0.88f;
                    StopItemAnimation(ui);
                    var co = StartCoroutine(AnimateNewAppearTracked(ui, rt, le));
                    _appearCoByItem[ui] = co;
                }
                else
                {
                    
                    rt.position = startPos;
                    StopItemAnimation(ui);
                    var co = StartCoroutine(AnimateMoveToTracked(ui, rt, targetPos, le));
                    _moveCoByItem[ui] = co;
                }
            }
        }

        private void StopAllItemAnimations()
        {
            
            foreach (var kv in _moveCoByItem)
            {
                if (kv.Value != null) StopCoroutine(kv.Value);
                var ui = kv.Key; if (ui == null) continue;
                var le = ui.GetComponent<LayoutElement>(); if (le != null) le.ignoreLayout = false;
            }
            foreach (var kv in _appearCoByItem)
            {
                if (kv.Value != null) StopCoroutine(kv.Value);
                var ui = kv.Key; if (ui == null) continue;
                var le = ui.GetComponent<LayoutElement>(); if (le != null) le.ignoreLayout = false;
            }
            _moveCoByItem.Clear();
            _appearCoByItem.Clear();
        }

        private void StopItemAnimation(CollectionItemUI ui)
        {
            if (ui == null) return;
            if (_moveCoByItem.TryGetValue(ui, out var mco) && mco != null) { StopCoroutine(mco); }
            if (_appearCoByItem.TryGetValue(ui, out var aco) && aco != null) { StopCoroutine(aco); }
            _moveCoByItem.Remove(ui);
            _appearCoByItem.Remove(ui);
            var le = ui.GetComponent<LayoutElement>(); if (le != null) le.ignoreLayout = false;
        }

        private System.Collections.IEnumerator AnimateMoveToTracked(CollectionItemUI ui, RectTransform rt, Vector3 targetWorldPos, LayoutElement le)
        {
            float t = 0f; const float dur = 0.22f;
            Vector3 from = rt.position;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dur);
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                rt.position = Vector3.LerpUnclamped(from, targetWorldPos, e);
                yield return null;
            }
            rt.position = targetWorldPos;
            if (le != null) le.ignoreLayout = false;
            _moveCoByItem.Remove(ui);
            Canvas.ForceUpdateCanvases();
        }

        private System.Collections.IEnumerator AnimateNewAppearTracked(CollectionItemUI ui, RectTransform rt, LayoutElement le)
        {
            float t = 0f; const float dur = 0.16f;
            Vector3 fromScale = rt.localScale;
            Vector3 toScale = Vector3.one;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dur);
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                rt.localScale = Vector3.LerpUnclamped(fromScale, toScale, e);
                yield return null;
            }
            rt.localScale = toScale;
            if (le != null) le.ignoreLayout = false;
            _appearCoByItem.Remove(ui);
            Canvas.ForceUpdateCanvases();
        }

        
        private void SetupSortingUI()
        {
            if (raritySortDropdown == null) return;
            if (raritySortDropdown.options == null || raritySortDropdown.options.Count == 0)
            {
                raritySortDropdown.options = new List<TMP_Dropdown.OptionData>
                {
                    new TMP_Dropdown.OptionData("Rarity: Common → Legendary"),
                    new TMP_Dropdown.OptionData("Rarity: Legendary → Common"),
                };
            }
            int desired = rarityDescending ? 1 : 0;
            if (raritySortDropdown.value != desired)
            {
                raritySortDropdown.SetValueWithoutNotify(desired);
            }
        }

        
        private void HandleRaritySortChanged(int index)
        {
            bool desc = (index == 1);
            if (desc != rarityDescending)
            {
                rarityDescending = desc;
                Refresh();
            }
        }

        
        private string _searchQuery = string.Empty;
        private Coroutine _searchDebounceCo;
        private float _lastSearchRefreshTime = -999f;
        private float _pendingSearchDelay = 0f;
        private void HandleSearchChanged(string text)
        {
            _searchQuery = text ?? string.Empty;
            
            
            float now = Time.unscaledTime;
            float sinceLast = now - _lastSearchRefreshTime;
            float throttle = Mathf.Max(0f, searchThrottleSeconds);
            if (sinceLast >= throttle)
            {
                CancelSearchDebounce();
                _lastSearchRefreshTime = now;
                Refresh();
            }
            else
            {
                
                float remainingThrottle = Mathf.Max(0f, throttle - sinceLast);
                _pendingSearchDelay = Mathf.Max(searchDebounceSeconds, remainingThrottle);
                if (_searchDebounceCo != null) StopCoroutine(_searchDebounceCo);
                _searchDebounceCo = StartCoroutine(SearchDebounceCo());
            }
        }

        private System.Collections.IEnumerator SearchDebounceCo()
        {
            float wait = Mathf.Max(0f, _pendingSearchDelay > 0f ? _pendingSearchDelay : searchDebounceSeconds);
            if (wait > 0f)
            {
                float t = 0f;
                while (t < wait)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            _searchDebounceCo = null;
            _lastSearchRefreshTime = Time.unscaledTime;
            Refresh();
        }

        private void CancelSearchDebounce()
        {
            if (_searchDebounceCo != null)
            {
                StopCoroutine(_searchDebounceCo);
                _searchDebounceCo = null;
            }
        }
    }
}
