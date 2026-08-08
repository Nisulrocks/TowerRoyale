using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using TR.Audio;
using TR.Net;
using TR.UI;
using TR.Systems;

namespace TR.Infrastructure
{
    public class LoadingScreen : MonoBehaviourPunCallbacks
    {
        [Header("Target Scene")] public string lobbySceneName = "Lobby";

        [Header("Company Splash (Video)")]
        public GameObject blackOverlay;
        [Tooltip("If null, auto-finds VideoPlayer on blackOverlay")]
        public VideoPlayer companyVideoPlayer;

        [Header("Game Splash UI")]
        public GameObject gameSplashScreen;

        [Header("Loading UI")]
        public Slider progressBar;
        public TMP_Text progressText;

        [Header("Timings")]
        public float fadeOutDuration = 0.35f;
        public float fadeInDuration = 0.35f;
        public float companyVideoFadeOut = 0.4f;
        public float gameSplashFadeIn = 0.4f;
        public float minTotalSplashTime = 2.0f;

        [Header("Firebase / Cloud Login")]
        [Tooltip("Assign the FirebaseConfig asset here.")]
        public FirebaseConfig firebaseConfig;
        [Tooltip("CloudLoginUI prefab shown during boot if not signed in.")]
        public GameObject cloudLoginUIPrefab;
        [Tooltip("Parent for the CloudLoginUI. If null, searches for a Canvas in the boot scene.")]
        public RectTransform cloudLoginUIParent;

        [Header("Internet Check")]
        [Tooltip("If enabled, the loader tries to connect to Photon to verify internet/service access before entering the Lobby.")]
        public bool checkInternetBeforeLoad = true;
        [Tooltip("Seconds to wait for a Photon connection before treating it as no internet.")]
        public float internetCheckTimeout = 8f;
        [Tooltip("Prefab to show when no internet is detected. If null, the message falls back to the progress text.")]
        public GameObject noInternetPopupPrefab;

        [Header("UI Audio")]
        [Tooltip("SFX Library key played when any UI button is clicked. Must match an entry key in Resources/SFX/SFXLibrary.")]
        [SerializeField] private string uiClickSfxKey = "ui_click";
        [Tooltip("Optional parent for the popup. If null, the loader searches for a Canvas in the Boot scene.")]
        public RectTransform noInternetPopupParent;
        [TextArea] public string noInternetMessage = "No internet connection detected.\nPlease check your network and try again.";

        private TaskCompletionSource<bool> _connectionTcs;

