using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;

namespace TR.Systems
{
    // Firestore-backed friends list, friend requests, presence and duo invites.
    //
    // Layout:
    //   profiles/{uid}                          playerName, nameLower, trophies, onlineAt
    //   friends/{uid}/list/{friendUid}          name, since
    //   friendRequests/{uid}/incoming/{fromUid} fromName, sentAt
    //   duoInvites/{uid}/incoming/{fromUid}     fromName, roomName, sentAt
    //
    // Requests and invites are written into the *recipient's* subtree, so the security rules must
    // allow a signed-in user to create documents under another user's friendRequests/duoInvites.
    public class FriendsService : MonoBehaviour
    {
        public static FriendsService Instance { get; private set; }

        private const string ProfilesCollection = "profiles";
        private const string FriendsCollection = "friends";
        private const string RequestsCollection = "friendRequests";
        private const string InvitesCollection = "duoInvites";
        private const string SubList = "list";
        private const string SubIncoming = "incoming";

        private const string FieldPlayerName = "playerName";
        private const string FieldNameLower = "nameLower";
        private const string FieldTrophies = "trophies";
        private const string FieldOnlineAt = "onlineAt";
        private const string FieldInMatch = "inMatch";
        private const string FieldFromName = "fromName";
        private const string FieldSentAt = "sentAt";
        private const string FieldRoomName = "roomName";
        private const string FieldArenaKey = "arenaKey";
        private const string FieldSince = "since";
        private const string FieldName = "name";

        // Upper bound for a Firestore prefix range query. Written as an escape rather than a
        // literal so the source stays plain ASCII.
        private const string HighCodePoint = "\uf8ff";

        // How long an unanswered invite stays valid before it is swept.
        private const int InviteLifetimeSeconds = 120;

        // Anti-spam. Client-side only, so it stops accidental and casual spam, not a modified
        // client — real enforcement would need a Cloud Function or a rules-based rate limit.
        private const float GlobalInviteCooldownSeconds = 4f;
        private const float PerFriendInviteCooldownSeconds = 30f;

        // A player counts as online if they wrote a heartbeat within this window. The window must
        // comfortably exceed the heartbeat interval or a client looks offline between beats.
        private const int OnlineWindowSeconds = 45;
        private const float HeartbeatSeconds = 15f;
        private const int SearchLimit = 20;

        [Tooltip("Logs what onlineAt value was read for each friend and why they resolved online/offline.")]
        [SerializeField] private bool verbosePresenceLogging = true;

        public class PlayerSummary
        {
            public string uid;
            public string playerName;
            public int trophies;
            public bool isOnline;

            // Online but unavailable: in a battle, or already searching for a match.
            public bool isInMatch;

            // Duo matches are arena-locked, so a friend's arena is derived from their trophies and
            // compared with ours. No extra Firestore field is needed for this.
            public string arenaKey;
            public string arenaName;

            public bool IsSameArenaAsLocal =>
                !string.IsNullOrEmpty(arenaKey) && arenaKey == ArenaService.GetLocalArenaKey();

            // Everything that must be true before an invite can be sent.
            public bool CanInviteToDuo => isOnline && !isInMatch && IsSameArenaAsLocal;
        }

        public class FriendRequestInfo
        {
            public string fromUid;
            public string fromName;
            public long sentAt;
        }

        public class DuoInviteInfo
        {
            public string fromUid;
            public string fromName;
            public string roomName;
            // The host's arena decides the battle scene, so both players load the same one even if
            // their trophy counts would otherwise put them in different arenas.
            public string arenaKey;
            public long sentAt;
        }

        public static event Action<List<PlayerSummary>> OnSearchResults;
        public static event Action<string> OnSearchFailed;
        public static event Action<List<PlayerSummary>> OnFriendsLoaded;
        public static event Action<List<FriendRequestInfo>> OnRequestsLoaded;
        public static event Action<DuoInviteInfo> OnDuoInviteReceived;

        // An invite that expired while the player was offline. The room is long gone, so this is
        // purely "you missed this" — it must never be treated as joinable.
        public static event Action<DuoInviteInfo> OnMissedInvite;
        public static event Action<string> OnActionFailed;
        public static event Action<string> OnActionSucceeded;

