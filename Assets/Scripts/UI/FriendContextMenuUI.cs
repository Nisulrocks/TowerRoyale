using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.UI
{
    // Small right-click popup over a friend row: invite to a duo match, or remove the friend.
    // Closes on any click that lands outside its own rect.
    public class FriendContextMenuUI : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private Button inviteButton;
        [SerializeField] private TMP_Text inviteLabel;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button closeButton;

        private FriendsService.PlayerSummary _target;
        private FriendsPanelUI _panel;
        private Canvas _canvas;

        private void Awake()
        {
            if (panel == null) panel = transform as RectTransform;
            _canvas = GetComponentInParent<Canvas>();

            if (inviteButton != null) inviteButton.onClick.AddListener(Invite);
            if (removeButton != null) removeButton.onClick.AddListener(Remove);
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            gameObject.SetActive(false);
        }

        public void Open(FriendsService.PlayerSummary target, Vector2 screenPos, FriendsPanelUI panelUI)
        {
            _target = target;
            _panel = panelUI;
            if (target == null) return;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            if (headerText != null) headerText.text = target.playerName;

            // Same rule as the row's button: online, and in our arena.
            if (inviteButton != null) inviteButton.interactable = target.CanInviteToDuo;
            if (inviteLabel != null)
            {
                if (!target.isOnline) inviteLabel.text = "Offline";
                else if (!target.IsSameArenaAsLocal)
                    inviteLabel.text = string.IsNullOrEmpty(target.arenaName)
                        ? "Different arena"
                        : $"In {target.arenaName}";
                else inviteLabel.text = "Invite to Duo";
            }

            PositionAt(screenPos);
        }

        private void PositionAt(Vector2 screenPos)
        {
            if (panel == null) return;
            var parentRect = panel.parent as RectTransform;
            if (parentRect == null) return;

            Camera cam = null;
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = _canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, cam, out Vector2 local))
            {
                panel.anchoredPosition = local;
                ClampInsideParent(parentRect);
            }
        }

        // Keep the whole menu on screen when it is opened near an edge.
        private void ClampInsideParent(RectTransform parentRect)
        {
            Vector2 size = panel.rect.size;
            Vector2 pivot = panel.pivot;
            Vector2 parentSize = parentRect.rect.size;

            float minX = -parentSize.x * 0.5f + size.x * pivot.x;
            float maxX = parentSize.x * 0.5f - size.x * (1f - pivot.x);
            float minY = -parentSize.y * 0.5f + size.y * pivot.y;
            float maxY = parentSize.y * 0.5f - size.y * (1f - pivot.y);

            Vector2 p = panel.anchoredPosition;
            p.x = Mathf.Clamp(p.x, minX, maxX);
            p.y = Mathf.Clamp(p.y, minY, maxY);
            panel.anchoredPosition = p;
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;

            // Dismiss on a click outside the menu.
            bool clicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            if (!clicked) return;

            Camera cam = null;
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = _canvas.worldCamera;

            if (!RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition, cam))
                Close();
        }

        private void Invite()
        {
            if (_panel != null && _target != null) _panel.InviteToDuo(_target);
            Close();
        }

        private void Remove()
        {
            if (_target != null && FriendsService.Instance != null)
                FriendsService.Instance.RemoveFriend(_target.uid);
            Close();
        }

        public void Close()
        {
            _target = null;
            gameObject.SetActive(false);
        }
    }
}
