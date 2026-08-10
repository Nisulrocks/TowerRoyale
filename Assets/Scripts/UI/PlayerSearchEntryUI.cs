using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.UI
{
    public class PlayerSearchEntryUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text idText;
        [SerializeField] private TMP_Text trophiesText;
        [SerializeField] private Image onlineDot;
        [SerializeField] private Button addButton;
        [SerializeField] private TMP_Text addButtonLabel;

        [SerializeField] private Color onlineColor = new Color(0.3f, 0.9f, 0.4f, 1f);
        [SerializeField] private Color offlineColor = new Color(0.45f, 0.45f, 0.5f, 1f);

        private FriendsService.PlayerSummary _data;

        public void Bind(FriendsService.PlayerSummary data, bool alreadyFriend)
        {
            _data = data;
            if (data == null) return;

            if (nameText != null) nameText.text = data.playerName;
            if (trophiesText != null) trophiesText.text = data.trophies.ToString();
            if (onlineDot != null) onlineDot.color = data.isOnline ? onlineColor : offlineColor;

            if (idText != null)
            {
                string uid = data.uid ?? string.Empty;
                idText.text = uid.Length > 10 ? $"ID: {uid.Substring(0, 10)}..." : $"ID: {uid}";
            }

            if (addButton != null)
            {
                addButton.onClick.RemoveAllListeners();
                addButton.interactable = !alreadyFriend;
                addButton.onClick.AddListener(SendRequest);
            }
            if (addButtonLabel != null) addButtonLabel.text = alreadyFriend ? "Friends" : "Add";
        }

        private void SendRequest()
        {
            if (_data == null || FriendsService.Instance == null) return;
            FriendsService.Instance.SendFriendRequest(_data.uid);

            if (addButton != null) addButton.interactable = false;
            if (addButtonLabel != null) addButtonLabel.text = "Sent";
        }
    }
}