        private FirebaseFirestore _db;
        private bool _ready;
        private string _uid;
        private ListenerRegistration _requestListener;
        private ListenerRegistration _inviteListener;
        private Coroutine _heartbeat;

        // Last invite room seen per sender, so a re-delivered snapshot is not surfaced twice.
        private readonly Dictionary<string, string> _lastInviteRoom = new Dictionary<string, string>();

        // Invites we have sent, so a crossed invite (both friends inviting each other at once) can
        // be detected and resolved. Local bookkeeping only, so a local clock is fine here.
        private class OutgoingInvite
        {
            public string roomName;
            public float sentAtRealtime;
        }
        private readonly Dictionary<string, OutgoingInvite> _outgoing = new Dictionary<string, OutgoingInvite>();

        // The room we offered this friend, or null if we have no live invite out to them.
        public string GetOutgoingInviteRoom(string toUid)
        {
            if (string.IsNullOrEmpty(toUid)) return null;
            if (!_outgoing.TryGetValue(toUid, out var pending)) return null;
            if (Time.realtimeSinceStartup - pending.sentAtRealtime > InviteLifetimeSeconds)
            {
                _outgoing.Remove(toUid);
                return null;
            }
            return pending.roomName;
        }

        public void ForgetOutgoingInvite(string toUid)
        {
            if (!string.IsNullOrEmpty(toUid)) _outgoing.Remove(toUid);
        }

        private float _nextInviteAllowed;
        private readonly Dictionary<string, float> _nextInviteToFriend = new Dictionary<string, float>();

        // Seconds until this friend can be invited again, or 0 when they can be invited now.
        public float GetInviteCooldownRemaining(string toUid)
        {
            float now = Time.realtimeSinceStartup;
            float remaining = Mathf.Max(0f, _nextInviteAllowed - now);
            if (!string.IsNullOrEmpty(toUid) && _nextInviteToFriend.TryGetValue(toUid, out float perFriend))
                remaining = Mathf.Max(remaining, perFriend - now);
            return Mathf.Max(0f, remaining);
        }

        // Invites arrive on a Firestore callback thread; queue them for the main thread.
        private readonly Queue<DuoInviteInfo> _pendingInvites = new Queue<DuoInviteInfo>();
        private readonly Queue<DuoInviteInfo> _pendingMissed = new Queue<DuoInviteInfo>();
        private readonly object _inviteLock = new object();
        private bool _requestsDirty;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            FirebaseService.OnSignInComplete += HandleSignIn;
            FirebaseService.OnSignOut += HandleSignOut;
        }

        private void OnDestroy()
        {
            FirebaseService.OnSignInComplete -= HandleSignIn;
            FirebaseService.OnSignOut -= HandleSignOut;
            StopListeners();
            if (Instance == this) Instance = null;
        }

        private void HandleSignIn(string uid, string displayName) => Initialize(uid);
        private void HandleSignOut() => Shutdown();

        public void Initialize(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            if (_ready && _uid == uid) return;

            if (!FirestoreProvider.TryGet(out _db))
            {
                Debug.LogWarning("[FriendsService] Firestore unavailable; friends disabled.");
                return;
            }

            _uid = uid;
            _ready = true;

            StartListeners();
            if (_heartbeat == null) _heartbeat = StartCoroutine(HeartbeatLoop());
            // Invites that expired while this player was offline are never surfaced by the
            // listener, so sweep them on the way in.
            StartCoroutine(PruneStaleInvites());
            Debug.Log($"[FriendsService] Ready for {uid}.");
        }

        private void Shutdown()
        {
            StopListeners();
            if (_heartbeat != null) { StopCoroutine(_heartbeat); _heartbeat = null; }
            _ready = false;
            _uid = null;
        }

        // ---------- presence ----------

