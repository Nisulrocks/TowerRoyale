using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.UI
{
    public class CloudLoginUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button signInButton;
        [SerializeField] private Button signOutButton;
        [SerializeField] private Button continueAsGuestButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text accountInfoText;
        [SerializeField] private GameObject loadingIndicator;

        [Header("Auto-show on start if not signed in")]
        [SerializeField] private bool autoShowOnStart = false;

        public event System.Action OnContinueAsGuest;

        private void Start()
        {
            if (signInButton != null)
                signInButton.onClick.AddListener(OnSignInClicked);
            if (signOutButton != null)
                signOutButton.onClick.AddListener(OnSignOutClicked);
            if (continueAsGuestButton != null)
                continueAsGuestButton.onClick.AddListener(OnContinueAsGuestClicked);

            FirebaseService.OnSignInComplete += HandleSignInComplete;
            FirebaseService.OnSignInFailed += HandleSignInFailed;
            FirebaseService.OnSignOut += HandleSignOut;
            PlayerProfile.OnCloudProfileLoaded += HandleCloudProfileLoaded;

            UpdateUI();

            if (autoShowOnStart && !FirebaseService.IsSignedIn)
                Show();
        }

        private void OnDestroy()
        {
            FirebaseService.OnSignInComplete -= HandleSignInComplete;
            FirebaseService.OnSignInFailed -= HandleSignInFailed;
            FirebaseService.OnSignOut -= HandleSignOut;
            PlayerProfile.OnCloudProfileLoaded -= HandleCloudProfileLoaded;
        }

        public void Show()
        {
            if (panel != null) panel.SetActive(true);
            UpdateUI();
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnSignInClicked()
        {
            if (statusText != null) statusText.text = "Opening browser for Google sign-in...";
            if (loadingIndicator != null) loadingIndicator.SetActive(true);
            if (signInButton != null) signInButton.interactable = false;

            // Guest must stay available: if the browser is closed the sign-in cannot complete, and
            // this is the player's only way out before the timeout.
            if (continueAsGuestButton != null) continueAsGuestButton.interactable = true;

            if (FirebaseService.Instance != null)
                FirebaseService.Instance.SignInWithGoogle();
            else
                HandleSignInFailed("FirebaseService not found in scene.");
        }

        private void Update()
        {
            // Without this the screen sits silent for the whole timeout and reads as a freeze.
            if (!GoogleOAuthHandler.IsWaitingForBrowser) return;
            if (statusText == null) return;

            int secs = Mathf.CeilToInt(GoogleOAuthHandler.SecondsRemaining);
            statusText.text =
                $"Waiting for you to finish signing in ({secs}s)...\n" +
                "Closed the browser? Press Sign In again or continue as a guest.";

            // Let them retry immediately rather than waiting out the countdown.
            if (signInButton != null && !signInButton.interactable)
                signInButton.interactable = true;
        }

        private void OnSignOutClicked()
        {
            if (FirebaseService.Instance != null)
                FirebaseService.Instance.SignOut();
        }

        private void OnContinueAsGuestClicked()
        {
            OnContinueAsGuest?.Invoke();
            Hide();
        }

        private void HandleSignInComplete(string uid, string displayName)
        {
            if (statusText != null) statusText.text = "Signed in! Loading cloud profile...";
            if (CloudProfileService.Instance != null)
                CloudProfileService.Instance.LoadProfile(uid);
            else
                HandleSignInFailed("CloudProfileService not found in scene.");
        }

        private void HandleCloudProfileLoaded()
        {
            if (statusText != null) statusText.text = "Cloud profile loaded!";
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            UpdateUI();
            Hide();
        }

        private void HandleSignInFailed(string error)
        {
            if (statusText != null) statusText.text = $"Sign-in failed: {error}";
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            if (signInButton != null) signInButton.interactable = true;
        }

        private void HandleSignOut()
        {
            if (statusText != null) statusText.text = "Signed out. Playing as guest.";
            UpdateUI();
        }

        private void UpdateUI()
        {
            bool signedIn = FirebaseService.IsSignedIn;

            if (signInButton != null) signInButton.gameObject.SetActive(!signedIn);
            if (signOutButton != null)
            {
                signOutButton.gameObject.SetActive(signedIn);
                // Blocked mid-match; FirebaseService.SignOut refuses too, this is just the cue.
                signOutButton.interactable = !MatchContext.IsMatchInProgress;
            }
            if (continueAsGuestButton != null) continueAsGuestButton.gameObject.SetActive(!signedIn);
            if (loadingIndicator != null) loadingIndicator.SetActive(false);

            if (accountInfoText != null)
            {
                if (signedIn)
                    accountInfoText.text = $"Connected as: {FirebaseService.DisplayName ?? FirebaseService.UserId}";
                else
                    accountInfoText.text = "Playing as Guest (local only)";
            }

            if (statusText != null && string.IsNullOrEmpty(statusText.text))
                statusText.text = signedIn ? "Connected" : "";
        }
    }
}
