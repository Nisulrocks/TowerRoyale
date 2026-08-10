using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Data;
using TR.Audio;

namespace TR.UI
{
    
    
    
    public class LobbyArenaUnlockNotifier : MonoBehaviour
    {
        [Header("Prefab & Parent")]

        [SerializeField] private GameObject popupPrefab;

        [SerializeField] private RectTransform parent;

        [Header("Text Template")]

        [SerializeField] private string headerText = "New Arena Unlocked!";

        [SerializeField] private string encouragingText = "Keep pushing forward, {0}!";

        [Header("Animation")]
        [SerializeField] private float initialDelay = 0.75f;
        [SerializeField] private float popInTime = 0.35f;
        [SerializeField] private float popInOvershoot = 0f;
        [SerializeField] private float holdTime = 2.0f;
        [SerializeField] private float fadeOutTime = 0.3f;

        [Header("Popup References")]
        [SerializeField] private string iconChildName = "Icon";

        [Header("SFX (Optional)")]
        [SerializeField] private string sfxKey = "ui_lobby_arena_unlock";

        public bool autoPlay = true;

        public bool IsShowing { get; private set; }

        private void Start()
        {
            if (autoPlay)
                StartCoroutine(WaitThenTryShow());
        }

        private void OnEnable()
        {
            PlayerProfile.OnTrophiesChanged += HandleTrophiesChanged;
        }

        private void OnDisable()
        {
            PlayerProfile.OnTrophiesChanged -= HandleTrophiesChanged;
        }

        private void HandleTrophiesChanged(int trophies)
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
            if (!PlayerProfile.TryConsumePendingArenaUnlock(out string arenaName)) return;

            IsShowing = true;

            GameDB.EnsureLoaded();
            
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

            
            var parentRt = parent != null ? parent : GetDefaultCanvasParent();
            var go = Instantiate(popupPrefab, parentRt);
            go.SetActive(true);

            
            Image img = null;
            if (!string.IsNullOrEmpty(iconChildName))
            {
                var iconTrans = go.transform.Find(iconChildName);
                if (iconTrans != null) img = iconTrans.GetComponent<Image>();
            }
            if (img == null) img = go.GetComponentInChildren<Image>(true);

            var txt = go.GetComponentInChildren<TMP_Text>(true);
            if (img != null) img.sprite = icon;
            if (txt != null)
            {
                if (string.IsNullOrEmpty(encouragingText)) encouragingText = "Keep going!";
                string line2 = string.Format(encouragingText, arenaName);
                
                txt.text = string.IsNullOrEmpty(headerText)
                    ? $"{arenaName}\n{line2}"
                    : $"{headerText}\n{arenaName}\n{line2}";
            }

            
            if (!string.IsNullOrEmpty(sfxKey) && SFXManager.Instance != null)
            {
                SFXManager.Instance.Play(sfxKey);
            }

            
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
