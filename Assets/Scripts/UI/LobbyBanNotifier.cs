using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Audio;

namespace TR.UI
{
    // Drop this in the Lobby scene. Assign a popup prefab (panel) with Text fields.
    // When the player is soft-banned (client-side moderation), this shows a blocking message with time remaining.
    public class LobbyBanNotifier : MonoBehaviour
    {
        [Header("Prefab & Parent")]
        [Tooltip("UI prefab for the ban panel. Should contain one or more TMP_Text fields for content.")]
        [SerializeField] private GameObject banPopupPrefab;
        [Tooltip("Optional parent RectTransform (Canvas). If null, auto-find a Canvas in scene.")]
        [SerializeField] private RectTransform parent;

        [Header("Backdrop (Optional)")]
        [Tooltip("If provided, a fullscreen Image is created behind the popup. Assign a blur material here for a blurred backdrop, otherwise a dim scrim is used.")]
        [SerializeField] private Material backdropMaterial;
        [Tooltip("RGBA color for the backdrop when no blur material is provided.")]
        [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.35f);
        [Tooltip("Max alpha to animate the backdrop to on show. Ignored if the material handles its own visuals.")]
        [SerializeField] private float backdropMaxAlpha = 0.35f;

        [Header("Texts")]
        [SerializeField] private string titleText = "Temporary Restriction";
        [SerializeField] private string bodyFormat = "We detected corrupted game data and temporarily restricted gameplay.\n\nTime remaining: {0}";
        [Tooltip("If provided, appended as a smaller note at the bottom.")]
        [SerializeField] private string footerText = "You can still browse menus. Please try again later.";

        [Header("Animation")]
        [SerializeField] private float initialDelay = 0.75f;
        [SerializeField] private float fadeInTime = 0.25f;
        [SerializeField] private float fadeOutTime = 0.25f;
        [Tooltip("How long to keep the popup visible before auto-dismissing (even if the ban is still active). 0 = never auto-dismiss")]
        [SerializeField] private float visibleHoldTime = 2.5f;
        [SerializeField] private Vector2 slideOffset = new Vector2(0f, -40f);

        [Header("SFX (Optional)")]
        [SerializeField] private string sfxKey = "";

        private GameObject _instance;
        private CanvasGroup _cg;
        private RectTransform _rt;
        private bool _isDismissing;
        private Image _backdrop;
        private CanvasGroup _backdropCg;
        private TMP_Text[] _texts; // cache to live-update remaining time
        private Coroutine _countdownCo;

        private void Start()
        {
            StartCoroutine(TryShowAfterDelay());
        }

        private IEnumerator TryShowAfterDelay()
        {
            // Delay for scene fade
            float t = Mathf.Max(0f, initialDelay);
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (!PlayerProfile.IsBanned(out var remaining))
                yield break;

            var parentRt = parent != null ? parent : GetDefaultCanvasParent();
            EnsureBackdrop(parentRt);
            _instance = Instantiate(banPopupPrefab, parentRt);
            _instance.SetActive(true);

            // Optional SFX
            if (!string.IsNullOrEmpty(sfxKey) && SFXManager.Instance != null)
            {
                SFXManager.Instance.Play(sfxKey);
            }

            // Fill texts (try to find multiple TMP_Texts if available)
            _texts = _instance.GetComponentsInChildren<TMP_Text>(true);
            if (_texts != null && _texts.Length > 0)
            {
                // Assume first 1-3 texts are title/body/footer in order (robust against prefab differences)
                if (_texts.Length >= 1) _texts[0].text = titleText;
                if (_texts.Length >= 2)
                {
                    string timeStr = FormatRemaining(remaining);
                    _texts[1].text = string.Format(bodyFormat, timeStr);
                }
                if (_texts.Length >= 3) _texts[2].text = footerText;
            }

            // Try to animate (CanvasGroup + anchored slide)
            _cg = _instance.GetComponent<CanvasGroup>();
            if (_cg == null) _cg = _instance.AddComponent<CanvasGroup>();
            _rt = _instance.GetComponent<RectTransform>();
            StartCoroutine(AnimateIn(_cg, _rt));
            // Begin monitoring; when ban expires, fade out using fadeOutTime
            StartCoroutine(MonitorBanUntilCleared());
            // Live countdown updates
            if (_countdownCo != null) StopCoroutine(_countdownCo);
            _countdownCo = StartCoroutine(CountdownUpdater());
            // Also auto-dismiss after a short hold time to avoid covering the lobby forever
            if (visibleHoldTime > 0f)
                StartCoroutine(AutoDismissAfterHold());
        }

