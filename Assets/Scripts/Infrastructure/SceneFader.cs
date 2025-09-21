using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

namespace TR.Infrastructure
{
    // Global fade controller that persists across scenes and provides smooth scene transitions.
    // No setup required in scenes; it creates a fullscreen Canvas + Image at runtime.
    public class SceneFader : MonoBehaviour
    {
        private static SceneFader _instance;
        public static SceneFader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("SceneFader");
                    _instance = go.AddComponent<SceneFader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Fade Settings")] [SerializeField]
        private Color fadeColor = Color.black;
        [SerializeField] private float defaultFadeDuration = 0.35f;
        [SerializeField] private int canvasSortOrder = 10000;
        [Header("Message Fade Settings")] [SerializeField]
        private float messageFadeInDuration = 0.3f;
        [SerializeField] private float messageFadeOutDuration = 0.3f;
        [Header("Post-Scene Fade Settings")] [SerializeField]
        private float postSceneFadeInDelay = 0.02f; // seconds, small safety delay before fading in after scene load

        private Canvas _canvas;
        private Image _image;
        private CanvasGroup _group;
        private bool _isFading;
        private TMP_Text _centerText;
        private string _nextMessage;
        private float _nextMessageDuration;
        private bool _pendingFadeIn;
        private float _pendingFadeInDuration;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureCanvas();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void EnsureCanvas()
        {
            // Canvas and helpers
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
                if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = canvasSortOrder;
                _canvas.pixelPerfect = false;
                if (GetComponent<CanvasScaler>() == null) gameObject.AddComponent<CanvasScaler>();
                if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            }

            // Fade overlay (Image + CanvasGroup)
            if (_image == null || _group == null)
            {
                Transform fadeTr = transform.Find("Fade");
                GameObject go;
                if (fadeTr == null)
                {
                    go = new GameObject("Fade");
                    go.transform.SetParent(transform, false);
                }
                else go = fadeTr.gameObject;

                _image = go.GetComponent<Image>();
                if (_image == null) _image = go.AddComponent<Image>();
                _image.color = fadeColor;
                _image.raycastTarget = false; // default: do NOT block, only during fades
                var rect = _image.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;

                _group = go.GetComponent<CanvasGroup>();
                if (_group == null) _group = go.AddComponent<CanvasGroup>();
                _group.alpha = Mathf.Clamp01(_group.alpha); // leave as-is if previously set
                _group.blocksRaycasts = false; // default no blocking
                _group.interactable = false;
            }

            // Centered TMP text for transition messages (e.g., Arena name)
            if (_centerText == null)
            {
                try
                {
                    Transform t = transform.Find("CenterText");
                    GameObject tgo;
                    if (t == null)
                    {
                        tgo = new GameObject("CenterText");
                        tgo.transform.SetParent(transform, false);
                    }
                    else tgo = t.gameObject;
                    var tmp = tgo.GetComponent<TextMeshProUGUI>();
                    if (tmp == null) tmp = tgo.AddComponent<TextMeshProUGUI>();
                    _centerText = tmp;
                    var rt = _centerText.rectTransform;
                    rt.anchorMin = new Vector2(0.1f, 0.4f);
                    rt.anchorMax = new Vector2(0.9f, 0.6f);
                    rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                    _centerText.alignment = TextAlignmentOptions.Center;
                    _centerText.textWrappingMode = TextWrappingModes.NoWrap;
                    _centerText.fontSize = 64;
                    _centerText.raycastTarget = false; // do not block clicks
                    var c = _centerText.color; c.a = 0f; _centerText.color = c; // invisible by default
                }
                catch
                {
                    _centerText = null; // TMP might be unavailable
                }
            }
        }

        // Instant set alpha (0..1)
        public void SetAlpha(float a)
        {
            EnsureCanvas();
            _group.alpha = Mathf.Clamp01(a);
        }

        public Task FadeOut(float duration = -1f)
        {
            return RunFade(1f, duration);
        }

        public Task FadeIn(float duration = -1f)
        {
            return RunFade(0f, duration);
        }