        private IEnumerator HeartbeatLoop()
        {
            while (true)
            {
                WritePresence();

                // Learn the device/server clock offset from our own stamp, then refresh it
                // occasionally in case the system clock is corrected while running.
                if (!_serverOffsetKnown || Time.unscaledTime >= _nextClockSync)
                {
                    _nextClockSync = Time.unscaledTime + 120f;
                    yield return new WaitForSecondsRealtime(2f);
                    yield return SyncServerClock();
                }

                // A fresh instance each pass: yield instructions hold state and reusing one across
                // iterations is version-dependent behaviour that can silently stop the loop.
                yield return new WaitForSecondsRealtime(HeartbeatSeconds);
            }
        }

        private void WritePresence()
        {
            if (!_ready || string.IsNullOrEmpty(_uid)) return;
            // A kicked session must stop advertising itself as online.
            if (SessionGuardService.IsKicked) return;

            // Stamped by Firestore so every client's presence is on one clock.
            var data = new Dictionary<string, object>
            {
                { FieldOnlineAt, FieldValue.ServerTimestamp },
                { FieldInMatch, IsLocallyBusy() }
            };
            _db.Collection(ProfilesCollection).Document(_uid).SetAsync(data, SetOptions.MergeAll)
               .ContinueWith(t =>
               {
                   // A silently rejected heartbeat is exactly what makes a player look offline to
                   // everyone else while looking fine to themselves, so surface it.
                   if (t.IsFaulted)
                       Debug.LogError($"[FriendsService] Presence write REJECTED for {_uid}: " +
                                      $"{t.Exception?.GetBaseException()?.Message}");
                   else if (verbosePresenceLogging)
                       Debug.Log("[FriendsService] presence heartbeat written (server timestamp).");
               });
        }

        // Deliberately narrower than MatchContext.IsMatchInProgress, which stays true while merely
        // sitting in a Photon room back in the lobby — that would advertise us as busy forever.
        // Being mid-search counts too: an invite then is just as futile as one sent mid-battle.
        private static bool IsLocallyBusy()
        {
            if (TR.Battle.BattleSceneController.Instance != null) return true;

            var duo = TR.Net.DuoNetworkManager.Instance;
            if (duo != null &&
                duo.State != TR.Net.DuoNetworkManager.MatchState.Idle &&
                duo.State != TR.Net.DuoNetworkManager.MatchState.Failed)
                return true;

            return false;
        }

        private static bool IsOnline(long onlineAt)
        {
            if (onlineAt <= 0) return false;
            long age = ServerNowSeconds() - onlineAt;
            // A small negative age is normal (our clock estimate lags the writer by up to one
            // heartbeat); treat anything not-yet-expired as online.
            return age <= OnlineWindowSeconds;
        }

        // ---------- server clock ----------
        //
        // Presence and invites are stamped by Firestore, not the client, because device clocks
        // drift. Comparing a writer's local clock against a reader's local clock made a healthy
        // client look permanently offline (and made fresh invites look already expired) whenever
        // the two devices disagreed by more than the expiry window.

        private static long _serverOffsetSeconds;
        private static bool _serverOffsetKnown;
        private float _nextClockSync;

        private static long LocalNowSeconds() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static long ServerNowSeconds()
            => LocalNowSeconds() + (_serverOffsetKnown ? _serverOffsetSeconds : 0L);

        // Reads back our own server-stamped heartbeat to learn how far this device's clock is off.
        private IEnumerator SyncServerClock()
        {
            var task = _db.Collection(ProfilesCollection).Document(_uid).GetSnapshotAsync();
            yield return new WaitUntil(() => task.IsCompleted);
            if (task.IsFaulted || task.Result == null || !task.Result.Exists) yield break;

            long server = ReadUnixSeconds(task.Result, FieldOnlineAt);
            if (server <= 0) yield break;

            long offset = server - LocalNowSeconds();
            bool changed = !_serverOffsetKnown || Math.Abs(offset - _serverOffsetSeconds) > 2;
            _serverOffsetSeconds = offset;
            _serverOffsetKnown = true;

            if (changed)
                Debug.Log($"[FriendsService] server clock offset = {offset}s " +
                          "(device clock is " + (offset > 0 ? "behind" : "ahead") + " of Firestore)");
        }

        // ---------- search ----------

