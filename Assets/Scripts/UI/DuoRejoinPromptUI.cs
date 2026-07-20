using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Net;

namespace TR.UI
{
    
    
    public class DuoRejoinPromptUI : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("The root panel that will be shown/hidden. Can be this object or a child.")]
        [SerializeField] private GameObject contentPanel;

        [Header("Matchmaking UI (optional)")]
        [Tooltip("If assigned, this panel is shown with 'Reconnecting...' and the arena icon while reconnecting.")]
        [SerializeField] private DuoMatchmakingUI matchmakingUI;

        [Header("Text")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Buttons")]
        [SerializeField] private Button rejoinButton;
        [SerializeField] private Button abandonButton;

        [Header("Copy")]
        [SerializeField] private string title = "Match Still Active";
        [SerializeField, TextArea(2, 4)]
        private string description = "You have an unfinished duo match. Rejoin now?";
        [SerializeField] private string rejoinLabel = "Rejoin";
        [SerializeField] private string abandonLabel = "Abandon";

        [Header("Match Ended Copy")]
        [SerializeField] private string matchEndedTitle = "Match Ended";
        [SerializeField, TextArea(2, 4)]
        private string matchEndedDescription = "Your last duo match has already ended.";
        [SerializeField] private string dismissLabel = "OK";
        [SerializeField] private float endedAutoDestroyDelay = 3f;

        private bool _autoDestroyStarted;

        private void Awake()
        {
            ResolveOptionalRefs();
            if (contentPanel == null) contentPanel = gameObject;

            if (titleText != null) titleText.text = title;
            if (descriptionText != null) descriptionText.text = description;
            if (rejoinButton != null)
            {
                rejoinButton.onClick.RemoveAllListeners();
                rejoinButton.onClick.AddListener(OnRejoinClicked);
                var rt = rejoinButton.GetComponentInChildren<TMP_Text>();
                if (rt != null) rt.text = rejoinLabel;
            }
            if (abandonButton != null)
            {
                abandonButton.onClick.RemoveAllListeners();
                abandonButton.onClick.AddListener(OnAbandonClicked);
                var at = abandonButton.GetComponentInChildren<TMP_Text>();
                if (at != null) at.text = abandonLabel;
            }

            
            var svc = FindFirstObjectByType<DuoRejoinService>();
            if (svc == null)
            {
                var go = new GameObject("DuoRejoinService");
                svc = go.AddComponent<DuoRejoinService>();
            }
            svc.OnRejoinComplete += HandleRejoinComplete;

            Refresh();
        }

        private void ResolveOptionalRefs()
        {
            if (contentPanel == null) contentPanel = transform.Find("Panel")?.gameObject;
            if (matchmakingUI == null || (matchmakingUI.gameObject != null && !matchmakingUI.gameObject.scene.IsValid()))
            {
                matchmakingUI = FindValidMatchmakingUI();
            }
            if (titleText == null) titleText = FindTextInChildren("Title");
            if (descriptionText == null) descriptionText = FindTextInChildren("Description");
            if (rejoinButton == null) rejoinButton = FindButtonInChildren("Rejoin");
            if (abandonButton == null) abandonButton = FindButtonInChildren("Abandon");
        }

        private DuoMatchmakingUI FindValidMatchmakingUI()
        {
            var all = FindObjectsOfType<DuoMatchmakingUI>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var ui = all[i];
                if (ui != null && ui.gameObject.scene.IsValid()) return ui;
            }
            return null;
        }

        private TMP_Text FindTextInChildren(string name)
        {
            var t = transform.Find(name);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private Button FindButtonInChildren(string name)
        {
            var t = transform.Find(name);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private void Start()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            var svc = DuoRejoinService.Instance;
            if (svc != null) svc.OnRejoinComplete -= HandleRejoinComplete;
        }

        private void Refresh()
        {
            bool hasActive = DuoRejoinService.HasActiveMatch;
            bool ended = DuoRejoinService.IsMatchEnded;
            Debug.Log($"[DuoRejoinPromptUI] Refresh: HasActiveMatch={hasActive}, IsMatchEnded={ended}, contentPanel={(contentPanel != null ? contentPanel.name : "null")}");

            if (!hasActive && !ended)
            {
                if (contentPanel != null) contentPanel.SetActive(false);
                return;
            }

            if (contentPanel != null) contentPanel.SetActive(true);
            SetPromptElementsActive(true);

            if (ended)
            {
                if (titleText != null)
                {
                    titleText.text = matchEndedTitle;
                    titleText.color = Color.red;
                }
                if (descriptionText != null)
                {
                    descriptionText.text = matchEndedDescription;
                    descriptionText.color = Color.red;
                }
                if (rejoinButton != null) rejoinButton.gameObject.SetActive(false);
                if (abandonButton != null) abandonButton.gameObject.SetActive(false);

                if (!_autoDestroyStarted)
                {
                    _autoDestroyStarted = true;
                    StartCoroutine(AutoDestroyAfterDelay(endedAutoDestroyDelay));
                }
                return;
            }

            _autoDestroyStarted = false;
            if (titleText != null)
            {
                titleText.text = title;
                titleText.color = Color.white;
            }
            if (descriptionText != null)
            {
                descriptionText.text = description;
                descriptionText.color = Color.white;
            }
            if (rejoinButton != null)
            {
                rejoinButton.gameObject.SetActive(true);
                var rt = rejoinButton.GetComponentInChildren<TMP_Text>();
                if (rt != null) rt.text = rejoinLabel;
            }
            if (abandonButton != null)
            {
                abandonButton.gameObject.SetActive(true);
                var at = abandonButton.GetComponentInChildren<TMP_Text>();
                if (at != null) at.text = abandonLabel;
            }
        }

        private void OnRejoinClicked()
        {
            ShowMatchmakingUI();
            DuoRejoinService.Instance?.BeginRejoin();
        }

        private void OnAbandonClicked()
        {
            DuoRejoinService.Instance?.AbandonMatch();
            if (contentPanel != null) contentPanel.SetActive(false);
            HideMatchmakingUI();
        }

        private void HandleRejoinComplete(bool success)
        {
            if (success)
            {
                if (matchmakingUI != null)
                {
                    matchmakingUI.SetCancelOverride(null);
                    matchmakingUI.SetStatus("Match found! Loading...");
                }
                return;
            }

            HideMatchmakingUI();
            Refresh();
        }

        private void SetPromptElementsActive(bool active)
        {
            if (titleText != null) titleText.gameObject.SetActive(active);
            if (descriptionText != null) descriptionText.gameObject.SetActive(active);
            if (rejoinButton != null) rejoinButton.gameObject.SetActive(active);
            if (abandonButton != null) abandonButton.gameObject.SetActive(active);
        }

        private void ShowMatchmakingUI()
        {
            if (matchmakingUI == null) return;

            if (contentPanel != null)
            {
                bool matchmakingIsChild = matchmakingUI.transform.IsChildOf(contentPanel.transform);
                contentPanel.SetActive(matchmakingIsChild);
                if (matchmakingIsChild) SetPromptElementsActive(false);
            }

            matchmakingUI.SetCancelOverride(() =>
            {
                DuoRejoinService.Instance?.AbandonMatch();
                HideMatchmakingUI();
                if (contentPanel != null) contentPanel.SetActive(true);
                SetPromptElementsActive(true);
                if (descriptionText != null) descriptionText.text = "You have an unfinished duo match. Rejoin now?";
            });

            string arenaId = DuoRejoinService.SavedArenaId;
            var arena = string.IsNullOrEmpty(arenaId) ? null : TR.Systems.GameDB.GetArenaById(arenaId);
            matchmakingUI.ShowForReconnect(arena != null ? arena.ArenaImage : null, "Reconnecting...");
        }

        private void HideMatchmakingUI()
        {
            matchmakingUI?.HideImmediate();
        }

        private System.Collections.IEnumerator AutoDestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            DuoRejoinService.Instance?.AbandonMatch();
            Destroy(gameObject);
        }
    }
}
