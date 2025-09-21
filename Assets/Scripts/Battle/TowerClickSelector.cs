using UnityEngine;
using UnityEngine.EventSystems;

namespace TR.Battle
{
    // Global, reliable click selector for towers using Physics2D overlap, bypassing EventSystem raycast quirks.
    // Drop one in the battle scene (alongside BattleSceneController). It will coexist with TowerSelectable.
    public class TowerClickSelector : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("Layers considered as towers for selection (set to your Towers layer). If zero, all layers are used.")]
        [SerializeField] private LayerMask towersLayerMask;
        [Tooltip("Max world distance from click point to accept a tower (safety). 0 = no limit.")]
        [SerializeField] private float maxPickDistance = 0f;
        [Tooltip("If true, clicks over UI are ignored. Turn OFF if a UI overlay sits on top of the game view.")]
        [SerializeField] private bool respectUIBlocks = false;
        [Tooltip("If no exact point hit is found, use this radius to search nearby (world units). 0 disables fallback.")]
        [SerializeField] private float pickRadius = 0.15f;

        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                Debug.LogWarning("[TowerClickSelector] No Camera.main found; selection disabled.");
            }
        }

        private void Update()
        {
            if (_cam == null) return;
            if (Input.GetMouseButtonUp(0))
            {
                // Optionally ignore clicks when pointer is over UI
                if (respectUIBlocks && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

                Vector3 wp = _cam.ScreenToWorldPoint(Input.mousePosition);
                Vector2 p = new Vector2(wp.x, wp.y);

                // Query all colliders at the point
                int mask = towersLayerMask.value == 0 ? Physics2D.AllLayers : towersLayerMask.value;
                Collider2D[] hits = Physics2D.OverlapPointAll(p, mask);
                // Fallback: small overlap circle in case the click point barely misses the collider
                if ((hits == null || hits.Length == 0) && pickRadius > 0f)
                {
                    hits = Physics2D.OverlapCircleAll(p, pickRadius, mask);
                }
                if (hits == null || hits.Length == 0)
                {
                    // If click is over UI, do not change tower selection
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    {
                        return;
                    }
                    // Clicked empty world space: clear any current selection
                    foreach (var t in TowerBase.All)
                    {
                        if (t == null) continue;
                        var selComp = t.GetComponent<TowerSelectable>();
                        if (selComp != null && selComp.Selected)
                        {
                            selComp.SetSelected(false);
                        }
                    }
                    return;
                }

                // Choose nearest TowerSelectable to the cursor
                TowerSelectable bestSel = null;
                float bestDist = float.MaxValue;
                for (int i = 0; i < hits.Length; i++)
                {
                    var h = hits[i];
                    if (h == null || !h.gameObject.activeInHierarchy) continue;
                    var sel = h.GetComponentInParent<TowerSelectable>();
                    if (sel == null || !sel.isActiveAndEnabled) continue;
                    Vector3 pos = h.transform.position;
                    float d = Vector2.Distance(p, new Vector2(pos.x, pos.y));
                    if (maxPickDistance > 0f && d > maxPickDistance) continue;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestSel = sel;
                    }
                }
                if (bestSel != null)
                {
                    // Toggle: if already selected, deselect; otherwise select
                    if (bestSel.Selected)
                        bestSel.SetSelected(false);
                    else
                        bestSel.SetSelected(true);
                }
            }
        }
    }
}
