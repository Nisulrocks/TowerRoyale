using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Net;

namespace TR.UI
{
    // Friends tab: friends list, incoming requests, player search, and duo invites.
    public class FriendsPanelUI : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Button friendsTabButton;
        [SerializeField] private Button requestsTabButton;
        [SerializeField] private Button searchTabButton;
        [SerializeField] private GameObject friendsTab;
        [SerializeField] private GameObject requestsTab;
        [SerializeField] private GameObject searchTab;

        [Header("Friends")]
        [SerializeField] private Transform friendsContent;
        [SerializeField] private GameObject friendEntryPrefab;
        [SerializeField] private TMP_Text friendsEmptyText;

        [Header("Requests")]
        [SerializeField] private Transform requestsContent;
        [SerializeField] private GameObject requestEntryPrefab;
        [SerializeField] private TMP_Text requestsEmptyText;
        [SerializeField] private GameObject requestsBadge;
        [SerializeField] private TMP_Text requestsBadgeText;

        [Header("Search")]
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private Button searchButton;
        [SerializeField] private Transform searchContent;
        [SerializeField] private GameObject searchEntryPrefab;
        [SerializeField] private TMP_Text searchStatusText;

        [Header("Guest Mode")]
        [Tooltip("Optional. Shown instead of the friends UI when the player has not signed in.")]
        [SerializeField] private GameObject guestNoticeRoot;
        [Tooltip("Optional. Message inside the guest notice.")]
        [SerializeField] private TMP_Text guestNoticeText;
        [TextArea]
        [SerializeField] private string guestMessage =
            "Friends are only available with an account.\nSign in to add friends and invite them to Duo matches.";

        [Header("Popups")]
        [SerializeField] private FriendContextMenuUI contextMenu;
        [SerializeField] private DuoInviteNotificationUI invitePopup;

        [Header("Shared")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text myIdText;
        [SerializeField] private Button copyIdButton;
        [SerializeField] private Button refreshButton;

        [Header("Presence")]
        [Tooltip("How often the friends list is re-read so online status stays current.")]
        [SerializeField] private float presenceRefreshSeconds = 10f;

        private float _nextPresenceRefresh;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly HashSet<string> _friendUids = new HashSet<string>();
        private float _statusClearAt;

        private void OnEnable()
        {
            FriendsService.OnFriendsLoaded += HandleFriendsLoaded;
            FriendsService.OnRequestsLoaded += HandleRequestsLoaded;
            FriendsService.OnSearchResults += HandleSearchResults;
            FriendsService.OnSearchFailed += HandleSearchFailed;
            FriendsService.OnActionSucceeded += HandleActionMessage;
            FriendsService.OnActionFailed += HandleActionMessage;
            // Incoming invites are handled by DuoInviteListener so they arrive on any screen.

            if (friendsTabButton != null) friendsTabButton.onClick.AddListener(ShowFriendsTab);
            if (requestsTabButton != null) requestsTabButton.onClick.AddListener(ShowRequestsTab);
            if (searchTabButton != null) searchTabButton.onClick.AddListener(ShowSearchTab);
            if (searchButton != null) searchButton.onClick.AddListener(RunSearch);
            if (refreshButton != null) refreshButton.onClick.AddListener(RefreshAll);
            if (copyIdButton != null) copyIdButton.onClick.AddListener(CopyMyId);
            if (searchInput != null) searchInput.onSubmit.AddListener(_ => RunSearch());

            if (FirebaseService.IsGuest)
            {
                ApplyGuestState();
                return;
            }

            if (guestNoticeRoot != null) guestNoticeRoot.SetActive(false);
            if (myIdText != null) myIdText.text = $"Your ID: {FirebaseService.UserId}";

            ShowFriendsTab();
            RefreshAll();
        }

        private void OnDisable()
        {
            FriendsService.OnFriendsLoaded -= HandleFriendsLoaded;
            FriendsService.OnRequestsLoaded -= HandleRequestsLoaded;
            FriendsService.OnSearchResults -= HandleSearchResults;
            FriendsService.OnSearchFailed -= HandleSearchFailed;
            FriendsService.OnActionSucceeded -= HandleActionMessage;
            FriendsService.OnActionFailed -= HandleActionMessage;

            if (friendsTabButton != null) friendsTabButton.onClick.RemoveListener(ShowFriendsTab);
            if (requestsTabButton != null) requestsTabButton.onClick.RemoveListener(ShowRequestsTab);
            if (searchTabButton != null) searchTabButton.onClick.RemoveListener(ShowSearchTab);
            if (searchButton != null) searchButton.onClick.RemoveListener(RunSearch);
            if (refreshButton != null) refreshButton.onClick.RemoveListener(RefreshAll);
            if (copyIdButton != null) copyIdButton.onClick.RemoveListener(CopyMyId);

            if (contextMenu != null) contextMenu.Close();
        }

        private void Update()
        {
            if (_statusClearAt > 0f && Time.unscaledTime >= _statusClearAt)
            {
                _statusClearAt = 0f;
                if (statusText != null) statusText.text = string.Empty;
            }

            // Presence was previously only read when the panel opened, so a friend coming online or
            // going offline never showed up until the player reopened the tab.
            if (FirebaseService.IsGuest) return;

            if (presenceRefreshSeconds > 0f && Time.unscaledTime >= _nextPresenceRefresh)
            {
                _nextPresenceRefresh = Time.unscaledTime + presenceRefreshSeconds;
                if (FriendsService.Instance != null) FriendsService.Instance.RefreshFriends();
            }
        }

        // Guests have no account to hang friends off, so present the tab as unavailable rather than
        // letting it look broken (empty lists, searches that always fail).
        private void ApplyGuestState()
        {
            if (guestNoticeRoot != null) guestNoticeRoot.SetActive(true);
            if (guestNoticeText != null) guestNoticeText.text = guestMessage;

            if (friendsTab != null) friendsTab.SetActive(false);
            if (requestsTab != null) requestsTab.SetActive(false);
            if (searchTab != null) searchTab.SetActive(false);

            SetInteractable(friendsTabButton, false);
            SetInteractable(requestsTabButton, false);
            SetInteractable(searchTabButton, false);
            SetInteractable(searchButton, false);
            SetInteractable(refreshButton, false);
            SetInteractable(copyIdButton, false);
            if (searchInput != null) searchInput.interactable = false;

            if (requestsBadge != null) requestsBadge.SetActive(false);
            if (myIdText != null) myIdText.text = "Playing as Guest";

            // No prefab wired for the notice: fall back to the status line so the reason is still
            // visible rather than showing an empty panel.
            if (guestNoticeRoot == null && statusText != null)
            {
                statusText.text = guestMessage;
                _statusClearAt = 0f;
            }
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null) button.interactable = value;
        }

