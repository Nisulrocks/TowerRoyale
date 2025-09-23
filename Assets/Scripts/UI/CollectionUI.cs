using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Data;

namespace TR.UI
{
    // Displays owned cards with levels and lets you upgrade when possible.
    public class CollectionUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private CollectionItemUI itemPrefab;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text softCurrencyText; // shows current coins
        [Header("Sorting")]
        [Tooltip("Optional dropdown to control rarity order. If not assigned, defaults to ascending (Common -> Legendary).")]
        [SerializeField] private TMP_Dropdown raritySortDropdown;
        [Tooltip("If true, rarity order is reversed (Legendary -> Common)")]
        [SerializeField] private bool rarityDescending = false;
        [Header("Search")]
        [Tooltip("Optional input field to filter cards by name (case-insensitive)")]
        [SerializeField] private TMP_InputField searchInput;

        private readonly List<CollectionItemUI> _items = new();

        private void Awake()
        {
            // Preload data database so the first time opening the panel doesn't hitch
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
            // Compute rarity priority using order in GameDB.Rarities (lower index = more common)
            int GetRarityPriority(RarityDefinition r)
            {
                if (r == null) return int.MaxValue;
                var rs = GameDB.Rarities;
                for (int i = 0; i < rs.Count; i++) if (rs[i] == r) return i;
                return int.MaxValue - 1;
            }

            // Sort policy:
            // 1) Owned cards first (discovered)
            // 2) Then cards unlocked for current trophies but not owned yet
            // 3) Then cards locked by higher arenas
            // Within each group: sort by rarity (GameDB.Rarities order), then by display name
            var sorted = new List<CardDefinition>(GameDB.Cards);
            // Apply name filter if provided
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
                if (aOwned != bOwned) return bOwned.CompareTo(aOwned); // true first

                bool aUnlockNow = a.IsUnlockedForPlayer();
                bool bUnlockNow = b.IsUnlockedForPlayer();
                // section: 0 owned, 1 unlocked-not-owned, 2 locked
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

            // Build/reuse UI instances and animate reordering
            bool firstBuild = _items.Count == 0;
            var existingById = new Dictionary<string, CollectionItemUI>();
            foreach (var it in _items)
            {
                if (it != null && it.Card != null && !string.IsNullOrEmpty(it.Card.CardId))
                    existingById[it.Card.CardId] = it;
            }

            var ordered = new List<CollectionItemUI>(sorted.Count);
            var newlyCreated = new HashSet<CollectionItemUI>();
            foreach (var card in sorted)
            {
                if (!existingById.TryGetValue(card.CardId, out var ui) || ui == null)
                {
                    ui = Instantiate(itemPrefab, listRoot);
                    ui.Bind(card);
                    _items.Add(ui);
                    newlyCreated.Add(ui);
                }
                else
                {
                    // Ensure binding reflects latest player state
                    ui.Bind(card);
                }
                ui.gameObject.SetActive(true);
                ordered.Add(ui);
            }

            // If searching, hide all non-matches and skip animations. Keep pool for reuse.
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
                // On the very first build, skip animations for instant display
                foreach (var ui in ordered)
                {
                    ui.transform.SetSiblingIndex(ordered.IndexOf(ui));
                }
                Canvas.ForceUpdateCanvases();
                var rootRt0 = listRoot as RectTransform;
                if (rootRt0 != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt0);
                return;
            }

            // Capture old world positions
            var oldPos = new Dictionary<RectTransform, Vector3>(ordered.Count);
            foreach (var ui in ordered)
            {
                var rt = ui.transform as RectTransform;
                if (rt != null) oldPos[rt] = rt.position;
            }

            // Reorder hierarchy to new order
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].transform.SetSiblingIndex(i);
            }

            // Force layout to compute target positions
            Canvas.ForceUpdateCanvases();
            var rootRt = listRoot as RectTransform;
            if (rootRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);

            // Animate items from old -> new positions
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
                    // Spawn from target with small scale (no alpha tween to avoid clashing with existing CanvasGroups)
                    rt.position = targetPos;
                    rt.localScale = Vector3.one * 0.88f;
                    StartCoroutine(AnimateNewAppear(rt, le));
                }
                else
                {
                    // Move from previous world position to new layout position
                    rt.position = startPos;
                    StartCoroutine(AnimateMoveTo(rt, targetPos, le));
                }
            }
        }

        private System.Collections.IEnumerator AnimateMoveTo(RectTransform rt, Vector3 targetWorldPos, LayoutElement le)
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
            Canvas.ForceUpdateCanvases();
        }

        private System.Collections.IEnumerator AnimateNewAppear(RectTransform rt, LayoutElement le)
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
            Canvas.ForceUpdateCanvases();
        }

        // Initialize rarity sort dropdown options and sync with current setting
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

        // Handle dropdown change and refresh collection
        private void HandleRaritySortChanged(int index)
        {
            bool desc = (index == 1);
            if (desc != rarityDescending)
            {
                rarityDescending = desc;
                Refresh();
            }
        }

        // Handle search input change
        private string _searchQuery = string.Empty;
        private void HandleSearchChanged(string text)
        {
            _searchQuery = text ?? string.Empty;
            Refresh();
        }
    }
}