        public void SearchPlayers(string query)
        {
            if (!_ready) { OnSearchFailed?.Invoke("Not connected."); return; }
            query = (query ?? string.Empty).Trim();
            if (query.Length < 2) { OnSearchFailed?.Invoke("Enter at least 2 characters."); return; }
            StartCoroutine(SearchCoroutine(query));
        }

        private IEnumerator SearchCoroutine(string query)
        {
            var results = new List<PlayerSummary>();

            // A player id is an exact document id, so try a direct lookup first.
            var byIdTask = _db.Collection(ProfilesCollection).Document(query).GetSnapshotAsync();
            yield return new WaitUntil(() => byIdTask.IsCompleted);

            if (!byIdTask.IsFaulted && byIdTask.Result != null && byIdTask.Result.Exists)
            {
                var summary = ToSummary(byIdTask.Result);
                if (summary != null && summary.uid != _uid) results.Add(summary);
            }

            // Then a prefix match on the lowercased name. U+F8FF is the standard high code point
            // used to bound a Firestore prefix range.
            string lower = query.ToLowerInvariant();
            var nameQuery = _db.Collection(ProfilesCollection)
                .WhereGreaterThanOrEqualTo(FieldNameLower, lower)
                .WhereLessThanOrEqualTo(FieldNameLower, lower + HighCodePoint)
                .Limit(SearchLimit);

            var byNameTask = nameQuery.GetSnapshotAsync();
            yield return new WaitUntil(() => byNameTask.IsCompleted);

            if (byNameTask.IsFaulted)
            {
                string err = byNameTask.Exception?.Message ?? "Search failed";
                Debug.LogWarning($"[FriendsService] Name search failed: {err}");
                // An id hit is still a usable result, so only fail outright if we have nothing.
                if (results.Count == 0) { OnSearchFailed?.Invoke(err); yield break; }
            }
            else if (byNameTask.Result != null)
            {
                foreach (var doc in byNameTask.Result.Documents)
                {
                    if (doc.Id == _uid) continue;
                    if (results.Exists(r => r.uid == doc.Id)) continue;
                    var s = ToSummary(doc);
                    if (s != null) results.Add(s);
                }
            }

            OnSearchResults?.Invoke(results);
        }

        private static PlayerSummary ToSummary(DocumentSnapshot doc)
        {
            if (doc == null || !doc.Exists) return null;
            doc.TryGetValue<string>(FieldPlayerName, out string name);
            doc.TryGetValue<int>(FieldTrophies, out int trophies);

            long onlineAt = ReadUnixSeconds(doc, FieldOnlineAt);
            doc.TryGetValue<bool>(FieldInMatch, out bool inMatch);

            var arena = ArenaService.GetArenaForTrophies(trophies);
            bool online = IsOnline(onlineAt);

            return new PlayerSummary
            {
                uid = doc.Id,
                playerName = string.IsNullOrEmpty(name) ? "Player" : name,
                trophies = trophies,
                isOnline = online,
                // Only meaningful while they are actually online; a stale flag on an offline
                // player would otherwise read as "in a match" indefinitely.
                isInMatch = online && inMatch,
                arenaKey = ArenaService.ResolveArenaKey(arena),
                arenaName = arena != null ? arena.DisplayName : null
            };
        }

        // Firestore can hand a stored number back as long, int or double depending on how it was
        // written. A typed TryGetValue<long> can miss those, which reads as "never online".
        // Raw onlineAt per friend, kept only so the presence log can show what was actually read.
        private static readonly Dictionary<string, long> _lastReadOnlineAt = new Dictionary<string, long>();

