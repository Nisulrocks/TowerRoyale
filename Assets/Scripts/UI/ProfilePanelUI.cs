using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.UI
{
    /// The player's own profile: identity, lifetime stats, and the battle log.
    public class ProfilePanelUI : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text castleLevelText;
        [SerializeField] private TMP_Text arenaText;
        [SerializeField] private TMP_Text trophiesText;
        [SerializeField] private TMP_Text bestTrophiesText;

        [Header("Stats")]
        [SerializeField] private TMP_Text matchesText;
        [SerializeField] private TMP_Text winsText;
        [SerializeField] private TMP_Text lossesText;
        [SerializeField] private TMP_Text abandonedText;
        [SerializeField] private TMP_Text winRateText;
        [SerializeField] private TMP_Text streakText;
        [SerializeField] private TMP_Text bestStreakText;
        [SerializeField] private TMP_Text cardsOwnedText;

        [Header("Battle Log")]
        [SerializeField] private BattleLogEntryUI logEntryPrefab;
        [SerializeField] private Transform logListRoot;
        [Tooltip("Scroll view holding the log. Left empty, it is found from logListRoot's parents.")]
        [SerializeField] private ScrollRect logScroll;
        [Tooltip("Shown instead of the list when no matches have been played yet.")]
        [SerializeField] private GameObject emptyStateRoot;
        [SerializeField] private TMP_Text emptyStateText;

        [Header("Close")]
        [SerializeField] private Button closeButton;
        [Tooltip("Panel name in the PanelSwitcher to return to when closing. Must match the name in the switcher's list, which uses the tab names (e.g. 'Play Tab').")]
        [SerializeField] private string returnPanelName = "Play Tab";

        private readonly List<BattleLogEntryUI> _entries = new();

        private void OnEnable()
        {
            BattleLogService.OnLogChanged += Refresh;
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }
            Refresh();
        }

        private void OnDisable()
        {
            BattleLogService.OnLogChanged -= Refresh;
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }

        private void Close()
        {
            var switcher = GetComponentInParent<PanelSwitcher>();
            if (switcher == null) switcher = FindFirstObjectByType<PanelSwitcher>(FindObjectsInactive.Include);
            if (switcher != null) switcher.ShowByName(returnPanelName);
            else gameObject.SetActive(false);
        }

        public void Refresh()
        {
            RefreshHeader();
            RefreshStats();
            RefreshLog();
        }

        private void RefreshHeader()
        {
            string name = PlayerProfile.GetPlayerName();
            if (playerNameText != null)
                playerNameText.text = string.IsNullOrEmpty(name) ? "Commander" : name;

            if (castleLevelText != null)
                castleLevelText.text = $"Castle Lv {PlayerProfile.Data.castleLevel}";

            if (arenaText != null)
            {
                var arena = ArenaService.GetCurrentArena();
                arenaText.text = arena != null ? arena.DisplayName : "-";
            }

            if (trophiesText != null) trophiesText.text = PlayerProfile.GetTrophies().ToString();
            if (bestTrophiesText != null) bestTrophiesText.text = $"Best {BattleLogService.BestTrophies}";
        }

        private void RefreshStats()
        {
            if (matchesText != null) matchesText.text = BattleLogService.TotalMatches.ToString();
            if (winsText != null) winsText.text = BattleLogService.Wins.ToString();
            if (lossesText != null) lossesText.text = BattleLogService.Losses.ToString();
            if (abandonedText != null) abandonedText.text = BattleLogService.Abandons.ToString();

            if (winRateText != null)
            {
                winRateText.text = BattleLogService.TotalMatches > 0
                    ? $"{BattleLogService.WinRate * 100f:0.#}%"
                    : "-";
            }

            if (streakText != null) streakText.text = BattleLogService.CurrentWinStreak.ToString();
            if (bestStreakText != null) bestStreakText.text = BattleLogService.LongestWinStreak.ToString();

            if (cardsOwnedText != null)
            {
                GameDB.EnsureLoaded();
                int owned = 0;
                var cards = GameDB.Cards;
                for (int i = 0; i < cards.Count; i++)
                {
                    var cp = PlayerProfile.GetOrCreateCard(cards[i].CardId);
                    if (cp != null && cp.ownedCount > 0) owned++;
                }
                cardsOwnedText.text = $"{owned}/{cards.Count}";
            }
        }

        private void RefreshLog()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i] != null) Destroy(_entries[i].gameObject);
            _entries.Clear();

            var log = BattleLogService.Entries;
            bool empty = log == null || log.Count == 0;

            if (emptyStateRoot != null) emptyStateRoot.SetActive(empty);
            if (empty && emptyStateText != null)
                emptyStateText.text = "No battles yet.\nPlay a match and it will show up here.";

            if (empty || logEntryPrefab == null || logListRoot == null) return;

            for (int i = 0; i < log.Count; i++)
            {
                var entry = Instantiate(logEntryPrefab, logListRoot);
                entry.Bind(log[i]);
                _entries.Add(entry);
            }

            ScrollLogToTop();
        }

        // Same trap as the deck builder: Destroy is deferred to end of frame, so the layout still
        // counts the old rows right now. Snap once immediately and again once they are really gone.
        private void ScrollLogToTop()
        {
            if (logScroll == null && logListRoot != null)
                logScroll = logListRoot.GetComponentInParent<ScrollRect>(true);
            if (logScroll == null) return;

            SnapLogToTop();
            if (isActiveAndEnabled) StartCoroutine(SnapLogToTopNextFrame());
        }

        private System.Collections.IEnumerator SnapLogToTopNextFrame()
        {
            yield return null;
            SnapLogToTop();
        }

        private void SnapLogToTop()
        {
            if (logScroll == null || !logScroll.vertical) return;
            if (logScroll.content != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(logScroll.content);
            }
            logScroll.velocity = Vector2.zero;
            logScroll.verticalNormalizedPosition = 1f;
        }
    }
}