        // ---------- tabs ----------

        public void ShowFriendsTab() => SetTab(0);
        public void ShowRequestsTab() => SetTab(1);
        public void ShowSearchTab() => SetTab(2);

        private void SetTab(int index)
        {
            if (friendsTab != null) friendsTab.SetActive(index == 0);
            if (requestsTab != null) requestsTab.SetActive(index == 1);
            if (searchTab != null) searchTab.SetActive(index == 2);
            if (contextMenu != null) contextMenu.Close();
        }

        public void RefreshAll()
        {
            if (FirebaseService.IsGuest) { ApplyGuestState(); return; }
            if (FriendsService.Instance == null)
            {
                SetStatus("Friends unavailable — not signed in.");
                return;
            }
            FriendsService.Instance.RefreshFriends();
            FriendsService.Instance.RefreshRequests();
        }

        // ---------- friends ----------

        private void HandleFriendsLoaded(List<FriendsService.PlayerSummary> friends)
        {
            ClearChildren(friendsContent);
            _friendUids.Clear();

            foreach (var f in friends)
            {
                if (f == null) continue;
                _friendUids.Add(f.uid);

                if (friendEntryPrefab == null || friendsContent == null) continue;
                var go = Instantiate(friendEntryPrefab, friendsContent);
                go.SetActive(true);
                _spawned.Add(go);

                var entry = go.GetComponent<FriendEntryUI>();
                if (entry != null) entry.Bind(f, this);
            }

            if (friendsEmptyText != null)
                friendsEmptyText.gameObject.SetActive(friends.Count == 0);
        }

        // ---------- requests ----------

