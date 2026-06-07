using UnityEngine;

namespace TR.Battle
{
    
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
            
            foreach (var t in TowerBase.All)
            {
                if (t == null || t == tower) continue;
                var sel = t.GetComponent<TowerSelectable>();
                if (sel != null && sel.Selected)
                {
                    sel.SetSelected(false);
                }
            }
            
            var currentSel = tower.GetComponent<TowerSelectable>();
            if (currentSel != null && !currentSel.Selected)
            {
                currentSel.SetSelected(true);
            }
            _handling = false;
        }
    }
}