        private async void Start()
        {
            var fader = SceneFader.Instance;
            if (fader != null) fader.SetAlpha(0f);

            float splashStart = Time.unscaledTime;

            VideoPlayer vp = companyVideoPlayer;
            if (vp == null && blackOverlay != null)
            {
                vp = blackOverlay.GetComponent<VideoPlayer>();
                if (vp == null) vp = blackOverlay.GetComponentInChildren<VideoPlayer>(true);
            }

            bool hasVideo = vp != null && (vp.clip != null || !string.IsNullOrEmpty(vp.url));
            if (!hasVideo && vp != null)
                Debug.LogWarning("[LoadingScreen] VideoPlayer found but has no clip/url assigned.");

            if (blackOverlay != null)
            {
                if (!blackOverlay.activeSelf) blackOverlay.SetActive(true);
                var cg = GetOrAddCanvasGroup(blackOverlay);
                cg.alpha = 1f;
            }

            if (gameSplashScreen != null)
            {
                if (!gameSplashScreen.activeInHierarchy) gameSplashScreen.SetActive(true);
                var cg = GetOrAddCanvasGroup(gameSplashScreen);
                cg.alpha = 0f;
            }

            if (progressBar != null)
            {
                EnsureActiveHierarchy(progressBar.gameObject);
                progressBar.value = 0f;
                var cg = GetOrAddCanvasGroup(progressBar.gameObject);
                cg.alpha = 0f;
            }

            if (progressText != null)
            {
                EnsureActiveHierarchy(progressText.gameObject);
                progressText.text = "0%";
                var cg = GetOrAddCanvasGroup(progressText.gameObject);
                cg.alpha = 0f;
            }

            if (hasVideo)
            {
                Debug.Log("[LoadingScreen] Playing company video...");
                var tcs = new System.Threading.Tasks.TaskCompletionSource<object>();

                vp.loopPointReached += source => tcs.TrySetResult(null);
                vp.errorReceived += (source, msg) =>
                {
                    Debug.LogError($"[LoadingScreen] Video error: {msg}");
                    tcs.TrySetResult(null);
                };

                vp.Stop();
                vp.Play();

                await System.Threading.Tasks.Task.WhenAny(
                    tcs.Task,
                    System.Threading.Tasks.Task.Delay(30000)
                );
                Debug.Log("[LoadingScreen] Company video finished.");
            }
            else
            {
                Debug.Log("[LoadingScreen] No company video configured; skipping to splash.");
            }

            if (blackOverlay != null)
            {
                var cg = GetOrAddCanvasGroup(blackOverlay);
                await FadeCanvasGroup(cg, 1f, 0f, companyVideoFadeOut);
                blackOverlay.SetActive(false);
            }

            var op = SceneManager.LoadSceneAsync(lobbySceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;

            if (gameSplashScreen != null)
            {
                var cg = GetOrAddCanvasGroup(gameSplashScreen);
                await FadeCanvasGroup(cg, 0f, 1f, gameSplashFadeIn);
            }

            if (progressBar != null)
            {
                var cg = GetOrAddCanvasGroup(progressBar.gameObject);
                await FadeCanvasGroup(cg, 0f, 1f, gameSplashFadeIn);
            }

            if (progressText != null)
            {
                var cg = GetOrAddCanvasGroup(progressText.gameObject);
                await FadeCanvasGroup(cg, 0f, 1f, gameSplashFadeIn);
            }

            if (checkInternetBeforeLoad && progressText != null)
            {
                progressText.text = "Checking connection...";
            }

            if (checkInternetBeforeLoad && !await HasInternetConnection())
            {
                ShowNoInternetPopup();
                return;
            }

            // Keep watching the connection after boot so the same popup appears in Lobby / matches.
            NetworkConnectionMonitor.Initialize(noInternetPopupPrefab, SceneManager.GetActiveScene().name);

            // One shared click sound for every UI button, in every scene.
            TR.Audio.UIClickSfx.Initialize(uiClickSfxKey);

            // Initialize Firebase and handle cloud login before proceeding to lobby.
            // Anything that escapes here would abort the rest of Start(), so allowSceneActivation
            // below would never run and the player would sit on the splash forever.
            if (progressText != null) progressText.text = "Initializing cloud...";
            try
            {
                await InitializeFirebaseAndLogin();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LoadingScreen] Cloud init failed, continuing offline: {ex}");
            }

            float elapsed = Time.unscaledTime - splashStart;
            if (elapsed < minTotalSplashTime)
            {
                float wait = minTotalSplashTime - elapsed;
                float t = 0f;
                while (t < wait) { t += Time.unscaledDeltaTime; await Task.Yield(); }
            }

            while (op.progress < 0.9f)
            {
                OnProgress(Mathf.Clamp01(op.progress / 0.9f));
                await Task.Yield();
            }
            OnProgress(1f);

            if (fader != null) await fader.FadeOut(fadeOutDuration);
            if (fader != null) fader.ScheduleFadeInAfterSceneLoad(fadeInDuration);
            op.allowSceneActivation = true;
            while (!op.isDone) { await Task.Yield(); }
        }

        private void OnProgress(float p)
        {
            if (progressBar != null) progressBar.value = p;
            if (progressText != null) progressText.text = Mathf.RoundToInt(p * 100f) + "%";
        }

