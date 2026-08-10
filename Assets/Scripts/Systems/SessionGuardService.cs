using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase.Firestore;
using TR.UI;

namespace TR.Systems
{
    public class SessionGuardService : MonoBehaviour
    {
        public static SessionGuardService Instance { get; private set; }

        private const string CollectionName = "profiles";
        private const string FieldSessionId = "activeSessionId";
        private const string FieldSessionAt = "activeSessionAt";
        private const string FieldSessionDevice = "activeSessionDevice";

        public static bool IsKicked { get; private set; }

        public static event Action OnSessionTakenOver;

        [SerializeField] private GameObject popupPrefab;
        [SerializeField] private string bootSceneName = "Boot";

        private FirebaseFirestore _db;
        private string _sessionId;
        private string _uid;
        private ListenerRegistration _listener;

        private volatile bool _takeoverPending;
        private bool _popupShown;
        private volatile bool _claimed;

        public static void Initialize(GameObject popup, string bootScene)
        {
            if (Instance == null)
            {
                var go = new GameObject("SessionGuardService");
                Instance = go.AddComponent<SessionGuardService>();
                DontDestroyOnLoad(go);
            }
            if (popup != null) Instance.popupPrefab = popup;
            if (!string.IsNullOrEmpty(bootScene)) Instance.bootSceneName = bootScene;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            FirebaseService.OnSignInComplete += HandleSignInComplete;
            FirebaseService.OnSignOut += HandleSignOut;
        }

        private void OnDestroy()
        {
            FirebaseService.OnSignInComplete -= HandleSignInComplete;
            FirebaseService.OnSignOut -= HandleSignOut;
            StopListening();
            if (Instance == this) Instance = null;
        }

        private void HandleSignInComplete(string uid, string displayName)
        {
            if (string.IsNullOrEmpty(uid)) return;
            ClaimSession(uid);
        }

        private void HandleSignOut()
        {
            StopListening();
            _uid = null;
            _sessionId = null;
            _claimed = false;
            IsKicked = false;
        }

        public void ClaimSession(string uid)
        {
            if (_uid == uid && _sessionId != null)
            {
                Debug.Log("[SessionGuard] Session already claimed for this account; ignoring repeat claim.");
                return;
            }

            if (_db == null && !FirestoreProvider.TryGet(out _db))
            {
                Debug.LogWarning("[SessionGuard] Firestore unavailable; single-session enforcement disabled.");
                return;
            }

            _uid = uid;
            _sessionId = Guid.NewGuid().ToString("N");
            _claimed = false;
            IsKicked = false;

            var docRef = _db.Collection(CollectionName).Document(uid);
            var data = new Dictionary<string, object>
            {
                { FieldSessionId, _sessionId },
                { FieldSessionAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                { FieldSessionDevice, SafeDeviceName() },
            };

            docRef.SetAsync(data, SetOptions.MergeAll).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogWarning($"[SessionGuard] Could not claim session: {task.Exception?.Message}");
                    return;
                }
                _claimed = true;
            });

            StartListening(docRef);
            Debug.Log($"[SessionGuard] Claimed account {uid} with session {_sessionId}.");
        }

        private void StartListening(DocumentReference docRef)
        {
            StopListening();
            try
            {
                _listener = docRef.Listen(snapshot =>
                {
                    if (snapshot == null || !snapshot.Exists) return;
                    if (!_claimed || IsKicked) return;

                    var meta = snapshot.Metadata;
                    if (meta != null && (meta.IsFromCache || meta.HasPendingWrites)) return;

                    if (!snapshot.TryGetValue<string>(FieldSessionId, out string remoteSession)) return;
                    if (string.IsNullOrEmpty(remoteSession)) return;

                    if (remoteSession != _sessionId)
                    {
                        _takeoverPending = true;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionGuard] Could not attach session listener: {ex.Message}");
            }
        }

        private void StopListening()
        {
            if (_listener == null) return;
            try { _listener.Stop(); }
            catch (Exception ex) { Debug.LogWarning($"[SessionGuard] Listener stop failed: {ex.Message}"); }
            _listener = null;
        }

        private void Update()
        {
            if (!_takeoverPending || _popupShown) return;
            _takeoverPending = false;
            HandleTakeover();
        }

        private void HandleTakeover()
        {
            if (_popupShown) return;
            _popupShown = true;
            IsKicked = true;

            Debug.LogWarning("[SessionGuard] Account claimed by another device; ending this session.");

            StopListening();
            OnSessionTakenOver?.Invoke();

            LeaveAnyActiveMatch();
            ShowPopup();
        }

        private void LeaveAnyActiveMatch()
        {
            try
            {
                TR.Net.DuoRejoinService.ClearActiveMatch();

                if (Photon.Pun.PhotonNetwork.InRoom)
                {
                    TR.Net.NetworkConnectionMonitor.Instance?.MarkIntentionalDisconnect();
                    Photon.Pun.PhotonNetwork.LeaveRoom();
                }
                MatchContext.Reset();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionGuard] Could not cleanly leave match: {ex.Message}");
            }
        }

        private void ShowPopup()
        {
            const string message = "Another device has logged into this account.\nThis session has been ended.";

            if (popupPrefab == null)
            {
                Debug.LogError("[SessionGuard] No popup prefab assigned; cannot show takeover notice.");
                return;
            }

            var parent = FindCanvasParent();
            if (parent == null)
            {
                Debug.LogWarning("[SessionGuard] No Canvas found to show takeover popup.");
                return;
            }

            var instance = Instantiate(popupPrefab, parent);
            instance.SetActive(true);
            instance.transform.SetAsLastSibling();

            var popup = instance.GetComponent<NoInternetPopup>();
            if (popup != null)
            {
                popup.SetMessage(message);
                popup.RetrySceneName = bootSceneName;
                popup.OnRetry += HandleRebootRequested;
                popup.OnQuit += RestoreTimeScale;
            }
            else
            {
                var txt = instance.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (txt != null) txt.text = message;
            }

            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        private float _savedTimeScale = 1f;

        private void RestoreTimeScale()
        {
            Time.timeScale = _savedTimeScale <= 0f ? 1f : _savedTimeScale;
        }

        private void HandleRebootRequested()
        {
            RestoreTimeScale();

            string uid = _uid;

            _popupShown = false;
            _takeoverPending = false;
            _claimed = false;
            _uid = null;
            _sessionId = null;
            IsKicked = false;

            if (!string.IsNullOrEmpty(uid))
            {
                Debug.Log("[SessionGuard] Reboot requested; reclaiming the account for this device.");
                ClaimSession(uid);
            }
        }

        private RectTransform FindCanvasParent()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c != null && (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera))
                    return c.transform as RectTransform;
            }
            return null;
        }

        private static string SafeDeviceName()
        {
            string name = SystemInfo.deviceName;
            return string.IsNullOrEmpty(name) || name == "<unknown>" ? Application.platform.ToString() : name;
        }
    }
}
