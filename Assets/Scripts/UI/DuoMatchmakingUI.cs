using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Net;

namespace TR.UI
{
    
    public class DuoMatchmakingUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameObject panelRoot;        // the searching panel container
        [SerializeField] private TMP_Text statusText;         // "Searching for a partner..."
        [SerializeField] private Button cancelButton;         // cancel matchmaking

        [Header("Fade")]
        [Tooltip("CanvasGroup used to fade the whole panel in/out. If left empty, one is added to panelRoot.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeInDuration = 0.25f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        [Header("Arena Icon")]
        [Tooltip("Image that shows which arena is being matched into.")]
        [SerializeField] private Image arenaIcon;
        [Tooltip("Root transform to pop-animate (defaults to arenaIcon's transform).")]
        [SerializeField] private Transform arenaIconRoot;
        [SerializeField] private float arenaPopDuration = 0.45f;
        [Tooltip("Peak scale during the pop (overshoot), settles back to 1.")]
        [SerializeField] private float arenaPopOvershoot = 1.25f;
        [Tooltip("Delay before the arena icon pops in after the panel starts fading in.")]
        [SerializeField] private float arenaPopDelay = 0.1f;

        private Coroutine _fadeCo;
        private Coroutine _popCo;

        private void Awake()
        {
            EnsureCanvasGroup();
            if (arenaIconRoot == null && arenaIcon != null) arenaIconRoot = arenaIcon.transform;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup != null) return;
            if (panelRoot != null)
            {
                canvasGroup = panelRoot.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = panelRoot.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnClickCancel);
                cancelButton.onClick.AddListener(OnClickCancel);
            }
        }

        private void OnDisable()
        {
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnClickCancel);
            Unsubscribe();
        }

        
        public void Show(Sprite arenaSprite = null)
        {
            EnsureCanvasGroup();
            if (panelRoot != null) panelRoot.SetActive(true);
            Subscribe();
            if (statusText != null) statusText.text = "Connecting...";

            
            if (arenaIcon != null)
            {
                arenaIcon.sprite = arenaSprite;
                arenaIcon.enabled = arenaSprite != null;
            }

            
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(FadeRoutine(1f, fadeInDuration, false));

            
            if (arenaSprite != null && arenaIconRoot != null)
            {
                if (_popCo != null) StopCoroutine(_popCo);
                _popCo = StartCoroutine(ArenaPopRoutine());
            }
        }

        
        public void Hide()
        {
            if (!isActiveAndEnabled || panelRoot == null || !panelRoot.activeInHierarchy)
            {
                HideImmediate();
                return;
            }
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(FadeRoutine(0f, fadeOutDuration, true));
        }

        
        public void HideImmediate()
        {
            if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
            if (_popCo != null) { StopCoroutine(_popCo); _popCo = null; }
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (panelRoot != null) panelRoot.SetActive(false);
            Unsubscribe();
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration, bool deactivateOnEnd)
        {
            if (canvasGroup == null)
            {
                if (deactivateOnEnd) HideImmediate();
                yield break;
            }
            float start = canvasGroup.alpha;
            float t = 0f;
            if (duration <= 0f)
            {
                canvasGroup.alpha = targetAlpha;
            }
            else
            {
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    float u = Mathf.Clamp01(t / duration);
                    canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, u);
                    yield return null;
                }
                canvasGroup.alpha = targetAlpha;
            }
            _fadeCo = null;
            if (deactivateOnEnd)
            {
                if (panelRoot != null) panelRoot.SetActive(false);
                Unsubscribe();
            }
        }

        private IEnumerator ArenaPopRoutine()
        {
            if (arenaIconRoot == null) yield break;
            Vector3 baseScale = Vector3.one;
            arenaIconRoot.localScale = Vector3.zero;
            if (arenaPopDelay > 0f) yield return new WaitForSecondsRealtime(arenaPopDelay);

            float t = 0f;
            float dur = Mathf.Max(0.01f, arenaPopDuration);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                
                float eased = EaseOutBack(u, arenaPopOvershoot);
                arenaIconRoot.localScale = baseScale * eased;
                yield return null;
            }
            arenaIconRoot.localScale = baseScale;
            _popCo = null;
        }

        
        private static float EaseOutBack(float x, float overshoot)
        {
            
            float s = Mathf.Max(0f, overshoot - 1f) * 1.70158f / 0.25f + 1.70158f;
            float inv = x - 1f;
            return 1f + (s + 1f) * inv * inv * inv + s * inv * inv;
        }

        private void Subscribe()
        {
            var mgr = DuoNetworkManager.Instance;
            if (mgr == null) return;
            mgr.OnStatusChanged += HandleStatus;
            mgr.OnCancelled += HandleCancelled;
            mgr.OnFailed += HandleFailed;
        }

        private void Unsubscribe()
        {
            var mgr = DuoNetworkManager.Instance;
            if (mgr == null) return;
            mgr.OnStatusChanged -= HandleStatus;
            mgr.OnCancelled -= HandleCancelled;
            mgr.OnFailed -= HandleFailed;
        }

        private void HandleStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        private void HandleCancelled()
        {
            Hide();
        }

        private void HandleFailed(string error)
        {
            if (statusText != null) statusText.text = $"Match failed: {error}";
            
            CancelInvoke(nameof(Hide));
            Invoke(nameof(Hide), 2.0f);
        }

        private void OnClickCancel()
        {
            if (DuoNetworkManager.Instance != null)
            {
                DuoNetworkManager.Instance.CancelMatchmaking();
            }
            Hide();
        }
    }
}
