using UnityEngine;
using UnityEngine.SceneManagement;
using TR.Systems;
using TR.Net;

namespace TR.UI
{
    // Delivers duo invites anywhere in the game, not just while the Friends panel happens to be
    // open. FriendsPanelUI used to be the only subscriber, so an invite that arrived while the
    // player was on any other tab was raised to nobody and silently expired.
    public class DuoInviteListener : MonoBehaviour
    {
        private const string PopupResourcePath = "UI/DuoInviteNotif";

        public static DuoInviteListener Instance { get; private set; }

        private DuoInviteNotificationUI _popup;

        public static void Initialize()
        {
            if (Instance != null) return;
            var go = new GameObject("DuoInviteListener");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DuoInviteListener>();
        }

        private void OnEnable()
        {
            FriendsService.OnDuoInviteReceived += HandleInvite;
            FriendsService.OnMissedInvite += HandleMissedInvite;
        }

        private void OnDisable()
        {
            FriendsService.OnDuoInviteReceived -= HandleInvite;
            FriendsService.OnMissedInvite -= HandleMissedInvite;
        }

        private FriendsService.DuoInviteInfo _deferredMissed;

        private void HandleMissedInvite(FriendsService.DuoInviteInfo missed)
        {
            if (missed == null) return;

            if (IsInBattle())
            {
                _deferredMissed = missed;
                return;
            }

            var popup = EnsurePopup();
            if (popup == null)
            {
                Debug.Log($"[DuoInviteListener] Missed a Duo invite from {missed.fromName}.");
                return;
            }
            popup.ShowMissed(missed, InviteBack);
        }

        // The original room is long gone, so "accept" on a missed invite means starting a fresh
        // match and inviting them to it. The invite could be minutes or hours old, so check their
        // live status first — hosting a room for someone who has since logged off would leave the
        // player waiting alone for an invite nobody will ever see.
        private void InviteBack(FriendsService.DuoInviteInfo missed)
        {
            if (missed == null || FriendsService.Instance == null) return;

            FriendsService.Instance.FetchPlayerSummary(missed.fromUid, summary =>
            {
                var popup = EnsurePopup();

                if (summary == null || !summary.isOnline)
                {
                    if (popup != null) popup.ShowNotice($"{missed.fromName} is offline right now.");
                    else Debug.Log($"[DuoInviteListener] {missed.fromName} is offline; invite back skipped.");
                    return;
                }

                if (summary.isInMatch)
                {
                    if (popup != null) popup.ShowNotice($"{missed.fromName} is already in a match.");
                    return;
                }

                if (!summary.IsSameArenaAsLocal)
                {
                    string where = string.IsNullOrEmpty(summary.arenaName) ? "another arena" : summary.arenaName;
                    if (popup != null) popup.ShowNotice($"{missed.fromName} is in {where} — you can only duo within the same arena.");
                    return;
                }

                string roomName = DuoNetworkManager.NewFriendRoomName();
                string arenaKey = LocalArenaKey();
                if (string.IsNullOrEmpty(arenaKey)) return;

                FriendsService.Instance.SendDuoInvite(missed.fromUid, roomName, arenaKey, (ok, error) =>
                {
                    if (!ok)
                    {
                        if (popup != null) popup.ShowNotice($"Couldn't invite {missed.fromName}: {error}");
                        return;
                    }
                    JoinFriendMatch(roomName, asHost: true, arenaKey);
                });
            });
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private FriendsService.DuoInviteInfo _deferred;

        private void Update()
        {
            if (IsInBattle()) return;

            if (_deferred != null)
            {
                var invite = _deferred;
                _deferred = null;
                HandleInvite(invite);
                return;
            }

            // Live invites take priority; a missed one is only information.
            if (_deferredMissed != null)
            {
                var missed = _deferredMissed;
                _deferredMissed = null;
                HandleMissedInvite(missed);
            }
        }

        // Deliberately narrower than MatchContext.IsMatchInProgress, which is also true while merely
        // sitting in a Photon room. Nothing leaves the room when a duo match ends, so using that
        // here meant every later invite was treated as "busy" and deleted. Starting a friend match
        // already leaves a stale room on its own.
        private static bool IsInBattle() => TR.Battle.BattleSceneController.Instance != null;

        private void HandleInvite(FriendsService.DuoInviteInfo invite)
        {
            if (invite == null) return;

            // Hold it rather than destroy it — it is shown as soon as the battle is over.
            if (IsInBattle())
            {
                Debug.Log($"[DuoInviteListener] Holding {invite.fromName}'s invite until the match ends.");
                _deferred = invite;
                return;
            }

            // Crossed invites: both friends invited each other at almost the same moment, so both
            // are now hosting their own room. Left alone they would each join the other's room and
            // end up separated. Resolve it with a rule both clients evaluate identically — the
            // lower uid keeps hosting — so no extra handshake is needed.
            if (TryResolveCrossedInvite(invite)) return;

            // Arena-locked duo: if the sender's arena is not ours the match cannot be played, so do
            // not offer it. Trophies can move a player between arenas after an invite was sent.
            string localArena = ArenaService.GetLocalArenaKey();
            if (!string.IsNullOrEmpty(invite.arenaKey) && invite.arenaKey != localArena)
            {
                Debug.Log($"[DuoInviteListener] Invite from {invite.fromName} ignored: " +
                          $"arena {invite.arenaKey} != local {localArena}.");
                FriendsService.Instance?.ClearInviteFrom(invite.fromUid);
                return;
            }

            var popup = EnsurePopup();
            if (popup == null)
            {
                Debug.LogWarning($"[DuoInviteListener] No popup available; {invite.fromName}'s invite was dropped.");
                return;
            }

            popup.Show(invite, Accept);
        }

        // The popup is rebuilt whenever the scene it lived in is gone.
        private DuoInviteNotificationUI EnsurePopup()
        {
            if (_popup != null) return _popup;

            var prefab = Resources.Load<GameObject>(PopupResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[DuoInviteListener] Missing prefab at Resources/{PopupResourcePath}. " +
                               "Run TR/UI/Build Friends Prefabs.");
                return null;
            }

            var parent = FindCanvasParent();
            if (parent == null) return null;

            var instance = Instantiate(prefab, parent);
            instance.SetActive(false);
            _popup = instance.GetComponent<DuoInviteNotificationUI>();
            return _popup;
        }