        private RectTransform GetDefaultCanvasParent()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c != null && (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera))
                {
                    return c.transform as RectTransform;
                }
            }
            return this.transform as RectTransform;
        }

        private IEnumerator AnimateIn(CanvasGroup cg, RectTransform rt)
        {
            Vector2 basePos = rt.anchoredPosition;
            Vector2 startPos = basePos + slideOffset;
            rt.anchoredPosition = startPos;
            cg.alpha = 0f;
            // Backdrop fade in
            if (_backdropCg != null)
            {
                _backdropCg.alpha = 0f;
                if (_backdrop != null && _backdrop.material == null)
                {
                    _backdrop.color = new Color(backdropColor.r, backdropColor.g, backdropColor.b, 0f);
                }
            }
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeInTime);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                cg.alpha = e;
                rt.anchoredPosition = Vector2.Lerp(startPos, basePos, e);
                if (_backdropCg != null)
                {
                    float ba = Mathf.Lerp(0f, backdropMaxAlpha, e);
                    _backdropCg.alpha = ba;
                    if (_backdrop != null && _backdrop.material == null)
                    {
                        _backdrop.color = new Color(backdropColor.r, backdropColor.g, backdropColor.b, ba);
                    }
                }
                yield return null;
            }
            cg.alpha = 1f;
            rt.anchoredPosition = basePos;
            if (_backdropCg != null)
            {
                _backdropCg.alpha = backdropMaxAlpha;
            }
        }

        private IEnumerator AnimateOut(CanvasGroup cg, RectTransform rt)
        {
            if (cg == null || rt == null) yield break;
            Vector2 basePos = rt.anchoredPosition;
            Vector2 endPos = basePos + (-slideOffset);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeOutTime);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                cg.alpha = 1f - e;
                rt.anchoredPosition = Vector2.Lerp(basePos, endPos, e);
                if (_backdropCg != null)
                {
                    float ba = Mathf.Lerp(backdropMaxAlpha, 0f, e);
                    _backdropCg.alpha = ba;
                    if (_backdrop != null && _backdrop.material == null)
                    {
                        _backdrop.color = new Color(backdropColor.r, backdropColor.g, backdropColor.b, ba);
                    }
                }
                yield return null;
            }
            cg.alpha = 0f;
            if (_instance != null) Destroy(_instance);
            _instance = null; _cg = null; _rt = null;
            if (_backdrop != null)
            {
                Destroy(_backdrop.gameObject);
                _backdrop = null; _backdropCg = null;
            }
        }

        private IEnumerator MonitorBanUntilCleared()
        {
            // Wait while still banned
            while (PlayerProfile.IsBanned(out _))
            {
                yield return null;
            }
            yield return Dismiss();
        }

        private IEnumerator CountdownUpdater()
        {
            while (_instance != null && PlayerProfile.IsBanned(out var remaining))
            {
                if (_texts != null && _texts.Length >= 2)
                {
                    _texts[1].text = string.Format(bodyFormat, FormatRemaining(remaining));
                }
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        private IEnumerator AutoDismissAfterHold()
        {
            float t = Mathf.Max(0f, visibleHoldTime);
            while (t > 0f && _instance != null)
            {
                t -= Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Dismiss();
        }

        private IEnumerator Dismiss()
        {
            if (_isDismissing) yield break;
            _isDismissing = true;
            if (_instance != null)
            {
                yield return AnimateOut(_cg, _rt);
            }
            _isDismissing = false;
        }

        private static string FormatRemaining(System.TimeSpan remaining)
        {
            if (remaining.TotalHours >= 1.0)
            {
                int h = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalHours));
                int m = Mathf.Max(0, Mathf.CeilToInt((float)(remaining.TotalMinutes % 60)));
                return m > 0 ? $"{h}h {m}m" : $"{h}h";
            }
            else if (remaining.TotalMinutes >= 1.0)
            {
                int m = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalMinutes));
                return $"{m}m";
            }
            else
            {
                int s = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalSeconds));
                return $"{s}s";
            }
        }

        private void EnsureBackdrop(RectTransform parentRt)
        {
            if (parentRt == null) return;
            if (_backdrop != null) return;
            // Create a full-screen Image under the same parent, ensure it renders behind popup by inserting as first child
            var go = new GameObject("BanBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parentRt, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            // Move to bottom of hierarchy so it sits behind the popup instance
            go.transform.SetAsFirstSibling();

            _backdrop = go.GetComponent<Image>();
            _backdropCg = go.GetComponent<CanvasGroup>();
            _backdrop.raycastTarget = true; // block clicks to lobby while visible
            if (backdropMaterial != null)
            {
                _backdrop.material = backdropMaterial;
                // If the blur material expects a base color alpha, keep the color alpha matching backdropMaxAlpha
                _backdrop.color = new Color(1f, 1f, 1f, backdropMaxAlpha);
            }
            else
            {
                _backdrop.material = null;
                _backdrop.color = new Color(backdropColor.r, backdropColor.g, backdropColor.b, 0f);
            }
            _backdropCg.alpha = 0f;
        }
    }
}
