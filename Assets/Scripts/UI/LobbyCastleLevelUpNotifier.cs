using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Audio;

namespace TR.UI
{
    public class LobbyCastleLevelUpNotifier : MonoBehaviour
    {
        [Header("Prefab & Parent")]
        [SerializeField] private GameObject popupPrefab;
        [SerializeField] private RectTransform parent;

        [Header("Text")]
        [SerializeField] private string headerText = "Castle Level UP!";
        [SerializeField] private string bodyFormat = "Lvl {0} - {1}\nHealth {2} - {3}";

        [Header("Animation")]
        [SerializeField] private float initialDelay = 0.75f;
        [SerializeField] private float popInTime = 0.35f;
        [SerializeField] private float popInOvershoot = 0f;
        [SerializeField] private float holdTime = 2.0f;
        [SerializeField] private float fadeOutTime = 0.3f;

        [Header("SFX (Optional)")]
        [SerializeField] private string sfxKey = "ui_lobby_level_up";

        public bool autoPlay = true;

        public bool IsShowing { get; private set; }

        private void Start()
        {
            if (autoPlay)
                StartCoroutine(WaitThenTryShow());
        }

        private void OnEnable()
        {
            PlayerProfile.OnCastleLevelUp += HandleCastleLevelUp;
        }

        private void OnDisable()
        {
            PlayerProfile.OnCastleLevelUp -= HandleCastleLevelUp;
        }

        private void HandleCastleLevelUp(int fromLevel, int toLevel)
        {
            if (autoPlay)
                TryShowIfPending();
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

        public void TryShowIfPending()
        {
            if (IsShowing) return;
            if (popupPrefab == null) return;
            if (!PlayerProfile.TryConsumePendingCastleLevelUp(out int fromLevel, out int toLevel, out int fromHealth, out int toHealth)) return;

            IsShowing = true;

            var parentRt = parent != null ? parent : GetDefaultCanvasParent();
            var go = Instantiate(popupPrefab, parentRt);
            go.SetActive(true);

            var txt = go.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                string body = string.Format(bodyFormat, fromLevel, toLevel, fromHealth, toHealth);
                txt.text = string.IsNullOrEmpty(headerText)
                    ? body
                    : $"{headerText}\n{body}";
            }

            if (!string.IsNullOrEmpty(sfxKey) && SFXManager.Instance != null)
            {
                SFXManager.Instance.Play(sfxKey);
            }

            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            var rt = go.GetComponent<RectTransform>();
            StartCoroutine(Animate(cg, rt));
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

        private IEnumerator Animate(CanvasGroup cg, RectTransform rt)
        {
            cg.alpha = 0f;
            rt.localScale = Vector3.zero;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, popInTime);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                cg.alpha = e;
                rt.localScale = Vector3.one * PopInScale(e);
                yield return null;
            }
            cg.alpha = 1f;
            rt.localScale = Vector3.one;

            float wait = Mathf.Max(0f, holdTime);
            while (wait > 0f)
            {
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }

            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeOutTime);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                cg.alpha = 1f - e;
                yield return null;
            }
            cg.alpha = 0f;
            IsShowing = false;
            Destroy(cg.gameObject);
        }

        private float PopInScale(float t)
        {
            float c1 = 1.70158f + popInOvershoot;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
        }
    }
}
