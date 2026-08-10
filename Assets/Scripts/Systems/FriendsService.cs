using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;

namespace TR.Systems
{
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

        private const string HighCodePoint = "\uf8ff";

        private const int InviteLifetimeSeconds = 120;

        private const float GlobalInviteCooldownSeconds = 4f;
        private const float PerFriendInviteCooldownSeconds = 30f;

        private const int OnlineWindowSeconds = 45;
        private const float HeartbeatSeconds = 15f;
        private const int SearchLimit = 20;

        [SerializeField] private bool verbosePresenceLogging = true;

        public class PlayerSummary
        {
            public string uid;
            public string playerName;
            public int trophies;
            public bool isOnline;

            public bool isInMatch;

            public string arenaKey;
            public string arenaName;

            public bool IsSameArenaAsLocal =>
                !string.IsNullOrEmpty(arenaKey) && arenaKey == ArenaService.GetLocalArenaKey();

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
            public string arenaKey;
            public long sentAt;
        }

        public static event Action<List<PlayerSummary>> OnSearchResults;
        public static event Action<string> OnSearchFailed;
        public static event Action<List<PlayerSummary>> OnFriendsLoaded;
        public static event Action<List<FriendRequestInfo>> OnRequestsLoaded;
        public static event Action<DuoInviteInfo> OnDuoInviteReceived;

        public static event Action<DuoInviteInfo> OnMissedInvite;
        public static event Action<string> OnActionFailed;
        public static event Action<string> OnActionSucceeded;

        private FirebaseFirestore _db;
        private bool _ready;
        private string _uid;
        private ListenerRegistration _requestListener;
        private ListenerRegistration _inviteListener;
        private Coroutine _heartbeat;

        private readonly Dictionary<string, string> _lastInviteRoom = new Dictionary<string, string>();

        private class OutgoingInvite
        {
            public string roomName;
            public float sentAtRealtime;
        }
        private readonly Dictionary<string, OutgoingInvite> _outgoing = new Dictionary<string, OutgoingInvite>();

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

        public float GetInviteCooldownRemaining(string toUid)
        {
            float now = Time.realtimeSinceStartup;
            float remaining = Mathf.Max(0f, _nextInviteAllowed - now);
            if (!string.IsNullOrEmpty(toUid) && _nextInviteToFriend.TryGetValue(toUid, out float perFriend))
                remaining = Mathf.Max(remaining, perFriend - now);
            return Mathf.Max(0f, remaining);
        }

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


        private IEnumerator HeartbeatLoop()
        {
            while (true)
            {
                WritePresence();

                if (!_serverOffsetKnown || Time.unscaledTime >= _nextClockSync)
                {
                    _nextClockSync = Time.unscaledTime + 120f;
                    yield return new WaitForSecondsRealtime(2f);
                    yield return SyncServerClock();
                }

                yield return new WaitForSecondsRealtime(HeartbeatSeconds);
            }
        }

        private void WritePresence()
        {
            if (!_ready || string.IsNullOrEmpty(_uid)) return;
            if (SessionGuardService.IsKicked) return;

            var data = new Dictionary<string, object>
            {
                { FieldOnlineAt, FieldValue.ServerTimestamp },
                { FieldInMatch, IsLocallyBusy() }
            };
            _db.Collection(ProfilesCollection).Document(_uid).SetAsync(data, SetOptions.MergeAll)
               .ContinueWith(t =>
               {
                   if (t.IsFaulted)
                       Debug.LogError($"[FriendsService] Presence write REJECTED for {_uid}: " +
                                      $"{t.Exception?.GetBaseException()?.Message}");
                   else if (verbosePresenceLogging)
                       Debug.Log("[FriendsService] presence heartbeat written (server timestamp).");
               });
        }

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
            return age <= OnlineWindowSeconds;
        }


        private static long _serverOffsetSeconds;
        private static bool _serverOffsetKnown;
        private float _nextClockSync;

        private static long LocalNowSeconds() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static long ServerNowSeconds()
            => LocalNowSeconds() + (_serverOffsetKnown ? _serverOffsetSeconds : 0L);

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

            var byIdTask = _db.Collection(ProfilesCollection).Document(query).GetSnapshotAsync();
            yield return new WaitUntil(() => byIdTask.IsCompleted);

            if (!byIdTask.IsFaulted && byIdTask.Result != null && byIdTask.Result.Exists)
            {
                var summary = ToSummary(byIdTask.Result);
                if (summary != null && summary.uid != _uid) results.Add(summary);
            }

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
                isInMatch = online && inMatch,
                arenaKey = ArenaService.ResolveArenaKey(arena),
                arenaName = arena != null ? arena.DisplayName : null
            };
        }

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
                    value = ts.ToDateTimeOffset().ToUnixTimeSeconds();
                }
                else
                {
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
                string msg = task.Exception?.GetBaseException()?.Message ?? "unknown error";
                Debug.LogError($"[FriendsService] Invite write REJECTED at {path}: {msg}");
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
                            if (change.ChangeType == DocumentChange.Type.Removed) continue;
                            var doc = change.Document;
                            if (doc == null || !doc.Exists) continue;

                            doc.TryGetValue<string>(FieldFromName, out string fromName);
                            doc.TryGetValue<string>(FieldRoomName, out string roomName);
                            long sentAt = ReadUnixSeconds(doc, FieldSentAt);

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