        private async Task RunFade(float target, float duration)
        {
            EnsureCanvas();
            if (_isFading) yieldBreakLike();
            _isFading = true;
            // Begin blocking input during fade
            _group.blocksRaycasts = true;
            _image.raycastTarget = true;
            float d = duration > 0f ? duration : defaultFadeDuration;
            float start = _group.alpha;
            float t = 0f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / d);
                _group.alpha = Mathf.Lerp(start, target, u);
                await Task.Yield();
            }
            _group.alpha = target;
            _isFading = false;
            // End blocking when fully transparent
            bool transparent = _group.alpha <= 0.0001f;
            _group.blocksRaycasts = !transparent; // only block if we remain visible
            _image.raycastTarget = !transparent;
        }

        // Wrapper to satisfy async without warnings in editor
        private async void yieldBreakLike() { await Task.Yield(); }

        // Public helper: fade out, load scene async, fade in
        public async Task LoadSceneWithFade(string sceneName, float fadeOut = -1f, float fadeIn = -1f)
        {
            EnsureCanvas();
            // If a next message is queued, show it during the fade out and ensure it displays long enough
            if (!string.IsNullOrEmpty(_nextMessage) && _centerText != null)
            {
                // Prepare text invisible, then run fade-in/hold/fade-out alongside the screen fade
                PrepareCenterMessage(_nextMessage, 0f);
                float hold = Mathf.Max(0f, _nextMessageDuration);
                _nextMessage = null; _nextMessageDuration = 0f;
                var msg = RunMessageSequence(hold);
                await Task.WhenAll(FadeOut(fadeOut), msg);
                if (_centerText != null) SetTextAlpha(_centerText, 0f);
            }
            else
            {
                await FadeOut(fadeOut);
            }
            // Load scene asynchronously
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            // Schedule a post-load fade-in to occur via sceneLoaded/activeSceneChanged callback
            ScheduleFadeInAfterSceneLoad(fadeIn > 0f ? fadeIn : defaultFadeDuration);
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                await Task.Yield();
            }
        }

        // Overload that accepts build index
        public async Task LoadSceneWithFade(int buildIndex, float fadeOut = -1f, float fadeIn = -1f)
        {
            EnsureCanvas();
            await FadeOut(fadeOut);
            var op = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            ScheduleFadeInAfterSceneLoad(fadeIn > 0f ? fadeIn : defaultFadeDuration);
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                await Task.Yield();
            }
        }

        // Optional: expose progress while loading target scene
        public async Task LoadSceneWithFadeAndProgress(string sceneName, System.Action<float> onProgress, float fadeOut = -1f, float fadeIn = -1f)
        {
            EnsureCanvas();
            if (!string.IsNullOrEmpty(_nextMessage))
            {
                ShowCenterMessageImmediate(_nextMessage);
                float hold = Mathf.Max(0f, _nextMessageDuration);
                _nextMessage = null; _nextMessageDuration = 0f;
                await Task.WhenAll(FadeOut(fadeOut), HoldUnscaled(hold));
                _centerText.color = new Color(_centerText.color.r, _centerText.color.g, _centerText.color.b, 0f);
            }
            else
            {
                await FadeOut(fadeOut);
            }
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
            {
                onProgress?.Invoke(Mathf.Clamp01(op.progress / 0.9f));
                await Task.Yield();
            }
            // Ready, show 100%
            onProgress?.Invoke(1f);
            op.allowSceneActivation = true;
            while (!op.isDone) { await Task.Yield(); }
            await FadeIn(fadeIn);
        }

        // Convenience: if already in target scene, do nothing; else fade transition
        public async Task LoadSceneWithFadeIfNeeded(string sceneName, float fadeOut = -1f, float fadeIn = -1f)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
            {
                return;
            }
            await LoadSceneWithFade(sceneName, fadeOut, fadeIn);
        }

        // Queue a message to be shown centered during the next fade-out
        public void SetNextTransitionMessage(string message, float seconds = 1.0f)
        {
            EnsureCanvas();
            if (_centerText == null) return; // no TMP available
            _nextMessage = message;
            _nextMessageDuration = Mathf.Max(0f, seconds);
        }

        private void ShowCenterMessageImmediate(string message)
        {
            if (_centerText == null) return;
            _centerText.text = message;
            var c = _centerText.color; c.a = 1f; _centerText.color = c;
        }

        private void PrepareCenterMessage(string message, float alpha)
        {
            if (_centerText == null) return;
            _centerText.text = message;
            SetTextAlpha(_centerText, alpha);
        }

        private void SetTextAlpha(TMP_Text txt, float a)
        {
            if (txt == null) return;
            var c = txt.color; c.a = Mathf.Clamp01(a); txt.color = c;
        }

        private async Task RunMessageSequence(float holdSeconds)
        {
            if (_centerText == null) { await Task.Yield(); return; }
            // Fade in
            await FadeText(_centerText, 0f, 1f, messageFadeInDuration);
            // Hold
            await HoldUnscaled(holdSeconds);
            // Fade out
            await FadeText(_centerText, 1f, 0f, messageFadeOutDuration);
        }

        private async Task FadeText(TMP_Text txt, float from, float to, float duration)
        {
            if (txt == null) return;
            float d = Mathf.Max(0.01f, duration);
            float t = 0f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / d);
                float a = Mathf.Lerp(from, to, u);
                SetTextAlpha(txt, a);
                await Task.Yield();
            }
            SetTextAlpha(txt, to);
        }

        private async Task HoldUnscaled(float duration)
        {
            float d = Mathf.Max(0f, duration);
            float t = 0f; while (t < d) { t += Time.unscaledDeltaTime; await Task.Yield(); }
        }

        // Schedule a fade-in to run right after the next scene is loaded and rendered at least one frame
        public void ScheduleFadeInAfterSceneLoad(float duration)
        {
            EnsureCanvas();
            _pendingFadeIn = true;
            _pendingFadeInDuration = duration > 0f ? duration : defaultFadeDuration;
            // Ensure we are fully opaque so the new scene starts under black
            _group.alpha = 1f;
        }

        private async void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_pendingFadeIn) return;
            EnsureCanvas();
            // Force black immediately on the new scene's first frame
            _group.alpha = 1f;
            // Wait a frame to ensure the scene has rendered under black
            await Task.Yield();
            if (postSceneFadeInDelay > 0f) await HoldUnscaled(postSceneFadeInDelay);
            var dur = _pendingFadeInDuration;
            _pendingFadeIn = false;
            await FadeIn(dur);
        }

        private async void OnActiveSceneChanged(Scene prev, Scene next)
        {
            if (!_pendingFadeIn) return;
            EnsureCanvas();
            _group.alpha = 1f;
            await Task.Yield();
            if (postSceneFadeInDelay > 0f) await HoldUnscaled(postSceneFadeInDelay);
            var dur = _pendingFadeInDuration;
            _pendingFadeIn = false;
            await FadeIn(dur);
        }
    }
}
