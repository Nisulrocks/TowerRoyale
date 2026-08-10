using UnityEngine;
using UnityEngine.SceneManagement;
using TR.Systems;
using TR.Net;

namespace TR.UI
{
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

            if (_deferredMissed != null)
            {
                var missed = _deferredMissed;
                _deferredMissed = null;
                HandleMissedInvite(missed);
            }
        }

        private static bool IsInBattle() => TR.Battle.BattleSceneController.Instance != null;

        private void HandleInvite(FriendsService.DuoInviteInfo invite)
        {
            if (invite == null) return;

            if (IsInBattle())
            {
                Debug.Log($"[DuoInviteListener] Holding {invite.fromName}'s invite until the match ends.");
                _deferred = invite;
                return;
            }

            if (TryResolveCrossedInvite(invite)) return;

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
            FriendsService.Instance?.ForgetOutgoingInvite(invite.fromUid);
            JoinFriendMatch(invite.roomName, asHost: false, invite.arenaKey);
        }

        private bool TryResolveCrossedInvite(FriendsService.DuoInviteInfo invite)
        {
            var friends = FriendsService.Instance;
            if (friends == null) return false;

            string myRoom = friends.GetOutgoingInviteRoom(invite.fromUid);
            if (string.IsNullOrEmpty(myRoom)) return false; 

            string myUid = FirebaseService.UserId;
            if (string.IsNullOrEmpty(myUid)) return false;

            bool iHost = string.Compare(myUid, invite.fromUid, System.StringComparison.Ordinal) < 0;

            if (iHost)
            {
                Debug.Log($"[DuoInviteListener] Crossed invite with {invite.fromName}: hosting ours ({myRoom}).");
                friends.ClearInviteFrom(invite.fromUid);
                return true;
            }

            Debug.Log($"[DuoInviteListener] Crossed invite with {invite.fromName}: joining theirs ({invite.roomName}).");
            friends.ForgetOutgoingInvite(invite.fromUid);
            Accept(invite);
            return true;
        }

        public static string LocalArenaKey()
        {
            var arena = ArenaService.GetArenaForTrophies(PlayerProfile.GetTrophies())
                        ?? ArenaService.GetCurrentArena();
            return ArenaService.ResolveArenaKey(arena);
        }

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
            _popup = null;
        }
    }
}
