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
        [Tooltip("Optional second line showing '42%  ·  Loading audio'. If left empty the detail is folded into progressText instead.")]
        public TMP_Text statusText;

        [Header("Loading Bar Feel")]
        [Tooltip("How long a stage with no measurable sub-progress takes to creep most of the way across its share of the bar. Network waits have no real percentage, so the fill eases forward instead of freezing.")]
        [SerializeField] private float stageCreepSeconds = 2.5f;
        [Tooltip("Minimum bar travel per second, so the fill is always visibly moving.")]
        [SerializeField] private float barMinSpeed = 0.10f;

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
                progressBar.minValue = 0f;
                progressBar.maxValue = 1f;
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

            if (statusText != null)
            {
                EnsureActiveHierarchy(statusText.gameObject);
                statusText.text = string.Empty;
                var cg = GetOrAddCanvasGroup(statusText.gameObject);
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

            // Starts streaming now and keeps going underneath every stage below, feeding the
            // Lobby slice of the bar from Update() as it goes.
            var op = SceneManager.LoadSceneAsync(lobbySceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;
            _sceneOp = op;

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

            if (statusText != null)
            {
                var cg = GetOrAddCanvasGroup(statusText.gameObject);
                await FadeCanvasGroup(cg, 0f, 1f, gameSplashFadeIn);
            }

            BeginStage(BootStage.Connection);
            if (checkInternetBeforeLoad && !await HasInternetConnection())
            {
                _statusOverride = "No connection";
                RefreshProgressUI();
                _halted = true; // stop Update stomping the popup's fallback message
                ShowNoInternetPopup();
                return;
            }
            CompleteStage(BootStage.Connection);

            // Keep watching the connection after boot so the same popup appears in Lobby / matches.
            NetworkConnectionMonitor.Initialize(noInternetPopupPrefab, SceneManager.GetActiveScene().name);

            // One shared click sound for every UI button, in every scene.
            TR.Audio.UIClickSfx.Initialize(uiClickSfxKey);

            // Initialize Firebase and handle cloud login before proceeding to lobby.
            // Anything that escapes here would abort the rest of Start(), so allowSceneActivation
            // below would never run and the player would sit on the splash forever.
            try
            {
                await InitializeFirebaseAndLogin();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LoadingScreen] Cloud init failed, continuing offline: {ex}");
            }
            // Whatever happened in there, those stages are done with.
            CompleteStage(BootStage.Services);
            CompleteStage(BootStage.Account);
            CompleteStage(BootStage.Profile);

            // Card/rarity/pack/arena tables. This used to happen in the Lobby's Awake, where it
            // hitched the first frame; doing it here is both honest progress and a smoother entry.
            BeginStage(BootStage.GameData);
            await Task.Yield();
            GameDB.EnsureLoaded();
            CompleteStage(BootStage.GameData);

            await PreloadAudio();

            float elapsed = Time.unscaledTime - splashStart;
            if (elapsed < minTotalSplashTime)
            {
                float wait = minTotalSplashTime - elapsed;
                float t = 0f;
                while (t < wait) { t += Time.unscaledDeltaTime; await Task.Yield(); }
            }

            BeginStage(BootStage.Lobby, measurable: true);
            while (op.progress < 0.9f) await Task.Yield();
            CompleteStage(BootStage.Lobby);

            // Let the fill visibly finish its travel rather than cutting away part-way.
            _statusOverride = "Ready";
            await WaitForBarToReach(1f);

            if (fader != null) await fader.FadeOut(fadeOutDuration);
            if (fader != null) fader.ScheduleFadeInAfterSceneLoad(fadeInDuration);
            op.allowSceneActivation = true;
            while (!op.isDone) { await Task.Yield(); }
        }

        // ---------- real progress ----------

        // Every stage below is actual work the boot does. The bar is the weighted sum of how far
        // each one has got, so it reflects what is really happening instead of sitting at zero and
        // snapping to full at the end. Stages run concurrently where the work does: the Lobby
        // scene streams in the whole time the cloud calls are in flight, and contributes as it goes.
        private enum BootStage { Connection = 0, Services, Account, Profile, GameData, Audio, Lobby }

        private static readonly string[] StageLabels =
        {
            "Checking connection",
            "Connecting to services",
            "Checking account",
            "Loading your profile",
            "Loading game data",
            "Loading audio",
            "Loading lobby",
        };

        // Sums to 1.
        private static readonly float[] StageWeights = { 0.16f, 0.14f, 0.12f, 0.14f, 0.14f, 0.14f, 0.16f };

        private readonly float[] _stageProgress = new float[7];
        private int _activeStage = -1;
        private float _activeElapsed;
        private bool _activeMeasurable;
        private float _shown;
        private AsyncOperation _sceneOp;
        private string _statusOverride;
        private bool _halted;

        // Waiting on the player is not loading, so hold the fill still instead of creeping.
        private void FreezeStageCreep() => _activeMeasurable = true;

        private void BeginStage(BootStage stage, bool measurable = false)
        {
            _activeStage = (int)stage;
            _activeElapsed = 0f;
            _activeMeasurable = measurable;
        }

        private void ReportStage(BootStage stage, float fraction)
        {
            int i = (int)stage;
            // Never let a stage walk backwards; the bar only ever moves forward.
            _stageProgress[i] = Mathf.Max(_stageProgress[i], Mathf.Clamp01(fraction));
        }

        private void CompleteStage(BootStage stage)
        {
            _stageProgress[(int)stage] = 1f;
            if (_activeStage == (int)stage) _activeMeasurable = true; // stop creeping
        }

        private void Update()
        {
            // Boot gave up (no internet); leave the bar and whatever message is on screen alone.
            if (_halted) return;

            // The scene keeps streaming in behind everything else, so poll it every frame.
            if (_sceneOp != null) ReportStage(BootStage.Lobby, _sceneOp.progress / 0.9f);

            if (_activeStage >= 0 && !_activeMeasurable)
            {
                _activeElapsed += Time.unscaledDeltaTime;
                // A network round trip has no honest percentage, so ease toward — but never reach —
                // the end of this stage's band. Only the stage actually finishing fills it.
                float creep = 0.85f * (1f - Mathf.Exp(-_activeElapsed / Mathf.Max(0.1f, stageCreepSeconds)));
                ReportStage((BootStage)_activeStage, creep);
            }

            float target = 0f;
            for (int i = 0; i < _stageProgress.Length; i++) target += StageWeights[i] * _stageProgress[i];

            // Gap-proportional so big jumps catch up fast, with a floor so the fill never stalls.
            float speed = Mathf.Max(barMinSpeed, (target - _shown) * 4f);
            _shown = Mathf.MoveTowards(_shown, target, speed * Time.unscaledDeltaTime);

            RefreshProgressUI();
        }

        private void RefreshProgressUI()
        {
            // Floor, so it never reads 100% while there is still work left.
            int pct = Mathf.Clamp(Mathf.FloorToInt(_shown * 100f), 0, 100);
            string label = _statusOverride
                        ?? (_activeStage >= 0 ? StageLabels[_activeStage] : "Starting up");

            if (progressBar != null) progressBar.value = _shown;

            if (statusText != null)
            {
                if (progressText != null) progressText.text = pct + "%";
                statusText.text = pct + "%  ·  " + label;
            }
            else if (progressText != null)
            {
                progressText.text = pct + "%  ·  " + label;
            }
        }

        // Lets the fill actually travel to its target instead of the scene cutting away mid-slide.
        private async Task WaitForBarToReach(float value, float timeout = 2f)
        {
            while (_shown < value - 0.001f && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                await Task.Yield();
            }
        }

        private async Task InitializeFirebaseAndLogin()
        {
            if (firebaseConfig == null)
            {
                Debug.LogWarning("[LoadingScreen] No FirebaseConfig assigned; skipping cloud login.");
                return;
            }

            BeginStage(BootStage.Services);

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
            CompleteStage(BootStage.Services);

            BeginStage(BootStage.Account);
            if (FirebaseService.IsSignedIn)
            {
                CompleteStage(BootStage.Account);

                BeginStage(BootStage.Profile);
                if (CloudProfileService.Instance != null && FirebaseService.UserId != null)
                    CloudProfileService.Instance.LoadProfile(FirebaseService.UserId);

                await WaitForCloudProfileLoad();
                CompleteStage(BootStage.Profile);
            }
            else
            {
                // Sitting on the sign-in prompt is a wait on the player, not on us — say so rather
                // than letting the bar creep as if something were still loading.
                _statusOverride = "Waiting for sign-in";
                FreezeStageCreep();
                await ShowCloudLoginAndWait();
                _statusOverride = null;
                CompleteStage(BootStage.Account);
                CompleteStage(BootStage.Profile);
            }
        }

        // Pulls every SFX and music clip into memory here rather than letting the first play in the
        // Lobby hitch. Clip-by-clip, so this stage reports a genuine percentage.
        private async Task PreloadAudio()
        {
            BeginStage(BootStage.Audio, measurable: true);

            var clips = new System.Collections.Generic.List<AudioClip>();

            var lib = Resources.Load<TR.Audio.SFXLibrary>("SFX/SFXLibrary");
            if (lib != null && lib.entries != null)
            {
                foreach (var e in lib.entries)
                {
                    if (e == null || e.clips == null) continue;
                    foreach (var c in e.clips)
                        if (c != null && !clips.Contains(c)) clips.Add(c);
                }
            }

            // Only an existing manager: constructing one here would start boot music playing.
            var bgm = FindFirstObjectByType<TR.Audio.BGMManager>(FindObjectsInactive.Include);
            if (bgm != null)
            {
                if (bgm.defaultClip != null && !clips.Contains(bgm.defaultClip)) clips.Add(bgm.defaultClip);
                if (bgm.tracks != null)
                {
                    foreach (var t in bgm.tracks)
                        if (t != null && t.clip != null && !clips.Contains(t.clip)) clips.Add(t.clip);
                }
            }

            if (clips.Count == 0)
            {
                Debug.LogWarning("[LoadingScreen] No audio clips found to preload.");
                CompleteStage(BootStage.Audio);
                return;
            }

            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i].loadState == AudioDataLoadState.Unloaded) clips[i].LoadAudioData();
                ReportStage(BootStage.Audio, (i + 1) / (float)clips.Count * 0.5f);
                await Task.Yield();
            }

            // LoadAudioData is asynchronous for compressed clips, so the second half of this
            // stage is them actually landing in memory.
            float timeout = 5f;
            while (timeout > 0f)
            {
                int ready = 0;
                for (int i = 0; i < clips.Count; i++)
                    if (clips[i].loadState != AudioDataLoadState.Loading) ready++;

                ReportStage(BootStage.Audio, 0.5f + ready / (float)clips.Count * 0.5f);
                if (ready >= clips.Count) break;

                timeout -= Time.unscaledDeltaTime;
                await Task.Yield();
            }

            CompleteStage(BootStage.Audio);
            Debug.Log($"[LoadingScreen] Preloaded {clips.Count} audio clip(s).");
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
