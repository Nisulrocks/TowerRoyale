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
            _instance = Instantiate(banPopupPrefab, parentRt);
            _instance.SetActive(true);

            // Optional SFX
            if (!string.IsNullOrEmpty(sfxKey) && SFXManager.Instance != null)
            {
                SFXManager.Instance.Play(sfxKey);
            }

            // Fill texts (try to find multiple TMP_Texts if available)
            var texts = _instance.GetComponentsInChildren<TMP_Text>(true);
            string timeStr = FormatRemaining(remaining);
            string body = string.Format(bodyFormat, timeStr);
            if (texts != null && texts.Length > 0)
            {
                if (texts.Length == 1)
                {
                    texts[0].text = string.IsNullOrEmpty(titleText) ? body : ($"{titleText}\n\n{body}\n\n{footerText}");
                }
                else
                {
                    // Heuristic: first = title, second = body, third (if any) = footer
                    texts[0].text = titleText;
                    texts[1].text = body;
                    if (texts.Length >= 3) texts[2].text = footerText;
                }
            }

            // Try to animate (CanvasGroup + anchored slide)
            _cg = _instance.GetComponent<CanvasGroup>();
            if (_cg == null) _cg = _instance.AddComponent<CanvasGroup>();
            _rt = _instance.GetComponent<RectTransform>();
            StartCoroutine(AnimateIn(_cg, _rt));
            // Begin monitoring; when ban expires, fade out using fadeOutTime
            StartCoroutine(MonitorBanUntilCleared());
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
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeInTime);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                cg.alpha = e;
                rt.anchoredPosition = Vector2.Lerp(startPos, basePos, e);
                yield return null;
            }
            cg.alpha = 1f;
            rt.anchoredPosition = basePos;
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
                yield return null;
            }
            cg.alpha = 0f;
            if (_instance != null) Destroy(_instance);
            _instance = null; _cg = null; _rt = null;
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
            else
            {
                int m = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalMinutes));
                return $"{m}m";
            }
        }
    }
}
