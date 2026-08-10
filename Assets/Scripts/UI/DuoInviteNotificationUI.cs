using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.UI
{
    // Shown when a friend invites you to a duo match. Accepting joins their Photon room by name.
    public class DuoInviteNotificationUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;

        [Tooltip("Optional. Lets the accept button read 'Invite Back' for a missed invite.")]
        [SerializeField] private TMP_Text acceptLabel;
        [SerializeField] private string acceptText = "Accept";
        [SerializeField] private string inviteBackText = "Invite Back";

        [Tooltip("Seconds before the invite expires on its own.")]
        [SerializeField] private float lifetimeSeconds = 30f;

        private FriendsService.DuoInviteInfo _invite;
        private System.Action<FriendsService.DuoInviteInfo> _onAccept;
        private Coroutine _countdown;

        private void Awake()
        {
            if (acceptButton != null) acceptButton.onClick.AddListener(Accept);
            if (declineButton != null) declineButton.onClick.AddListener(Decline);
            gameObject.SetActive(false);
        }

        // Takes a callback rather than the Friends panel so the popup can be driven from anywhere.
        public void Show(FriendsService.DuoInviteInfo invite, System.Action<FriendsService.DuoInviteInfo> onAccept)
        {
            _invite = invite;
            _onAccept = onAccept;
            if (invite == null) return;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            if (messageText != null)
                messageText.text = $"{invite.fromName} invited you to a Duo match!";

            if (timerText != null) timerText.gameObject.SetActive(true);
            if (acceptLabel != null) acceptLabel.text = acceptText;

            // ShowNotice hides accept, so restore it for the interactive modes.
            if (acceptButton != null) acceptButton.gameObject.SetActive(true);
            SetButtons(true);

            if (_countdown != null) StopCoroutine(_countdown);
            _countdown = StartCoroutine(CountdownRoutine());
        }

        // An invite that expired while the player was away. The room no longer exists, so accepting
        // sends a fresh invite back rather than trying to join something dead. No countdown either,
        // since there is nothing left to expire.
        public void ShowMissed(FriendsService.DuoInviteInfo invite, System.Action<FriendsService.DuoInviteInfo> onInviteBack)
        {
            _invite = invite;
            _onAccept = onInviteBack;
            if (invite == null) return;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            if (messageText != null)
                messageText.text = $"{invite.fromName} invited you to a Duo match while you were away.";

            if (timerText != null) timerText.gameObject.SetActive(false);
            if (acceptLabel != null) acceptLabel.text = inviteBackText;

            // ShowNotice hides accept, so restore it for the interactive modes.
            if (acceptButton != null) acceptButton.gameObject.SetActive(true);
            SetButtons(true);

            if (_countdown != null) { StopCoroutine(_countdown); _countdown = null; }
        }

        // Information only, with nothing to accept — used when an invite back cannot go anywhere
        // (the friend went offline, or moved to another arena).
        public void ShowNotice(string message)
        {
            _invite = null;
            _onAccept = null;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            if (messageText != null) messageText.text = message;
            if (timerText != null) timerText.gameObject.SetActive(false);
            if (acceptButton != null) acceptButton.gameObject.SetActive(false);
            if (declineButton != null)
            {
                declineButton.gameObject.SetActive(true);
                declineButton.interactable = true;
            }

            if (_countdown != null) { StopCoroutine(_countdown); _countdown = null; }
        }

        private IEnumerator CountdownRoutine()
        {
            float remaining = Mathf.Max(1f, lifetimeSeconds);
            while (remaining > 0f)
            {
                if (timerText != null) timerText.text = $"{Mathf.CeilToInt(remaining)}s";
                // Unscaled so the countdown still runs if something paused the game.
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
            Decline();
        }

        private void Accept()
        {
            if (_invite == null) return;
            SetButtons(false);
            if (_countdown != null) { StopCoroutine(_countdown); _countdown = null; }

            var invite = _invite;
            var callback = _onAccept;
            Dismiss();
            callback?.Invoke(invite);
        }

        private void Decline()
        {
            if (_invite != null && FriendsService.Instance != null)
                FriendsService.Instance.ClearInviteFrom(_invite.fromUid);
            Dismiss();
        }

        private void Dismiss()
        {
            if (_countdown != null) { StopCoroutine(_countdown); _countdown = null; }
            _invite = null;
            gameObject.SetActive(false);
        }

        private void SetButtons(bool interactable)
        {
            if (acceptButton != null) acceptButton.interactable = interactable;
            if (declineButton != null) declineButton.interactable = interactable;
        }
    }
}
