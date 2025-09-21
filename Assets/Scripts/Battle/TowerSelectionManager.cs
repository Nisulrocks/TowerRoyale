using UnityEngine;

namespace TR.Battle
{
    // Ensures only one tower is selected at a time. Drop this in the battle scene once.
    public class TowerSelectionManager : MonoBehaviour
    {
        private bool _handling;

        private void OnEnable()
        {
            TowerSelectable.OnTowerSelectionChanged += HandleSelectionChanged;
        }

        private void OnDisable()
        {
            TowerSelectable.OnTowerSelectionChanged -= HandleSelectionChanged;
        }

        private void HandleSelectionChanged(TowerBase tower, bool selected)
        {
            if (_handling) return;
            if (!selected || tower == null) return;

            _handling = true;
            // Deselect all other towers
            foreach (var t in TowerBase.All)
            {
                if (t == null || t == tower) continue;
                var sel = t.GetComponent<TowerSelectable>();
                if (sel != null && sel.Selected)
                {
                    sel.SetSelected(false);
                }
            }
            // Re-affirm selection to ensure the correct UI is visible after deselections
            var currentSel = tower.GetComponent<TowerSelectable>();
            if (currentSel != null && !currentSel.Selected)
            {
                currentSel.SetSelected(true);
            }
            _handling = false;
        }
    }
}
