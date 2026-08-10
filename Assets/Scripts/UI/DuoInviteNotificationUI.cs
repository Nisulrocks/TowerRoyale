using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.UI
{
    public class DuoInviteNotificationUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;

        [SerializeField] private TMP_Text acceptLabel;
        [SerializeField] private string acceptText = "Accept";
        [SerializeField] private string inviteBackText = "Invite Back";

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

            if (acceptButton != null) acceptButton.gameObject.SetActive(true);
            SetButtons(true);

            if (_countdown != null) StopCoroutine(_countdown);
            _countdown = StartCoroutine(CountdownRoutine());
        }

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

            if (acceptButton != null) acceptButton.gameObject.SetActive(true);
            SetButtons(true);

            if (_countdown != null) { StopCoroutine(_countdown); _countdown = null; }
        }

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
