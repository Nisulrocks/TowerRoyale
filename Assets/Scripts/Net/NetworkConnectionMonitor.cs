using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using TR.Systems;
using TR.UI;

namespace TR.Net
{
    public class NetworkConnectionMonitor : MonoBehaviourPunCallbacks
    {
        public static NetworkConnectionMonitor Instance { get; private set; }

        [Header("Popup")]
        [Tooltip("Same NoInternetPopup prefab used by LoadingScreen.")]
        [SerializeField] private GameObject noInternetPopupPrefab;
        [Tooltip("Scene to load when Retry is pressed. Use your Boot scene name.")]
        [SerializeField] private string bootSceneName = "Boot";

        [Header("Polling")]
        [Tooltip("If true, also poll Application.internetReachability between Photon callbacks.")]
        [SerializeField] private bool checkInternetReachability = true;
        [Tooltip("Seconds between reachability polls.")]
        [SerializeField] private float checkInterval = 1f;

        [Header("Ignored Disconnect Causes")]
        [SerializeField] private List<DisconnectCause> ignoredDisconnectCauses = new List<DisconnectCause>
        {
            DisconnectCause.DisconnectByClientLogic,
            DisconnectCause.ApplicationQuit
        };

        private bool _intentionalDisconnect;
        private bool _popupOpen;
        private GameObject _popupInstance;
        private float _timer;

        private bool _pausedGame;
        private float _savedTimeScale = 1f;

        protected void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static void Initialize(GameObject popupPrefab, string retryScene)
        {
            if (Instance == null)
            {
                var go = new GameObject("NetworkConnectionMonitor");
                Instance = go.AddComponent<NetworkConnectionMonitor>();
                DontDestroyOnLoad(go);
            }

            if (popupPrefab != null)
                Instance.noInternetPopupPrefab = popupPrefab;
            if (!string.IsNullOrEmpty(retryScene))
                Instance.bootSceneName = retryScene;
        }

        public void MarkIntentionalDisconnect()
        {
            _intentionalDisconnect = true;
        }

        public override void OnConnectedToMaster()
        {
            _intentionalDisconnect = false;
            ClosePopup();
        }

        public override void OnJoinedRoom()
        {
            _intentionalDisconnect = false;
            ClosePopup();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (_intentionalDisconnect)
            {
                _intentionalDisconnect = false;
                return;
            }

            if (ignoredDisconnectCauses.Contains(cause))
                return;

            if (SceneManager.GetActiveScene().name == bootSceneName)
                return;

            string message = "Connection lost.\nPlease check your network and try again.";
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                message = "No internet connection detected.\nPlease check your network and try again.";
            }
            else if (cause != DisconnectCause.None)
            {
                message = $"Connection lost: {cause}\nPlease check your network and try again.";
            }

            ShowPopup(message);
        }

        private void Update()
        {
            if (_popupInstance == null)
                _popupOpen = false;

            if (!checkInternetReachability || noInternetPopupPrefab == null)
                return;

            _timer += Time.unscaledDeltaTime;
            if (_timer < checkInterval)
                return;
            _timer = 0f;

            if (SceneManager.GetActiveScene().name == bootSceneName)
                return;
            if (PhotonNetwork.OfflineMode)
                return;
            if (PhotonNetwork.IsConnectedAndReady)
                return;

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                ShowPopup("No internet connection detected.\nPlease check your network and try again.");
            }
        }

        private void ShowPopup(string message)
        {
            if (_popupOpen)
                return;
            if (noInternetPopupPrefab == null)
                return;
            if (SceneManager.GetActiveScene().name == bootSceneName)
                return;

            var parent = FindCanvasParent();
            if (parent == null)
            {
                Debug.LogWarning("[NetworkConnectionMonitor] No Canvas found to show popup.");
                return;
            }

            _popupInstance = Instantiate(noInternetPopupPrefab, parent);
            _popupInstance.SetActive(true);
            _popupOpen = true;

            var popup = _popupInstance.GetComponent<NoInternetPopup>();
            if (popup != null)
            {
                popup.SetMessage(message);
                popup.RetrySceneName = bootSceneName;
                popup.OnRetry += () => { ResumeGame(); ClosePopup(); };
            }
            else
            {
                var txt = _popupInstance.GetComponentInChildren<TMP_Text>(true);
                if (txt != null)
                    txt.text = message;
            }

            PauseGameIfNeeded();
        }

        private void PauseGameIfNeeded()
        {
            if (_pausedGame) return;
            if (MatchContext.IsDuo) return;

            string sceneName = SceneManager.GetActiveScene().name;
            if (!sceneName.EndsWith("BattleScene", System.StringComparison.OrdinalIgnoreCase))
                return;

            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _pausedGame = true;
        }

        private void ResumeGame()
        {
            if (!_pausedGame) return;

            Time.timeScale = _savedTimeScale <= 0f ? 1f : _savedTimeScale;
            _pausedGame = false;
        }

        private RectTransform FindCanvasParent()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c != null && (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera))
                    return c.transform as RectTransform;
            }
            return null;
        }

        private void ClosePopup()
        {
            ResumeGame();
            if (_popupInstance != null)
                Destroy(_popupInstance);
            _popupOpen = false;
            _popupInstance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            ClosePopup();
        }
    }
}
