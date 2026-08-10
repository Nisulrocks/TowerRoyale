using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.UI
{
    public class CloudSettingsUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text accountLabel;
        [SerializeField] private TMP_Text emailLabel;
        [SerializeField] private Button signInButton;
        [SerializeField] private Button signOutButton;
        [SerializeField] private TMP_Text feedbackText;

        private void OnEnable()
        {
            FirebaseService.OnSignInComplete += HandleSignInComplete;
            FirebaseService.OnSignInFailed += HandleSignInFailed;
            FirebaseService.OnSignOut += HandleSignOut;
            PlayerProfile.OnCloudProfileLoaded += HandleCloudProfileLoaded;

            UpdateUI();
        }

        private void OnDisable()
        {
            FirebaseService.OnSignInComplete -= HandleSignInComplete;
            FirebaseService.OnSignInFailed -= HandleSignInFailed;
            FirebaseService.OnSignOut -= HandleSignOut;
            PlayerProfile.OnCloudProfileLoaded -= HandleCloudProfileLoaded;
        }

        private void Start()
        {
            if (signInButton != null)
                signInButton.onClick.AddListener(OnSignInClicked);
            if (signOutButton != null)
                signOutButton.onClick.AddListener(OnSignOutClicked);

            UpdateUI();
        }

        private void OnSignInClicked()
        {
            if (feedbackText != null) feedbackText.text = "Opening browser...";
            if (signInButton != null) signInButton.interactable = false;

            if (FirebaseService.Instance != null)
                FirebaseService.Instance.SignInWithGoogle();
        }

        private void OnSignOutClicked()
        {
            if (FirebaseService.Instance != null)
                FirebaseService.Instance.SignOut();
        }

        private void HandleSignInComplete(string uid, string displayName)
        {
            if (feedbackText != null) feedbackText.text = "Loading cloud profile...";
            UpdateUI();
        }

        private void HandleCloudProfileLoaded()
        {
            if (feedbackText != null) feedbackText.text = "Cloud profile loaded!";
            UpdateUI();
        }

        private void HandleSignInFailed(string error)
        {
            if (feedbackText != null) feedbackText.text = $"Failed: {error}";
            if (signInButton != null) signInButton.interactable = true;
            UpdateUI();
        }

        private void HandleSignOut()
        {
            if (feedbackText != null) feedbackText.text = "Signed out. Playing as guest.";
            UpdateUI();
        }

        private void UpdateUI()
        {
            bool signedIn = FirebaseService.IsSignedIn;

            bool matchRunning = MatchContext.IsMatchInProgress;
            if (signInButton != null)
            {
                signInButton.gameObject.SetActive(!signedIn);
                signInButton.interactable = !matchRunning;
            }
            if (signOutButton != null)
            {
                signOutButton.gameObject.SetActive(signedIn);
                signOutButton.interactable = !matchRunning;
            }

            if (statusLabel != null)
                statusLabel.text = signedIn ? "Cloud: Connected" : "Cloud: Guest (local only)";

            if (feedbackText != null && signedIn && matchRunning)
                feedbackText.text = "You can't sign out during a match.";

            if (accountLabel != null)
            {
                if (signedIn)
                    accountLabel.text = FirebaseService.DisplayName ?? FirebaseService.UserId;
                else
                    accountLabel.text = "";
            }

            if (emailLabel != null)
            {
                if (signedIn && !string.IsNullOrEmpty(FirebaseService.Email))
                    emailLabel.text = FirebaseService.Email;
                else
                    emailLabel.text = "";
            }
        }
    }
}
