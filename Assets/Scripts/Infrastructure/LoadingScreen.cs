using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using TR.Audio;

namespace TR.Infrastructure
{
    public class LoadingScreen : MonoBehaviour
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
    }
}
