using UnityEngine;
using UnityEngine.EventSystems;
using TR.Data;
using TR.Systems;

namespace TR.Battle
{
    // Handles selecting a card from the deck bar and placing a tower by snapping to predefined snap points.
    public class TowerPlacementController : MonoBehaviour
    {
        [Header("Snap Points Only")]
        [SerializeField] private Transform snapPointsRoot;         // parent containing empty children placed as invisible circles
        [SerializeField] private float snapMaxDistance = 1000f;    // max distance to consider a point (in world units)

        private MatchEconomy _economy;
        private CardDefinition _selectedCard;
        private readonly System.Collections.Generic.HashSet<Transform> _occupied = new();
        private bool _snapVisible = false;

        public void Configure(MatchEconomy economy)
        {
            _economy = economy;
            _occupied.Clear();
        }

        // Note: click-to-place path removed. Use TryPlaceAt/GetSnappedPosition via drag system.

        private GameObject PlaceTower(CardDefinition def, int level, Vector3 position)
        {
            var go = TowerFactory.CreateTower(def, level, position, Quaternion.identity);
            go.name = $"Tower_{def.DisplayName}_L{level}";
            return go;
        }

        private Transform FindNearestFreeSnapPoint(Vector3 fromWorld)
        {
            if (snapPointsRoot == null) return null;
            Transform best = null;
            float bestDist = snapMaxDistance <= 0f ? float.MaxValue : snapMaxDistance;
            foreach (Transform child in snapPointsRoot)
            {
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                if (_occupied.Contains(child)) continue;
                float d = Vector2.Distance(new Vector2(fromWorld.x, fromWorld.y), new Vector2(child.position.x, child.position.y));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = child;
                }
            }
            return best;
        }

        // Drag-and-drop API: preview snapped position (true if a free point is found)
        public bool GetSnappedPosition(Vector3 worldPos, out Vector3 snappedPos)
        {
            snappedPos = default;
            var snap = FindNearestFreeSnapPoint(worldPos);
            if (snap == null) return false;
            snappedPos = new Vector3(snap.position.x, snap.position.y, 0f);
            return true;
        }

        // Drag-and-drop API: attempt to place a specific card at the given world position (snaps internally)
        public bool TryPlaceAt(Vector3 worldPos, CardDefinition card)
        {
            if (Camera.main == null || snapPointsRoot == null || card == null) return false;
            var snap = FindNearestFreeSnapPoint(worldPos);
            if (snap == null) return false;
            int level = 1;
            var cp = PlayerProfile.GetOrCreateCard(card.CardId);
            level = Mathf.Max(1, cp.level);
            int cost = card.GetStatsForLevel(level).cost;
            // Effect caps gating
            if (TR.Systems.EffectLimitService.IsEnabled)
            {
                if (!TR.Systems.EffectLimitService.CanPlace(card, level, out var blockType, out var cap, out var current))
                {
                    Debug.LogWarning($"[Placement] Limit reached for {blockType}: {current}/{cap}. Cannot place {card.DisplayName}.");
                    // Toast feedback
                    TR.UI.BattleToast.Show($"Limit reached: {blockType} ({current}/{cap})");
                    return false;
                }
            }
            // Per-card caps gating
            if (TR.Systems.EffectLimitService.CardCapsEnabled)
            {
                if (!TR.Systems.EffectLimitService.CanPlaceCard(card, out var capCard, out var curCard))
                {
                    Debug.LogWarning($"[Placement] Card limit reached for {card.DisplayName}: {curCard}/{capCard}.");
                    TR.UI.BattleToast.Show($"Limit reached: {card.DisplayName} ({curCard}/{capCard})");
                    return false;
                }
            }
            if (_economy != null && !_economy.CanAfford(cost))
            {
                Debug.Log($"[Placement] Not enough money. Need {cost}, have {_economy.Current}.");
                // Visual feedback: pulse the economy money UI red
                var moneyUI = FindFirstObjectByType<TR.Battle.BattleEconomyUI>(FindObjectsInactive.Include);
                if (moneyUI != null) moneyUI.PulseInsufficient();
                return false;
            }
            if (_economy != null) _economy.Spend(cost);
            var pos = new Vector3(snap.position.x, snap.position.y, 0f);
            var towerGO = PlaceTower(card, level, pos);
            // Register effects now that placement succeeded
            if (towerGO != null && TR.Systems.EffectLimitService.IsEnabled)
            {
                TR.Systems.EffectLimitService.Register(card, level);
                var eff = towerGO.GetComponent<EffectLimitBinding>();
                if (eff == null) eff = towerGO.AddComponent<EffectLimitBinding>();
                var types = TR.Systems.EffectLimitService.GetEffectTypesForCard(card, level);
                eff.SetTypes(types);
            }
            // Register per-card now that placement succeeded
            if (towerGO != null && TR.Systems.EffectLimitService.CardCapsEnabled)
            {
                TR.Systems.EffectLimitService.RegisterCard(card);
                // Also attach a small binder to unregister per-card on destroy
                var binder = towerGO.GetComponent<CardLimitBinding>();
                if (binder == null) binder = towerGO.AddComponent<CardLimitBinding>();
                binder.SetCardId(card.CardId);
            }
            _occupied.Add(snap);
            // Attach a binding so when the tower is destroyed, the snap frees up
            if (towerGO != null)
            {
                var bind = towerGO.GetComponent<TowerSnapBinding>();
                if (bind == null) bind = towerGO.AddComponent<TowerSnapBinding>();
                bind.Bind(snap, this);
            }
            // Update visuals after occupying
            RefreshSnapPointColors(worldPos);
            Debug.Log($"[Placement] Placed {card.DisplayName} L{level} at {pos} for cost {cost}.");
            return true;
        }