        private async Task InitializeFirebaseAndLogin()
        {
            if (firebaseConfig == null)
            {
                Debug.LogWarning("[LoadingScreen] No FirebaseConfig assigned; skipping cloud login.");
                return;
            }

            // Must exist before FirebaseService.Initialize(), because a restored session fires
            // OnSignInComplete from inside that call and the guard has to catch it.
            SessionGuardService.Initialize(noInternetPopupPrefab, "Boot");

            if (FirebaseService.Instance == null)
            {
                var go = new GameObject("FirebaseService");
                var svc = go.AddComponent<FirebaseService>();
                var configField = typeof(FirebaseService).GetField("config",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (configField != null)
                    configField.SetValue(svc, firebaseConfig);
                svc.Initialize();
            }

            // FirebaseService keeps its signed-in state across a scene reload and will not fire
            // OnSignInComplete a second time, so a reboot has to claim the account explicitly.
            // ClaimSession ignores this when the account is already claimed for this session.
            if (FirebaseService.IsSignedIn && !string.IsNullOrEmpty(FirebaseService.UserId))
            {
                SessionGuardService.Instance?.ClaimSession(FirebaseService.UserId);
            }

            if (CloudProfileService.Instance == null)
            {
                var go = new GameObject("CloudProfileService");
                go.AddComponent<CloudProfileService>();
            }

            if (LeaderboardService.Instance == null)
            {
                var lbGo = new GameObject("LeaderboardService");
                lbGo.AddComponent<LeaderboardService>();
            }

            if (FriendsService.Instance == null)
            {
                var frGo = new GameObject("FriendsService");
                frGo.AddComponent<FriendsService>();
            }

            // Must exist outside the Friends panel so invites arrive on any screen.
            TR.UI.DuoInviteListener.Initialize();

            // Same reason as the session claim above: a reboot will not re-fire OnSignInComplete.
            if (FirebaseService.IsSignedIn && !string.IsNullOrEmpty(FirebaseService.UserId))
            {
                FriendsService.Instance?.Initialize(FirebaseService.UserId);
            }


            await Task.Yield();

            if (CloudProfileService.Instance != null)
                CloudProfileService.Instance.Initialize();

            // FirebaseFirestore.DefaultInstance throws (not returns null) when Firebase failed to
            // initialize. Letting that escape aborts the rest of the loading flow, including the
            // cloud login prompt below, so degrade to offline instead.
            try
            {
                if (LeaderboardService.Instance != null
                    && FirestoreProvider.TryGet(out var firestore))
                {
                    LeaderboardService.Instance.Initialize(firestore);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LoadingScreen] Leaderboard unavailable: {ex.Message}");
            }

            await Task.Yield();

            if (FirebaseService.IsSignedIn)
            {
                if (progressText != null) progressText.text = "Loading cloud profile...";
                if (CloudProfileService.Instance != null && FirebaseService.UserId != null)
                    CloudProfileService.Instance.LoadProfile(FirebaseService.UserId);

                await WaitForCloudProfileLoad();
            }
            else
            {
                await ShowCloudLoginAndWait();
            }
        }

        private async Task WaitForCloudProfileLoad()
        {
            bool loaded = false;
            void OnLoaded() => loaded = true;
            PlayerProfile.OnCloudProfileLoaded += OnLoaded;
            try
            {
                float timeout = 10f;
                while (!loaded && timeout > 0f)
                {
                    timeout -= Time.unscaledDeltaTime;
                    await Task.Yield();
                }
            }
            finally
            {
                PlayerProfile.OnCloudProfileLoaded -= OnLoaded;
            }
        }

        private async Task ShowCloudLoginAndWait()
        {
            if (cloudLoginUIPrefab == null)
            {
                Debug.LogWarning("[LoadingScreen] No cloudLoginUIPrefab assigned; continuing as guest.");
                return;
            }

            var parent = cloudLoginUIParent != null ? cloudLoginUIParent : GetDefaultCanvasParent();
            var inst = Instantiate(cloudLoginUIPrefab, parent);
            CanvasGroup loginCg = null;
            if (inst != null)
            {
                if (inst.transform is RectTransform rect)
                {
                    rect.anchoredPosition = Vector2.zero;
                    rect.localScale = Vector3.one;
                }
                loginCg = GetOrAddCanvasGroup(inst);
                loginCg.alpha = 0f;
                inst.SetActive(true);
                await FadeCanvasGroup(loginCg, 0f, 1f, 0.4f);
            }

            bool loginResolved = false;
            bool signedIn = false;
            System.Action<string, string> onSignIn = (uid, name) => signedIn = true;
            System.Action onLoaded = () => loginResolved = true;
            System.Action onGuest = () => loginResolved = true;
            System.Action<string> onProfileFailed = (error) => loginResolved = true;

            FirebaseService.OnSignInComplete += onSignIn;
            PlayerProfile.OnCloudProfileLoaded += onLoaded;
            CloudProfileService.OnProfileLoadFailed += onProfileFailed;

            var loginUI = inst != null ? inst.GetComponent<CloudLoginUI>() : null;
            if (loginUI != null)
                loginUI.OnContinueAsGuest += onGuest;

            try
            {
                while (!loginResolved)
                {
                    await Task.Yield();
                }
            }
            finally
            {
                FirebaseService.OnSignInComplete -= onSignIn;
                PlayerProfile.OnCloudProfileLoaded -= onLoaded;
                CloudProfileService.OnProfileLoadFailed -= onProfileFailed;
                if (loginUI != null)
                    loginUI.OnContinueAsGuest -= onGuest;
            }

            if (inst != null)
            {
                if (loginCg != null)
                    await FadeCanvasGroup(loginCg, loginCg.alpha, 0f, 0.3f);
                Destroy(inst);
            }
        }

        private async Task Hold(float duration)
        {
            float d = Mathf.Max(0f, duration);
            float t = 0f;
            while (t < d) { t += Time.unscaledDeltaTime; await Task.Yield(); }
        }

        private async Task FadeImageAlpha(Image img, float from, float to, float duration)
        {
            if (img == null) return;
            float d = Mathf.Max(0.01f, duration);
            float t = 0f;
            var c = img.color;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / d);
                float a = Mathf.Lerp(from, to, u);
                c.a = a; img.color = c;
                await Task.Yield();
            }
            c.a = to; img.color = c;
        }

