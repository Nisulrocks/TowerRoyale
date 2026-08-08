using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.UI
{
    // An incoming friend request, with accept / decline.
    public class FriendRequestEntryUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;

        private FriendsService.FriendRequestInfo _data;

        public void Bind(FriendsService.FriendRequestInfo data)
        {
            _data = data;
            if (data == null) return;

            if (nameText != null) nameText.text = $"{data.fromName} wants to be friends";

            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveAllListeners();
                acceptButton.onClick.AddListener(Accept);
            }
            if (declineButton != null)
            {
                declineButton.onClick.RemoveAllListeners();
                declineButton.onClick.AddListener(Decline);
            }
        }

        private void Accept()
        {
            if (_data == null || FriendsService.Instance == null) return;
            SetButtons(false);
            FriendsService.Instance.AcceptFriendRequest(_data.fromUid, _data.fromName);
        }

        private void Decline()
        {
            if (_data == null || FriendsService.Instance == null) return;
            SetButtons(false);
            FriendsService.Instance.DeclineFriendRequest(_data.fromUid);
        }

        // The list is rebuilt from Firestore right after either action; disabling in the meantime
        // stops a double tap from firing the request twice.
        private void SetButtons(bool interactable)
        {
            if (acceptButton != null) acceptButton.interactable = interactable;
            if (declineButton != null) declineButton.interactable = interactable;
        }
    }
}