        // ===== Snap Point Visualization =====
        private static readonly Color _colorFree = new Color(0.3f, 1f, 0.3f, 0.35f);
        private static readonly Color _colorTaken = new Color(1f, 0.3f, 0.3f, 0.35f);
        private static readonly Color _colorHighlight = new Color(0.3f, 1f, 0.3f, 0.65f);

        public void SetSnapPointsVisible(bool visible)
        {
            _snapVisible = visible;
            if (snapPointsRoot == null) return;
            foreach (Transform child in snapPointsRoot)
            {
                if (child == null) continue;
                var sr = child.GetComponentInChildren<SpriteRenderer>(true);
                if (sr == null) continue; // designer may omit sprite; that's fine
                var baseCol = _occupied.Contains(child) ? _colorTaken : _colorFree;
                baseCol.a = visible ? baseCol.a : 0f;
                sr.color = baseCol;
                sr.enabled = visible;
            }
        }

        public void RefreshSnapPointColors(Vector3 worldPos)
        {
            if (snapPointsRoot == null) return;
            if (!_snapVisible)
            {
                // If hidden, do not modify sprite states to avoid flashing them on
                return;
            }
            // Find nearest free (within range)
            var nearest = FindNearestFreeSnapPoint(worldPos);
            foreach (Transform child in snapPointsRoot)
            {
                if (child == null) continue;
                var sr = child.GetComponentInChildren<SpriteRenderer>(true);
                if (sr == null) continue;
                bool taken = _occupied.Contains(child);
                Color c = taken ? _colorTaken : _colorFree;
                // If this is the nearest free point, emphasize
                if (!taken && nearest == child)
                {
                    c = _colorHighlight;
                }
                sr.color = c;
                sr.enabled = true;
            }
        }

        // Called by TowerSnapBinding when its tower is destroyed
        public void FreeSnap(Transform snap)
        {
            if (snap == null) return;
            if (_occupied.Remove(snap))
            {
                // Refresh colors; use snap.position for highlight computation fallback
                RefreshSnapPointColors(snap.position);
                Debug.Log($"[Placement] Freed snap point {snap.name}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (snapPointsRoot == null) return;
            Gizmos.color = Color.green;
            foreach (Transform child in snapPointsRoot)
            {
                if (child == null) continue;
                Gizmos.color = _occupied != null && _occupied.Contains(child) ? Color.red : Color.green;
                Gizmos.DrawWireSphere(child.position, 0.2f);
            }
            // Draw selection radius at mouse position (editor only)
            if (Camera.main != null)
            {
                var world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                world.z = 0f;
                Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
                Gizmos.DrawWireSphere(world, snapMaxDistance);
            }
        }
    }
}
