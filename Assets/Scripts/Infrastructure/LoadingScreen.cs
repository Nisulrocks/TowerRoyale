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

        [Header("Internet Check")]
        [Tooltip("If enabled, the loader tries to connect to Photon to verify internet/service access before entering the Lobby.")]
        public bool checkInternetBeforeLoad = true;
        [Tooltip("Seconds to wait for a Photon connection before treating it as no internet.")]
        public float internetCheckTimeout = 8f;
        [Tooltip("Prefab to show when no internet is detected. If null, the message falls back to the progress text.")]
        public GameObject noInternetPopupPrefab;
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
