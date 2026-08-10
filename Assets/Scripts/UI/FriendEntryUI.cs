using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.UI
{
    public class FriendEntryUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text trophiesText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image onlineDot;
        [SerializeField] private Button inviteButton;

        [SerializeField] private TMP_Text arenaText;
        [SerializeField] private TMP_Text inviteLabel;

        [SerializeField] private Color onlineColor = new Color(0.3f, 0.9f, 0.4f, 1f);
        [SerializeField] private Color offlineColor = new Color(0.45f, 0.45f, 0.5f, 1f);
        [SerializeField] private Color mismatchColor = new Color(0.95f, 0.65f, 0.25f, 1f);

        private FriendsService.PlayerSummary _data;
        private FriendsPanelUI _panel;

        public FriendsService.PlayerSummary Data => _data;

        public void Bind(FriendsService.PlayerSummary data, FriendsPanelUI panel)
        {
            _data = data;
            _panel = panel;
            if (data == null) return;

            if (nameText != null) nameText.text = data.playerName;
            if (trophiesText != null) trophiesText.text = data.trophies.ToString();

            bool sameArena = data.IsSameArenaAsLocal;

            if (statusText != null)
            {
                if (!data.isOnline) statusText.text = "Offline";
                else if (data.isInMatch) statusText.text = "In a match";
                else if (!sameArena) statusText.text = "Different arena";
                else statusText.text = "Online";
            }

            if (onlineDot != null)
            {
                onlineDot.color = !data.isOnline ? offlineColor
                                : data.CanInviteToDuo ? onlineColor
                                : mismatchColor;
            }

            if (arenaText != null)
                arenaText.text = string.IsNullOrEmpty(data.arenaName) ? string.Empty : data.arenaName;

            if (inviteButton != null)
            {
                inviteButton.interactable = data.CanInviteToDuo;
                inviteButton.onClick.RemoveAllListeners();
                inviteButton.onClick.AddListener(Invite);
            }

            if (inviteLabel != null)
            {
                if (!data.isOnline) inviteLabel.text = "Offline";
                else if (data.isInMatch) inviteLabel.text = "Busy";
                else if (!sameArena) inviteLabel.text = "Locked";
                else inviteLabel.text = "Invite";
            }
        }

        public void Invite()
        {
            if (_panel != null && _data != null) _panel.InviteToDuo(_data);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || _panel == null || _data == null) return;
            if (eventData.button != PointerEventData.InputButton.Right) return;
            _panel.ShowContextMenu(_data, eventData.position);
        }
    }
}
