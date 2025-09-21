using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using TR.Audio;

namespace TR.Infrastructure
{
    // Attach this to an empty GameObject in the Boot/Loading scene.
    // It will fade in from black, load the Lobby scene asynchronously with progress,
    // and then fade into the Lobby.
    public class LoadingScreen : MonoBehaviour
    {
        [Header("Target Scene")] public string lobbySceneName = "Lobby";
        [Header("UI (optional)")]
        public Slider progressBar; // optional
        public TMP_Text progressText; // optional
        [Tooltip("A full-screen black Image used as the initial background. Anchor it to stretch fullscreen.")]
        public Image blackOverlay; // required for the requested sequence
        [Header("Splash Text (optional)")]
        public TMP_Text companyNameText; // fades in then out
        public TMP_Text gameNameText; // fades in then out
        [Header("SFX (optional)")]
        [Tooltip("SFX key to play (via SFXManager) right when the game name appears")]
        public string gameNameSfxKey = "";
        [Header("Timings")] 
        public float fadeOutDuration = 0.35f; 
        public float fadeInDuration = 0.35f;
        public float companyFadeIn = 0.4f, companyHold = 0.8f, companyFadeOut = 0.4f;
        public float gameFadeIn = 0.4f, gameHold = 0.9f, gameFadeOut = 0.4f;
        public float minTotalSplashTime = 2.0f; // ensure splash is visible even if loading is instant

        private async void Start()
        {
            // Ensure fader exists (we won't use it for the initial black; only for final scene transition)
            var fader = SceneFader.Instance;
            fader.SetAlpha(0f); // keep fader transparent; we'll use blackOverlay for the intro

            // Ensure base text colors are opaque; we use CanvasGroup to control visibility
            SetTextAlpha(companyNameText, 1f);
            SetTextAlpha(gameNameText, 1f);
            // Prepare black overlay to solid black (no hierarchy reordering)
            if (blackOverlay != null)
            {
                var c = blackOverlay.color; c.a = 1f; blackOverlay.color = c;
                if (!blackOverlay.gameObject.activeSelf) blackOverlay.gameObject.SetActive(true);
            }

            // Ensure progress UI is active (do not modify hierarchy/sorting)
            if (progressBar != null)
            {
                EnsureActiveHierarchy(progressBar.gameObject);
            }
            if (progressText != null)
            {
                EnsureActiveHierarchy(progressText.gameObject);
            }

            // Kick off async load but do not activate yet
            var op = SceneManager.LoadSceneAsync(lobbySceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;

            float splashStart = Time.unscaledTime;

            // 1) Company name on black
            if (companyNameText != null)
            {
                EnsureActiveHierarchy(companyNameText.gameObject);
                var cg = GetOrAddCanvasGroup(companyNameText.gameObject);
                await FadeTMPWithGroup(companyNameText, cg, 0f, 1f, companyFadeIn);
                await Hold(companyHold);
                await FadeTMPWithGroup(companyNameText, cg, 1f, 0f, companyFadeOut);
            }

            // 2) Fade black background out to reveal scene
            if (blackOverlay != null)
            {
                await FadeImageAlpha(blackOverlay, 1f, 0f, fadeInDuration);
                // Optionally disable after fade
                blackOverlay.gameObject.SetActive(false);
            }

            // 3) Game name fade-in while loading continues
            if (gameNameText != null)
            {
                EnsureActiveHierarchy(gameNameText.gameObject);
                var cg2 = GetOrAddCanvasGroup(gameNameText.gameObject);
                // Optional SFX when the game name appears
                if (!string.IsNullOrEmpty(gameNameSfxKey))
                {
                    try { SFXManager.Instance.Play(gameNameSfxKey); } catch { /* ignore if manager not ready */ }
                }
                await FadeTMPWithGroup(gameNameText, cg2, 0f, 1f, gameFadeIn);
                await Hold(gameHold);
                await FadeTMPWithGroup(gameNameText, cg2, 1f, 0f, gameFadeOut);
            }

            // Ensure minimum splash duration
            float elapsed = Time.unscaledTime - splashStart;
            if (elapsed < minTotalSplashTime)
            {
                float wait = minTotalSplashTime - elapsed;
                float t = 0f; while (t < wait) { t += Time.unscaledDeltaTime; await Task.Yield(); }
            }

            // Wait until the scene is ready to activate (op.progress reaches ~0.9)
            while (op.progress < 0.9f)
            {
                OnProgress(Mathf.Clamp01(op.progress / 0.9f));
                await Task.Yield();
            }
            OnProgress(1f);

            // Fade to black, then activate, then schedule fade-in in the next scene's first frame
            await fader.FadeOut(fadeOutDuration);
            fader.ScheduleFadeInAfterSceneLoad(fadeInDuration);
            op.allowSceneActivation = true;
            while (!op.isDone) { await Task.Yield(); }
        }

        private void OnProgress(float p)
        {
            if (progressBar != null) progressBar.value = p;
            if (progressText != null) progressText.text = Mathf.RoundToInt(p * 100f) + "%";
        }

        private async Task RunSplashSequence()
        {
            // Company name
            if (companyNameText != null)
            {
                await FadeText(companyNameText, 0f, 1f, companyFadeIn);
                await Hold(companyHold);
                await FadeText(companyNameText, 1f, 0f, companyFadeOut);
            }
            // Game name
            if (gameNameText != null)
            {
                await FadeText(gameNameText, 0f, 1f, gameFadeIn);
                await Hold(gameHold);
                await FadeText(gameNameText, 1f, 0f, gameFadeOut);
            }
        }

        private void SetTextAlpha(TMP_Text t, float a)
        {
            if (t == null) return;
            var c = t.color; c.a = Mathf.Clamp01(a); t.color = c;
        }

        private async Task FadeText(TMP_Text t, float from, float to, float duration)
        {
            if (t == null) return;
            float d = Mathf.Max(0.01f, duration);
            float time = 0f;
            while (time < d)
            {
                time += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(time / d);
                float a = Mathf.Lerp(from, to, u);
                SetTextAlpha(t, a);
                await Task.Yield();
            }
            SetTextAlpha(t, to);
        }

        private async Task Hold(float duration)
        {
            float d = Mathf.Max(0f, duration);
            float t = 0f; while (t < d) { t += Time.unscaledDeltaTime; await Task.Yield(); }
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

        // Note: we do not modify hierarchy or sorting of UI elements; the scene's setup defines layering.

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

        private async Task FadeTMPWithGroup(TMP_Text txt, CanvasGroup cg, float from, float to, float duration)
        {
            if (txt == null) return;
            var baseColor = txt.color;
            float d = Mathf.Max(0.01f, duration);
            float t = 0f;
            if (cg != null) cg.alpha = from;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / d);
                float a = Mathf.Lerp(from, to, u);
                // drive both
                if (cg != null) cg.alpha = a;
                var c = baseColor; c.a = a; txt.color = c;
                await Task.Yield();
            }
            if (cg != null) cg.alpha = to;
            var c2 = baseColor; c2.a = to; txt.color = c2;
        }
    }
}
