using UnityEngine;
using UnityEngine.EventSystems;
using TR.UI;

namespace TR.Battle
{
    // Click to toggle selection on a tower: shows range ring and emits an event for UI panels.
    public class TowerSelectable : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TowerBase tower;
        [SerializeField] private bool toggle = true;
        private bool _selected;
        public bool Selected => _selected;

        public static System.Action<TowerBase, bool> OnTowerSelectionChanged; // (tower, selected)

        private void Awake()
        {
            if (tower == null) tower = GetComponent<TowerBase>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (tower == null) return;
            if (toggle) SetSelected(!_selected); else SetSelected(true);
        }

        private void OnDisable()
        {
            if (tower != null) tower.ShowRangeRing(false);
            if (_selected)
            {
                _selected = false;
                OnTowerSelectionChanged?.Invoke(tower, false);
                // Hide only if this was the selected tower
                HoverCardDetailsUI.Hide();
            }
        }

        public void SetSelected(bool value)
        {
            _selected = value;
            if (tower != null)
            {
                tower.ShowRangeRing(_selected);
            }
            OnTowerSelectionChanged?.Invoke(tower, _selected);

            // Reuse the existing HoverCardDetailsUI to display tower info; bind to tower for live updates
            if (_selected && tower != null && tower.Definition != null)
            {
                HoverCardDetailsUI.Show(tower);
            }
            else
            {
                HoverCardDetailsUI.Hide();
            }
        }
    }
}