        private static long ReadUnixSeconds(DocumentSnapshot doc, string field)
        {
            try
            {
                if (!doc.TryGetValue<object>(field, out object raw) || raw == null)
                {
                    if (field == FieldOnlineAt) _lastReadOnlineAt[doc.Id] = 0L;
                    return 0L;
                }

                long value;
                if (raw is Timestamp ts)
                {
                    // Server-stamped fields come back as Timestamp.
                    value = ts.ToDateTimeOffset().ToUnixTimeSeconds();
                }
                else
                {
                    // Legacy client-clock values already in the database.
                    value = Convert.ToInt64(raw);
                }

                if (field == FieldOnlineAt) _lastReadOnlineAt[doc.Id] = value;
                return value;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FriendsService] Could not read '{field}' on {doc.Id}: {ex.Message}");
                return 0L;
            }
        }

        // One-shot read of a single player's live profile. Cached list data can be up to a refresh
        // interval old, which is not good enough right before committing to hosting a match.
        public void FetchPlayerSummary(string uid, Action<PlayerSummary> onComplete)
        {
            if (!_ready || string.IsNullOrEmpty(uid)) { onComplete?.Invoke(null); return; }
            StartCoroutine(FetchPlayerSummaryCoroutine(uid, onComplete));
        }

        private IEnumerator FetchPlayerSummaryCoroutine(string uid, Action<PlayerSummary> onComplete)
        {
            var task = _db.Collection(ProfilesCollection).Document(uid).GetSnapshotAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || task.Result == null || !task.Result.Exists)
            {
                onComplete?.Invoke(null);
                yield break;
            }
            onComplete?.Invoke(ToSummary(task.Result));
        }

        // ---------- friend requests ----------

        public void SendFriendRequest(string toUid)
        {
            if (!_ready) { OnActionFailed?.Invoke("Not connected."); return; }
            if (string.IsNullOrEmpty(toUid) || toUid == _uid) { OnActionFailed?.Invoke("Invalid player."); return; }

            var data = new Dictionary<string, object>
            {
                { FieldFromName, LocalName() },
                { FieldSentAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
            };

            _db.Collection(RequestsCollection).Document(toUid)
               .Collection(SubIncoming).Document(_uid)
               .SetAsync(data)
               .ContinueWith(t =>
               {
                   if (t.IsFaulted) Debug.LogWarning($"[FriendsService] Friend request failed: {t.Exception?.Message}");
               });

            OnActionSucceeded?.Invoke("Friend request sent.");
        }

        public void RefreshRequests()
        {
            if (!_ready) return;
            StartCoroutine(RefreshRequestsCoroutine());
        }

        private IEnumerator RefreshRequestsCoroutine()
        {
            var task = _db.Collection(RequestsCollection).Document(_uid)
                          .Collection(SubIncoming).GetSnapshotAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            var list = new List<FriendRequestInfo>();
            if (!task.IsFaulted && task.Result != null)
            {
                foreach (var doc in task.Result.Documents)
                {
                    doc.TryGetValue<string>(FieldFromName, out string fromName);
                    doc.TryGetValue<long>(FieldSentAt, out long sentAt);
                    list.Add(new FriendRequestInfo
                    {
                        fromUid = doc.Id,
                        fromName = string.IsNullOrEmpty(fromName) ? "Player" : fromName,
                        sentAt = sentAt
                    });
                }
            }
            else if (task.IsFaulted)
            {
                Debug.LogWarning($"[FriendsService] Loading requests failed: {task.Exception?.Message}");
            }

            OnRequestsLoaded?.Invoke(list);
        }

        public void AcceptFriendRequest(string fromUid, string fromName)
        {
            if (!_ready || string.IsNullOrEmpty(fromUid)) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Both sides of the friendship are written so either player can list the other.
            _db.Collection(FriendsCollection).Document(_uid).Collection(SubList).Document(fromUid)
               .SetAsync(new Dictionary<string, object> { { FieldName, fromName ?? "Player" }, { FieldSince, now } });

            _db.Collection(FriendsCollection).Document(fromUid).Collection(SubList).Document(_uid)
               .SetAsync(new Dictionary<string, object> { { FieldName, LocalName() }, { FieldSince, now } });

            _db.Collection(RequestsCollection).Document(_uid).Collection(SubIncoming).Document(fromUid)
               .DeleteAsync();

            OnActionSucceeded?.Invoke("Friend added.");
            RefreshRequests();
            RefreshFriends();
        }

        public void DeclineFriendRequest(string fromUid)
        {
            if (!_ready || string.IsNullOrEmpty(fromUid)) return;
            _db.Collection(RequestsCollection).Document(_uid).Collection(SubIncoming).Document(fromUid)
               .DeleteAsync();
            OnActionSucceeded?.Invoke("Request declined.");
            RefreshRequests();
        }

        public void RemoveFriend(string friendUid)
        {
            if (!_ready || string.IsNullOrEmpty(friendUid)) return;
            _db.Collection(FriendsCollection).Document(_uid).Collection(SubList).Document(friendUid).DeleteAsync();
            _db.Collection(FriendsCollection).Document(friendUid).Collection(SubList).Document(_uid).DeleteAsync();
            OnActionSucceeded?.Invoke("Friend removed.");
            RefreshFriends();
        }

        // ---------- friends list ----------

        public void RefreshFriends()
        {
            if (!_ready) return;
            StartCoroutine(RefreshFriendsCoroutine());
        }

        private IEnumerator RefreshFriendsCoroutine()
        {
            var listTask = _db.Collection(FriendsCollection).Document(_uid)
                              .Collection(SubList).GetSnapshotAsync();
            yield return new WaitUntil(() => listTask.IsCompleted);

            var friends = new List<PlayerSummary>();
            if (listTask.IsFaulted || listTask.Result == null)
            {
                if (listTask.IsFaulted)
                    Debug.LogWarning($"[FriendsService] Loading friends failed: {listTask.Exception?.Message}");
                OnFriendsLoaded?.Invoke(friends);
                yield break;
            }

            var ids = new List<string>();
            foreach (var doc in listTask.Result.Documents) ids.Add(doc.Id);

            // Each friend's live name/trophies/presence lives on their profile, so read those.
            foreach (string id in ids)
            {
                var profTask = _db.Collection(ProfilesCollection).Document(id).GetSnapshotAsync();
                yield return new WaitUntil(() => profTask.IsCompleted);

                if (profTask.IsFaulted || profTask.Result == null || !profTask.Result.Exists) continue;
                var s = ToSummary(profTask.Result);
                if (s != null) friends.Add(s);
            }

            friends.Sort((a, b) =>
            {
                if (a.isOnline != b.isOnline) return b.isOnline.CompareTo(a.isOnline);
                return string.Compare(a.playerName, b.playerName, StringComparison.OrdinalIgnoreCase);
            });

            if (verbosePresenceLogging)
            {
                long now = ServerNowSeconds();
                foreach (var f in friends)
                {
                    long seen = _lastReadOnlineAt.TryGetValue(f.uid, out long v) ? v : 0L;
                    Debug.Log($"[FriendsService] presence {f.playerName}: onlineAt={seen} " +
                              $"age={(seen > 0 ? (now - seen).ToString() + "s" : "MISSING")} " +
                              $"window={OnlineWindowSeconds}s -> {(f.isOnline ? "ONLINE" : "offline")}");
                }
            }

            OnFriendsLoaded?.Invoke(friends);
        }

        // ---------- duo invites ----------

        // Confirms the write before reporting success. The previous fire-and-forget version claimed
        // "Invite sent." even when Firestore rejected it, which is why an empty duoInvites
        // collection looked like a delivery problem rather than a write problem.
        public void SendDuoInvite(string toUid, string roomName, string arenaKey, Action<bool, string> onComplete = null)
        {
            if (!_ready)
            {
                Debug.LogWarning("[FriendsService] Invite aborted: service not ready (Firestore unavailable or not signed in).");
                onComplete?.Invoke(false, "Not connected.");
                return;
            }
            if (string.IsNullOrEmpty(toUid) || string.IsNullOrEmpty(roomName))
            {
                onComplete?.Invoke(false, "Invalid invite.");
                return;
            }

            // An invite of ours is still live for this friend — resending would only overwrite the
            // one they are already looking at.
            if (!string.IsNullOrEmpty(GetOutgoingInviteRoom(toUid)))
            {
                onComplete?.Invoke(false, "They already have your invite.");
                return;
            }

            float cooldown = GetInviteCooldownRemaining(toUid);
            if (cooldown > 0f)
            {
                onComplete?.Invoke(false, $"Wait {Mathf.CeilToInt(cooldown)}s before inviting again.");
                return;
            }

            // Reserve the slots before the write so rapid repeat clicks cannot all pass the check
            // while the first request is still in flight.
            float now = Time.realtimeSinceStartup;
            _nextInviteAllowed = now + GlobalInviteCooldownSeconds;
            _nextInviteToFriend[toUid] = now + PerFriendInviteCooldownSeconds;

            StartCoroutine(SendDuoInviteCoroutine(toUid, roomName, arenaKey, onComplete));
        }

        private IEnumerator SendDuoInviteCoroutine(string toUid, string roomName, string arenaKey, Action<bool, string> onComplete)
        {
            var data = new Dictionary<string, object>
            {
                { FieldFromName, LocalName() },
                { FieldRoomName, roomName },
                { FieldArenaKey, arenaKey ?? string.Empty },
                // Server-stamped: a client clock ahead of the recipient's made every invite look
                // already expired, so the recipient deleted it on arrival.
                { FieldSentAt, FieldValue.ServerTimestamp }
            };

            string path = $"{InvitesCollection}/{toUid}/{SubIncoming}/{_uid}";
            Debug.Log($"[FriendsService] Writing invite to {path} (room={roomName})...");

            var task = _db.Collection(InvitesCollection).Document(toUid)
                          .Collection(SubIncoming).Document(_uid)
                          .SetAsync(data);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted)
            {
                // Almost always a security-rules rejection on the recipient's subtree.
                string msg = task.Exception?.GetBaseException()?.Message ?? "unknown error";
                Debug.LogError($"[FriendsService] Invite write REJECTED at {path}: {msg}");
                // Nothing was delivered, so do not hold the player on the long per-friend cooldown.
                _nextInviteToFriend.Remove(toUid);
                OnActionFailed?.Invoke("Invite could not be sent.");
                onComplete?.Invoke(false, msg);
                yield break;
            }

            Debug.Log($"[FriendsService] Invite write confirmed at {path}.");
            _outgoing[toUid] = new OutgoingInvite { roomName = roomName, sentAtRealtime = Time.realtimeSinceStartup };
            OnActionSucceeded?.Invoke("Invite sent.");
            onComplete?.Invoke(true, null);
        }

        private IEnumerator PruneStaleInvites()
        {
            var task = _db.Collection(InvitesCollection).Document(_uid)
                          .Collection(SubIncoming).GetSnapshotAsync();
            yield return new WaitUntil(() => task.IsCompleted);
            if (task.IsFaulted || task.Result == null) yield break;

            // Without a known clock offset we cannot age these safely; leave them for the listener.
            if (!_serverOffsetKnown) yield break;

            long now = ServerNowSeconds();
            int removed = 0;
            DuoInviteInfo mostRecent = null;

            foreach (var doc in task.Result.Documents)
            {
                long sentAt = ReadUnixSeconds(doc, FieldSentAt);
                if (sentAt <= 0) continue;
                if (now - sentAt <= InviteLifetimeSeconds) continue;

                doc.TryGetValue<string>(FieldFromName, out string fromName);
                var missed = new DuoInviteInfo
                {
                    fromUid = doc.Id,
                    fromName = string.IsNullOrEmpty(fromName) ? "A friend" : fromName,
                    sentAt = sentAt
                };
                // Several friends may have tried while we were away; only the latest is worth
                // interrupting the player about.
                if (mostRecent == null || missed.sentAt > mostRecent.sentAt) mostRecent = missed;

                doc.Reference.DeleteAsync();
                removed++;
            }

            if (removed > 0)
            {
                Debug.Log($"[FriendsService] Swept {removed} expired invite(s).");
                if (mostRecent != null) OnMissedInvite?.Invoke(mostRecent);
            }
        }

        public void ClearInviteFrom(string fromUid)
        {
            if (!_ready || string.IsNullOrEmpty(fromUid)) return;
            // Forget the dedupe entry too, so the same friend can invite again immediately.
            _lastInviteRoom.Remove(fromUid);
            _db.Collection(InvitesCollection).Document(_uid).Collection(SubIncoming).Document(fromUid).DeleteAsync();
        }

        private void StartListeners()
        {
            StopListeners();
            try
            {
                _requestListener = _db.Collection(RequestsCollection).Document(_uid).Collection(SubIncoming)
                    .Listen(_ => { _requestsDirty = true; });

                _inviteListener = _db.Collection(InvitesCollection).Document(_uid).Collection(SubIncoming)
                    .Listen(snapshot =>
                    {
                        if (snapshot == null) return;
                        foreach (var change in snapshot.GetChanges())
                        {
                            // Modified counts too: an invite document is keyed by sender, so a
                            // second invite from the same friend overwrites the existing document
                            // and arrives as Modified, not Added. Only reacting to Added meant a
                            // re-invite was never delivered.
                            if (change.ChangeType == DocumentChange.Type.Removed) continue;
                            var doc = change.Document;
                            if (doc == null || !doc.Exists) continue;

                            doc.TryGetValue<string>(FieldFromName, out string fromName);
                            doc.TryGetValue<string>(FieldRoomName, out string roomName);
                            long sentAt = ReadUnixSeconds(doc, FieldSentAt);

                            // A server timestamp is null on the local echo of a write, and the
                            // clock offset may not be known yet on the very first invite. Never
                            // discard an invite we cannot age reliably — show it instead.
                            // Firestore re-delivers a document on metadata changes (the server
                            // timestamp resolving, for one), so dedupe on the room name — every
                            // invite carries a fresh GUID room, making it a stable identity.
                            if (!string.IsNullOrEmpty(roomName))
                            {
                                if (_lastInviteRoom.TryGetValue(doc.Id, out string seenRoom) && seenRoom == roomName)
                                    continue;
                                _lastInviteRoom[doc.Id] = roomName;
                            }

                            bool ageKnown = sentAt > 0 && _serverOffsetKnown;
                            if (ageKnown && ServerNowSeconds() - sentAt > InviteLifetimeSeconds)
                            {
                                Debug.Log($"[FriendsService] Dropping expired invite from {doc.Id} " +
                                          $"(age {ServerNowSeconds() - sentAt}s).");
                                lock (_inviteLock)
                                {
                                    _pendingMissed.Enqueue(new DuoInviteInfo
                                    {
                                        fromUid = doc.Id,
                                        fromName = string.IsNullOrEmpty(fromName) ? "A friend" : fromName,
                                        sentAt = sentAt
                                    });
                                }
                                ClearInviteFrom(doc.Id);
                                continue;
                            }

                            lock (_inviteLock)
                            {
                                doc.TryGetValue<string>(FieldArenaKey, out string arenaKey);
                                _pendingInvites.Enqueue(new DuoInviteInfo
                                {
                                    fromUid = doc.Id,
                                    fromName = string.IsNullOrEmpty(fromName) ? "Player" : fromName,
                                    roomName = roomName,
                                    arenaKey = arenaKey,
                                    sentAt = sentAt
                                });
                            }
                        }
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FriendsService] Could not attach listeners: {ex.Message}");
            }
        }

        private void StopListeners()
        {
            try { _requestListener?.Stop(); } catch { }
            try { _inviteListener?.Stop(); } catch { }
            _requestListener = null;
            _inviteListener = null;
        }

        private void Update()
        {
            if (_requestsDirty)
            {
                _requestsDirty = false;
                RefreshRequests();
            }

            while (true)
            {
                DuoInviteInfo invite = null;
                lock (_inviteLock)
                {
                    if (_pendingInvites.Count > 0) invite = _pendingInvites.Dequeue();
                }
                if (invite == null) break;
                OnDuoInviteReceived?.Invoke(invite);
            }

            while (true)
            {
                DuoInviteInfo missed = null;
                lock (_inviteLock)
                {
                    if (_pendingMissed.Count > 0) missed = _pendingMissed.Dequeue();
                }
                if (missed == null) break;
                OnMissedInvite?.Invoke(missed);
            }
        }

        private static string LocalName()
        {
            string n = PlayerProfile.Data != null ? PlayerProfile.Data.playerName : null;
            if (string.IsNullOrEmpty(n)) n = FirebaseService.DisplayName;
            return string.IsNullOrEmpty(n) ? "Player" : n;
        }
    }
}