        private static RectTransform FindCanvasParent()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Canvas best = null;
            foreach (var c in canvases)
            {
                if (c == null) continue;
                if (c.renderMode != RenderMode.ScreenSpaceOverlay && c.renderMode != RenderMode.ScreenSpaceCamera) continue;
                if (best == null || c.sortingOrder > best.sortingOrder) best = c;
            }
            return best != null ? best.transform as RectTransform : null;
        }

        public void Accept(FriendsService.DuoInviteInfo invite)
        {
            if (invite == null || string.IsNullOrEmpty(invite.roomName)) return;
            FriendsService.Instance?.ClearInviteFrom(invite.fromUid);
            // We are joining them, so any invite of ours to the same friend is moot. Leaving it
            // would let a later stale entry look like a crossed invite.
            FriendsService.Instance?.ForgetOutgoingInvite(invite.fromUid);
            // Use the host's arena, not ours — otherwise two friends on different trophy counts
            // would try to load different battle scenes for the same room.
            JoinFriendMatch(invite.roomName, asHost: false, invite.arenaKey);
        }

        // Returns true when the invite was consumed as a crossed-invite resolution.
        private bool TryResolveCrossedInvite(FriendsService.DuoInviteInfo invite)
        {
            var friends = FriendsService.Instance;
            if (friends == null) return false;

            string myRoom = friends.GetOutgoingInviteRoom(invite.fromUid);
            if (string.IsNullOrEmpty(myRoom)) return false; // no invite of ours outstanding to them

            string myUid = FirebaseService.UserId;
            if (string.IsNullOrEmpty(myUid)) return false;

            // Ordinal compare on uid: each side compares (self, other) and gets the opposite sign,
            // so exactly one of the two resolves as host.
            bool iHost = string.Compare(myUid, invite.fromUid, System.StringComparison.Ordinal) < 0;

            if (iHost)
            {
                // Keep hosting our room and drop theirs. By the same rule they will abandon their
                // room and join ours, so there is nothing to prompt the player about.
                Debug.Log($"[DuoInviteListener] Crossed invite with {invite.fromName}: hosting ours ({myRoom}).");
                friends.ClearInviteFrom(invite.fromUid);
                return true;
            }

            // Their room wins. We already expressed intent by inviting them, so join immediately
            // rather than asking again — StartFriendMatch leaves our own room on the way.
            Debug.Log($"[DuoInviteListener] Crossed invite with {invite.fromName}: joining theirs ({invite.roomName}).");
            friends.ForgetOutgoingInvite(invite.fromUid);
            Accept(invite);
            return true;
        }

        // The arena key the host chose. Falls back to this client's own arena only when the invite
        // did not carry one (older invite documents).
        public static string LocalArenaKey()
        {
            var arena = ArenaService.GetArenaForTrophies(PlayerProfile.GetTrophies())
                        ?? ArenaService.GetCurrentArena();
            return ArenaService.ResolveArenaKey(arena);
        }

        // Shared by the invite popup (joining) and the Friends panel (hosting) so both paths resolve
        // the arena and scene the same way.
        public static void JoinFriendMatch(string roomName, bool asHost, string arenaKey)
        {
            if (string.IsNullOrEmpty(roomName)) return;
            if (DuoNetworkManager.Instance == null)
            {
                Debug.LogWarning("[DuoInviteListener] No DuoNetworkManager; cannot start friend match.");
                return;
            }

            if (string.IsNullOrEmpty(arenaKey)) arenaKey = LocalArenaKey();

            string sceneName = ArenaService.GetBattleSceneName(arenaKey);
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[DuoInviteListener] Could not resolve a battle scene for this match.");
                return;
            }

            var arena = ArenaService.GetArenaForTrophies(PlayerProfile.GetTrophies());

            MatchContext.Mode = GameMode.Duo;
            MatchContext.ArenaId = arenaKey;

            Debug.Log($"[DuoInviteListener] Friend match: room={roomName} host={asHost} arena={arenaKey} scene={sceneName}");

            DuoNetworkManager.Instance.StartFriendMatch(
                roomName,
                asHost,
                arenaKey,
                PlayerProfile.GetTrophies(),
                PlayerProfile.GetCastleLevel(),
                sceneName,
                arena != null ? arena.DisplayName : null,
                PlayerProfile.Data != null ? PlayerProfile.Data.playerName : null);
        }

        private void OnLevelWasLoaded(int level)
        {
            // The popup lived on the previous scene's canvas.
            _popup = null;
        }
    }
}