        private CanvasGroup GetOrAddCanvasGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        private void EnsureActiveHierarchy(GameObject go)
        {
            if (go == null) return;
            if (!go.activeInHierarchy)
            {
                go.SetActive(true);
                var p = go.transform.parent;
                while (p != null)
                {
                    if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
                    p = p.parent;
                }
            }
        }

        private async Task FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) return;
            float d = Mathf.Max(0.01f, duration);
            float t = 0f;
            cg.alpha = from;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / d);
                cg.alpha = Mathf.Lerp(from, to, u);
                await Task.Yield();
            }
            cg.alpha = to;
        }

        private async Task<bool> HasInternetConnection()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.Log("[LoadingScreen] No network interface detected.");
                return false;
            }

            if (PhotonNetwork.IsConnectedAndReady)
            {
                Debug.Log("[LoadingScreen] Already connected to Photon.");
                return true;
            }

            float effectiveTimeout = Mathf.Max(3f, internetCheckTimeout);
            _connectionTcs = new TaskCompletionSource<bool>();

            if (!PhotonNetwork.IsConnected)
            {
                Debug.Log("[LoadingScreen] Checking internet via Photon connection...");
                PhotonNetwork.ConnectUsingSettings();
            }
            else
            {
                Debug.Log("[LoadingScreen] Waiting for existing Photon connection attempt...");
            }

            var timeoutTask = Task.Delay((int)(effectiveTimeout * 1000));
            Task completed = await Task.WhenAny(_connectionTcs.Task, timeoutTask);

            if (completed != _connectionTcs.Task)
            {
                _connectionTcs.TrySetResult(false);
                if (PhotonNetwork.IsConnected)
                    PhotonNetwork.Disconnect();
                Debug.LogWarning("[LoadingScreen] Photon connection check timed out.");
                return false;
            }

            return await _connectionTcs.Task;
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log("[LoadingScreen] Photon connected to master.");
            _connectionTcs?.TrySetResult(true);
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (_connectionTcs == null || _connectionTcs.Task.IsCompleted)
                return;

            Debug.LogWarning($"[LoadingScreen] Photon disconnected during check: {cause}");
            _connectionTcs.TrySetResult(false);
        }

        private void ShowNoInternetPopup()
        {
            Debug.LogWarning("[LoadingScreen] No internet connection. Showing popup.");

            if (noInternetPopupPrefab != null)
            {
                var parent = noInternetPopupParent != null ? noInternetPopupParent : GetDefaultCanvasParent();
                var go = Instantiate(noInternetPopupPrefab, parent);
                go.SetActive(true);

                var popup = go.GetComponent<NoInternetPopup>();
                if (popup != null)
                {
                    popup.SetMessage(noInternetMessage);
                }
                else
                {
                    var txt = go.GetComponentInChildren<TMP_Text>(true);
                    if (txt != null) txt.text = noInternetMessage;
                }
                return;
            }

            // Fallback if no prefab is assigned: write the message into the progress text.
            if (progressText != null)
            {
                EnsureActiveHierarchy(progressText.gameObject);
                progressText.text = noInternetMessage;
                var cg = GetOrAddCanvasGroup(progressText.gameObject);
                cg.alpha = 1f;
            }
        }

        private RectTransform GetDefaultCanvasParent()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c != null && (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera))
                    return c.transform as RectTransform;
            }
            return transform as RectTransform;
        }
    }
}
