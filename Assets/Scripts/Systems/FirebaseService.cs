using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase;
using Firebase.Auth;

namespace TR.Systems
{
    public class FirebaseService : MonoBehaviour
    {
        public static FirebaseService Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private FirebaseConfig config;

        public static bool IsSignedIn { get; private set; }

        // Guests never authenticate at all, so there is no uid to key cloud data by. Anything that
        // needs an account (friends, cloud saves, leaderboard placement) must check this.
        public static bool IsGuest => !IsSignedIn || string.IsNullOrEmpty(UserId);
        public static string UserId { get; private set; }
        public static string DisplayName { get; private set; }
        public static string Email { get; private set; }

        public static event Action<string, string> OnSignInComplete;
        public static event Action<string> OnSignInFailed;
        public static event Action OnSignOut;

        private static bool _initialized = false;
        private string _redirectUri => $"http://localhost:{(config != null ? config.redirectPort : 5050)}/";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized) return;
            if (config == null)
            {
                Debug.LogError("[FirebaseService] No FirebaseConfig assigned.");
                return;
            }

            try
            {
                var firebaseApp = ResolveDefaultApp();
                if (firebaseApp == null)
                {
                    Debug.LogError("[FirebaseService] Could not create a Firebase app. Check FirebaseConfig values.");
                    return;
                }

                _initialized = true;
                Debug.Log("[FirebaseService] Firebase initialized successfully.");

                TryRestoreSession();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirebaseService] Initialization failed: {ex}");
            }
        }

        // On platforms where the Firebase plugin bundles google-services.json (Android/iOS),
        // DefaultInstance resolves on its own. On desktop there is no such file, and
        // DefaultInstance *throws* rather than returning null — so the AppOptions path below
        // has to be reached via catch, not via a null check.
        private FirebaseApp ResolveDefaultApp()
        {
            try
            {
                var app = FirebaseApp.DefaultInstance;
                if (app != null) return app;
            }
            catch (Exception ex)
            {
                Debug.Log($"[FirebaseService] No bundled Firebase config ({ex.GetType().Name}); creating app from FirebaseConfig.");
            }

            if (string.IsNullOrEmpty(config.apiKey) || string.IsNullOrEmpty(config.projectId) || string.IsNullOrEmpty(config.appId))
            {
                Debug.LogError("[FirebaseService] FirebaseConfig is missing apiKey, projectId or appId.");
                return null;
            }

            return FirebaseApp.Create(new AppOptions
            {
                ApiKey = config.apiKey,
                ProjectId = config.projectId,
                StorageBucket = config.storageBucket,
                AppId = config.appId,
            });
        }

        private void TryRestoreSession()
        {
            try
            {
                var auth = FirebaseAuth.GetAuth(FirebaseApp.DefaultInstance);
                if (auth.CurrentUser != null)
                {
                    IsSignedIn = true;
                    UserId = auth.CurrentUser.UserId;
                    DisplayName = auth.CurrentUser.DisplayName;
                    Email = auth.CurrentUser.Email;
                    Debug.Log($"[FirebaseService] Restored session for {DisplayName} ({UserId})");
                    OnSignInComplete?.Invoke(UserId, DisplayName);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirebaseService] Session restore check failed: {ex}");
            }
        }

        public void SignInWithGoogle()
        {
            // Signing in restarts from Boot, which would tear down a running match.
            if (MatchContext.IsMatchInProgress)
            {
                Debug.LogWarning("[FirebaseService] Sign-in blocked: a match is in progress.");
                OnSignInFailed?.Invoke("You can't sign in during a match.");
                return;
            }

            if (!_initialized)
            {
                OnSignInFailed?.Invoke("Firebase not initialized.");
                return;
            }
            if (config == null || string.IsNullOrEmpty(config.webClientId))
            {
                OnSignInFailed?.Invoke("Web Client ID not configured.");
                return;
            }

            StartCoroutine(OAuthSignInCoroutine());
        }

        private IEnumerator OAuthSignInCoroutine()
        {
            Debug.Log("[FirebaseService] Starting Google OAuth flow...");
            string idToken = null;
            string oauthError = null;

            yield return GoogleOAuthHandler.StartOAuthFlow(
                config.webClientId,
                config.clientSecret,
                _redirectUri,
                config.scopes,
                token => idToken = token,
                error => oauthError = error);

            if (!string.IsNullOrEmpty(oauthError))
            {
                Debug.LogError($"[FirebaseService] OAuth failed: {oauthError}");
                OnSignInFailed?.Invoke(oauthError);
                yield break;
            }

            if (string.IsNullOrEmpty(idToken))
            {
                OnSignInFailed?.Invoke("No ID token received from OAuth.");
                yield break;
            }

            var auth = FirebaseAuth.GetAuth(FirebaseApp.DefaultInstance);
            var credential = GoogleAuthProvider.GetCredential(idToken, null);

            var signInTask = auth.SignInWithCredentialAsync(credential);
            yield return new WaitUntil(() => signInTask.IsCompleted);

            if (signInTask.IsFaulted)
            {
                string errorMsg = "Sign-in failed.";
                if (signInTask.Exception != null)
                {
                    foreach (var inner in signInTask.Exception.InnerExceptions)
                        errorMsg += $" {inner.Message}";
                }
                Debug.LogError($"[FirebaseService] {errorMsg}");
                OnSignInFailed?.Invoke(errorMsg);
                yield break;
            }

            var user = signInTask.Result;
            IsSignedIn = true;
            UserId = user.UserId;
            DisplayName = user.DisplayName;
            Email = user.Email;
            Debug.Log($"[FirebaseService] Signed in as {DisplayName} ({UserId})");
            OnSignInComplete?.Invoke(UserId, DisplayName);

            // Signing in from inside the game (guest -> account) leaves everything still running on
            // the guest's local profile: the cloud profile was never downloaded, and services that
            // key off a uid were started without one. Boot is what performs that load, so re-run it.
            // During boot itself the login flow is already in progress, so nothing to do.
            if (SceneManager.GetActiveScene().name != BootSceneName)
            {
                Debug.Log("[FirebaseService] Mid-session sign-in; restarting from Boot to load the account.");
                TR.UI.PanelSwitcher.ClearReturnPanel();
                SceneManager.LoadScene(BootSceneName);
            }
        }

        private const string BootSceneName = "Boot";

        // Returns false when the sign-out was refused. Guarding here rather than only in the UI
        // means every caller is covered, including DeveloperTools.
        public bool SignOut()
        {
            if (MatchContext.IsMatchInProgress)
            {
                Debug.LogWarning("[FirebaseService] Sign-out blocked: a match is in progress.");
                return false;
            }

            if (!_initialized) return false;

            try
            {
                var auth = FirebaseAuth.GetAuth(FirebaseApp.DefaultInstance);
                auth.SignOut();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirebaseService] SignOut error: {ex}");
            }

            IsSignedIn = false;
            UserId = null;
            DisplayName = null;
            Email = null;

            PlayerProfile.WipeAllData();
            _initialized = false;
            TR.UI.PanelSwitcher.ClearReturnPanel();

            Debug.Log("[FirebaseService] Signed out. Returning to boot scene.");
            OnSignOut?.Invoke();

            if (CloudProfileService.Instance != null)
                Destroy(CloudProfileService.Instance.gameObject);

            if (TR.Tutorial.TutorialManager.Instance != null)
                Destroy(TR.Tutorial.TutorialManager.Instance.gameObject);

            Instance = null;
            Destroy(gameObject);
            SceneManager.LoadScene("Boot");
            return true;
        }
    }
}
