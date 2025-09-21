using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Data;
using TR.Audio;

namespace TR.UI
{
    // Place this in your Lobby scene. Assign a prefab (with an Image + TMP_Text) and an optional parent.
    // When the player unlocks a new arena, ArenaService queues a pending notification in PlayerProfile.
    // On lobby load, this script consumes it and shows a celebratory popup with icon + text.
    public class LobbyArenaUnlockNotifier : MonoBehaviour
    {
        [Header("Prefab & Parent")]
        [Tooltip("UI prefab containing an Image and a TMP_Text. We'll auto-find those and fill them.")]
        [SerializeField] private GameObject popupPrefab;
        [Tooltip("Optional parent container. If null, we try to attach to the nearest Canvas.")]
        [SerializeField] private RectTransform parent;

        [Header("Text Template")]
        [Tooltip("Header shown above or alongside the arena name.")]
        [SerializeField] private string headerText = "New Arena Unlocked!";
        [Tooltip("Encouraging line that appears after the arena name. Use {0} for the arena display name if desired.")]
        [SerializeField] private string encouragingText = "Keep pushing forward, {0}!";

        [Header("Animation")]
        [Tooltip("Seconds to wait after Lobby loads before showing the popup (to avoid competing with screen fade-in)")]
        [SerializeField] private float initialDelay = 0.75f;
        [SerializeField] private float fadeInTime = 0.25f;
        [SerializeField] private float holdTime = 2.0f;
        [SerializeField] private float fadeOutTime = 0.3f;
        [SerializeField] private Vector2 slideOffset = new Vector2(0f, 40f);

        [Header("SFX (Optional)")]
        [SerializeField] private string sfxKey = "";

        private void Start()
        {
            StartCoroutine(WaitThenTryShow());
        }

        private IEnumerator WaitThenTryShow()
        {
            float t = Mathf.Max(0f, initialDelay);
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime;
                yield return null;
            }
            TryShowIfPending();
        }

        private void TryShowIfPending()
        {
            if (popupPrefab == null) return;
            if (!PlayerProfile.TryConsumePendingArenaUnlock(out string arenaName)) return;

            GameDB.EnsureLoaded();
            // Try to find the ArenaDefinition by display name to get the icon; fallback to name-only.
            Sprite icon = null;
            var arenas = GameDB.GetArenasSortedByRequirement();
            if (arenas != null)
            {
                foreach (var a in arenas)
                {
                    if (a != null && a.DisplayName == arenaName)
                    {
                        icon = a.ArenaImage;
                        break;
                    }
                }
            }

            // Instantiate UI
            var parentRt = parent != null ? parent : GetDefaultCanvasParent();
            var go = Instantiate(popupPrefab, parentRt);
            go.SetActive(true);

            // Find first Image and TMP_Text in the instance
            var img = go.GetComponentInChildren<Image>(true);
            var txt = go.GetComponentInChildren<TMP_Text>(true);
            if (img != null) img.sprite = icon;
            if (txt != null)
            {
                if (string.IsNullOrEmpty(encouragingText)) encouragingText = "Keep going!";
                string line2 = string.Format(encouragingText, arenaName);
                // If the prefab has only one TMP_Text, combine header + name + encouraging line
                txt.text = string.IsNullOrEmpty(headerText)
                    ? $"{arenaName}\n{line2}"
                    : $"{headerText}\n{arenaName}\n{line2}";
            }

            // Optional SFX
            if (!string.IsNullOrEmpty(sfxKey) && SFXManager.Instance != null)
            {
                SFXManager.Instance.Play(sfxKey);
            }

            // Animate fade+slide
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            StartCoroutine(Animate(cg, go.GetComponent<RectTransform>()));
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
            // Fallback: attach under self
            return this.transform as RectTransform;
        }

        private IEnumerator Animate(CanvasGroup cg, RectTransform rt)
        {
            Vector2 basePos = rt.anchoredPosition;
            Vector2 startPos = basePos + slideOffset;
            rt.anchoredPosition = startPos;
            cg.alpha = 0f;
            // Fade in
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

            // Hold
            float wait = Mathf.Max(0f, holdTime);
            while (wait > 0f)
            {
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }

            // Fade out
            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeOutTime);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                cg.alpha = 1f - e;
                rt.anchoredPosition = Vector2.Lerp(basePos, basePos + slideOffset, e);
                yield return null;
            }
            cg.alpha = 0f;
            Destroy(cg.gameObject);
        }
    }
}
