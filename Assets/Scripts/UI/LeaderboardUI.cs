using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.UI
{
    public class LeaderboardUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform contentParent;
        [SerializeField] private GameObject entryPrefab;
        [SerializeField] private Button refreshButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text yourRankText;

        [Header("Entry Colors")]
        [SerializeField] private Color top1Color = new Color(1f, 0.84f, 0f, 1f);
        [SerializeField] private Color top2Color = new Color(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private Color top3Color = new Color(0.72f, 0.45f, 0.2f, 1f);
        [SerializeField] private Color normalColor = new Color(0.12f, 0.12f, 0.15f, 1f);
        [SerializeField] private Color yourEntryColor = new Color(0.4f, 0.8f, 1f, 1f);

        private List<GameObject> _spawnedEntries = new List<GameObject>();

        private void OnEnable()
        {
            LeaderboardService.OnLeaderboardLoaded += HandleLeaderboardLoaded;
            LeaderboardService.OnLeaderboardFailed += HandleLeaderboardFailed;

            if (refreshButton != null)
                refreshButton.onClick.AddListener(Refresh);

            Refresh();
        }

        private void OnDisable()
        {
            LeaderboardService.OnLeaderboardLoaded -= HandleLeaderboardLoaded;
            LeaderboardService.OnLeaderboardFailed -= HandleLeaderboardFailed;

            if (refreshButton != null)
                refreshButton.onClick.RemoveListener(Refresh);
        }

        public void Refresh()
        {
            if (statusText != null) statusText.text = "Loading leaderboard...";

            if (LeaderboardService.Instance != null)
                LeaderboardService.Instance.FetchTopPlayers();
            else if (statusText != null)
                statusText.text = "Leaderboard service not available.";
        }

        private void HandleLeaderboardLoaded(List<LeaderboardService.LeaderboardEntry> entries)
        {
            ClearEntries();

            if (statusText != null) statusText.text = "";

            string myUid = FirebaseService.UserId;
            int myRank = -1;

            foreach (var entry in entries)
            {
                bool isMe = entry.uid == myUid;
                if (isMe) myRank = entry.rank;

                var go = Instantiate(entryPrefab, contentParent);
                go.SetActive(true);
                _spawnedEntries.Add(go);

                var texts = go.GetComponentsInChildren<TMP_Text>(true);
                if (texts.Length >= 3)
                {
                    texts[0].text = $"#{entry.rank}";
                    texts[1].text = entry.playerName;
                    texts[2].text = entry.trophies.ToString();

                    Color c = entry.rank switch
                    {
                        1 => top1Color,
                        2 => top2Color,
                        3 => top3Color,
                        _ => normalColor
                    };

                    if (isMe) c = yourEntryColor;

                    foreach (var t in texts)
                        t.color = c;
                }
            }

            if (yourRankText != null)
            {
                if (FirebaseService.IsGuest)
                    yourRankText.text = "Sign in to appear on the leaderboard";
                else if (myRank > 0)
                    yourRankText.text = $"Your Rank: #{myRank}";
                else
                    yourRankText.text = "Your Rank: Unranked";
            }
        }

        private void HandleLeaderboardFailed(string error)
        {
            if (statusText == null) return;

            bool looksLikePermissions = !string.IsNullOrEmpty(error) &&
                (error.IndexOf("permission", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 error.IndexOf("PERMISSION_DENIED", System.StringComparison.OrdinalIgnoreCase) >= 0);

            if (looksLikePermissions && FirebaseService.IsGuest)
                statusText.text = "Sign in to view the leaderboard.";
            else
                statusText.text = $"Failed to load: {error}";
        }

        private void ClearEntries()
        {
            foreach (var go in _spawnedEntries)
            {
                if (go != null) Destroy(go);
            }
            _spawnedEntries.Clear();
        }
    }
}
