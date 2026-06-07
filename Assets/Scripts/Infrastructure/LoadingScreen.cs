using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using TR.Audio;

namespace TR.Infrastructure
{
    
    
    
    public class LoadingScreen : MonoBehaviour
    {
        [Header("Target Scene")] public string lobbySceneName = "Lobby";
        [Header("UI (optional)")]
        public Slider progressBar; 
        public TMP_Text progressText; 

        public Image blackOverlay; 
        [Header("Splash Text (optional)")]
        public TMP_Text companyNameText; 
        public TMP_Text gameNameText; 
        [Header("SFX (optional)")]
        [Tooltip("SFX key to play (via SFXManager) right when the game name appears")]
        public string gameNameSfxKey = "";
        [Header("Timings")] 
        public float fadeOutDuration = 0.35f; 
        public float fadeInDuration = 0.35f;
        public float companyFadeIn = 0.4f, companyHold = 0.8f, companyFadeOut = 0.4f;
        public float gameFadeIn = 0.4f, gameHold = 0.9f, gameFadeOut = 0.4f;
        public float minTotalSplashTime = 2.0f; 

        private async void Start()
        {
            
            var fader = SceneFader.Instance;
            fader.SetAlpha(0f); 

            
            SetTextAlpha(companyNameText, 1f);
            SetTextAlpha(gameNameText, 1f);
            
            if (blackOverlay != null)
            {
                var c = blackOverlay.color; c.a = 1f; blackOverlay.color = c;
                if (!blackOverlay.gameObject.activeSelf) blackOverlay.gameObject.SetActive(true);
            }

            
            if (progressBar != null)
            {
                EnsureActiveHierarchy(progressBar.gameObject);
            }
            if (progressText != null)
            {
                EnsureActiveHierarchy(progressText.gameObject);
            }

            
            var op = SceneManager.LoadSceneAsync(lobbySceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;

            float splashStart = Time.unscaledTime;

            
            if (companyNameText != null)
            {
                EnsureActiveHierarchy(companyNameText.gameObject);
                var cg = GetOrAddCanvasGroup(companyNameText.gameObject);
                await FadeTMPWithGroup(companyNameText, cg, 0f, 1f, companyFadeIn);
                await Hold(companyHold);
                await FadeTMPWithGroup(companyNameText, cg, 1f, 0f, companyFadeOut);
            }

            
            if (blackOverlay != null)
            {
                await FadeImageAlpha(blackOverlay, 1f, 0f, fadeInDuration);
                
                blackOverlay.gameObject.SetActive(false);
            }

            
            if (gameNameText != null)
            {
                EnsureActiveHierarchy(gameNameText.gameObject);
                var cg2 = GetOrAddCanvasGroup(gameNameText.gameObject);
                
                if (!string.IsNullOrEmpty(gameNameSfxKey))
                {
                    try { SFXManager.Instance.Play(gameNameSfxKey); } catch { /* ignore if manager not ready */ }
                }
                await FadeTMPWithGroup(gameNameText, cg2, 0f, 1f, gameFadeIn);
                await Hold(gameHold);
                await FadeTMPWithGroup(gameNameText, cg2, 1f, 0f, gameFadeOut);
            }

            
            float elapsed = Time.unscaledTime - splashStart;
            if (elapsed < minTotalSplashTime)
            {
                float wait = minTotalSplashTime - elapsed;
                float t = 0f; while (t < wait) { t += Time.unscaledDeltaTime; await Task.Yield(); }
            }

            
            while (op.progress < 0.9f)
            {
                OnProgress(Mathf.Clamp01(op.progress / 0.9f));
                await Task.Yield();
            }
            OnProgress(1f);

            
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
            
            if (companyNameText != null)
            {
                await FadeText(companyNameText, 0f, 1f, companyFadeIn);
                await Hold(companyHold);
                await FadeText(companyNameText, 1f, 0f, companyFadeOut);
            }
            
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
                
                if (cg != null) cg.alpha = a;
                var c = baseColor; c.a = a; txt.color = c;
                await Task.Yield();
            }
            if (cg != null) cg.alpha = to;
            var c2 = baseColor; c2.a = to; txt.color = c2;
        }
    }
}