        private void HandleRequestsLoaded(List<FriendsService.FriendRequestInfo> requests)
        {
            ClearChildren(requestsContent);

            foreach (var r in requests)
            {
                if (r == null || requestEntryPrefab == null || requestsContent == null) continue;
                var go = Instantiate(requestEntryPrefab, requestsContent);
                go.SetActive(true);
                _spawned.Add(go);

                var entry = go.GetComponent<FriendRequestEntryUI>();
                if (entry != null) entry.Bind(r);
            }

            if (requestsEmptyText != null)
                requestsEmptyText.gameObject.SetActive(requests.Count == 0);

            if (requestsBadge != null) requestsBadge.SetActive(requests.Count > 0);
            if (requestsBadgeText != null) requestsBadgeText.text = requests.Count.ToString();
        }

        // ---------- search ----------

        public void RunSearch()
        {
            if (FirebaseService.IsGuest) { SetStatus(guestMessage); return; }
            if (FriendsService.Instance == null) { SetStatus("Not signed in."); return; }
            string q = searchInput != null ? searchInput.text : string.Empty;
            if (searchStatusText != null) searchStatusText.text = "Searching...";
            FriendsService.Instance.SearchPlayers(q);
        }

        private void HandleSearchResults(List<FriendsService.PlayerSummary> results)
        {
            ClearChildren(searchContent);

            if (searchStatusText != null)
                searchStatusText.text = results.Count == 0 ? "No players found." : string.Empty;

            foreach (var r in results)
            {
                if (r == null || searchEntryPrefab == null || searchContent == null) continue;
                var go = Instantiate(searchEntryPrefab, searchContent);
                go.SetActive(true);
                _spawned.Add(go);

                var entry = go.GetComponent<PlayerSearchEntryUI>();
                if (entry != null) entry.Bind(r, _friendUids.Contains(r.uid));
            }
        }

        private void HandleSearchFailed(string error)
        {
            if (searchStatusText != null) searchStatusText.text = error;
        }

        // ---------- context menu / invites ----------

        public void ShowContextMenu(FriendsService.PlayerSummary target, Vector2 screenPos)
        {
            if (contextMenu != null) contextMenu.Open(target, screenPos, this);
        }

        public void InviteToDuo(FriendsService.PlayerSummary target)
        {
            if (target == null || FriendsService.Instance == null) return;
            if (DuoNetworkManager.Instance == null) { SetStatus("Matchmaking unavailable."); return; }

            // Enforced here as well as on the buttons: a list refreshed a moment ago can leave a
            // button enabled after the friend's trophies moved them to another arena.
            if (!target.isOnline)
            {
                SetStatus($"{target.playerName} is offline.");
                return;
            }
            if (target.isInMatch)
            {
                SetStatus($"{target.playerName} is already in a match.");
                return;
            }
            if (!target.IsSameArenaAsLocal)
            {
                SetStatus($"{target.playerName} is in a different arena — you can only duo within the same arena.");
                return;
            }

            // The inviter hosts the room and hands its name plus its arena to the friend, so both
            // clients load the same battle scene.
            string roomName = DuoNetworkManager.NewFriendRoomName();
            string arenaKey = DuoInviteListener.LocalArenaKey();
            string friendName = target.playerName;

            if (string.IsNullOrEmpty(arenaKey))
            {
                SetStatus("No arena available to play in.");
                return;
            }

            SetStatus($"Inviting {friendName}...");

            // Only host once the invite is actually on the server — otherwise a rejected write left
            // the inviter sitting alone in a room nobody could ever be told about.
            FriendsService.Instance.SendDuoInvite(target.uid, roomName, arenaKey, (ok, error) =>
            {
                if (!ok)
                {
                    SetStatus($"Couldn't invite {friendName}: {error}");
                    return;
                }
                DuoInviteListener.JoinFriendMatch(roomName, asHost: true, arenaKey);
                SetStatus($"Invited {friendName}. Waiting in the room...");
            });
        }

        // ---------- helpers ----------

        private void CopyMyId()
        {
            string uid = FirebaseService.UserId;
            if (string.IsNullOrEmpty(uid)) return;
            GUIUtility.systemCopyBuffer = uid;
            SetStatus("Player ID copied.");
        }

        private void HandleActionMessage(string message) => SetStatus(message);

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            _statusClearAt = Time.unscaledTime + 4f;
        }

        private void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                var go = _spawned[i];
                if (go == null) { _spawned.RemoveAt(i); continue; }
                if (go.transform.parent != parent) continue;
                _spawned.RemoveAt(i);
                Destroy(go);
            }
        }
    }
}
