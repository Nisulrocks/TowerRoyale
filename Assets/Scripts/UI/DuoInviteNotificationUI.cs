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

            SetButtons(true);

            if (_countdown != null) StopCoroutine(_countdown);
            _countdown = StartCoroutine(CountdownRoutine());
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
