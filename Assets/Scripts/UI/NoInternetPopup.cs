using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using TR.Audio;

namespace TR.UI
{
    public class NoInternetPopup : MonoBehaviour
    {
        [Header("Message")]
        [SerializeField] private TMP_Text messageText;
        [SerializeField] [TextArea] private string message = "No internet connection detected.\nPlease check your network and try again.";

        [Header("Buttons")]
        [Tooltip("Optional retry button — reloads the current scene or the configured retry scene.")]
        [SerializeField] private Button retryButton;
        [Tooltip("Optional quit button — closes the application.")]
        [SerializeField] private Button quitButton;
        [Tooltip("If set, Retry loads this scene. If empty, Retry reloads the currently active scene.")]
        [SerializeField] private string retrySceneName = "";

        [Header("SFX (Optional)")]
        [SerializeField] private string showSfxKey = "";

        public string RetrySceneName { get => retrySceneName; set => retrySceneName = value; }

        public event System.Action OnRetry;
        public event System.Action OnQuit;

        private void Awake()
        {
            if (messageText != null)
                messageText.text = message;

            if (retryButton != null)
                retryButton.onClick.AddListener(Retry);

            if (quitButton != null)
                quitButton.onClick.AddListener(Quit);
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(showSfxKey) && SFXManager.Instance != null)
                SFXManager.Instance.Play(showSfxKey);
        }

        public void SetMessage(string msg)
        {
            message = msg;
            if (messageText != null)
                messageText.text = message;
        }

        public void Retry()
        {
            OnRetry?.Invoke();
            string scene = string.IsNullOrEmpty(retrySceneName) ? SceneManager.GetActiveScene().name : retrySceneName;
            Destroy(gameObject);
            SceneManager.LoadScene(scene);
        }

        public void Quit()
        {
            OnQuit?.Invoke();
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void OnDestroy()
        {
            if (retryButton != null)
                retryButton.onClick.RemoveListener(Retry);

            if (quitButton != null)
                quitButton.onClick.RemoveListener(Quit);
        }
    }
}
