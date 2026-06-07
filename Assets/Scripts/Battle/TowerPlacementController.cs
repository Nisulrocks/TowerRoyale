using UnityEngine;
using UnityEngine.EventSystems;
using TR.Data;
using TR.Systems;

namespace TR.Battle
{
    
    public class TowerPlacementController : MonoBehaviour
    {
        [Header("Snap Points Only")]
        [SerializeField] private Transform snapPointsRoot;         
        [SerializeField] private float snapMaxDistance = 1000f;    

        private MatchEconomy _economy;
        private CardDefinition _selectedCard;
        private readonly System.Collections.Generic.HashSet<Transform> _occupied = new();
        private bool _snapVisible = false;

        public void Configure(MatchEconomy economy)
        {
            _economy = economy;
            _occupied.Clear();
        }

        

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

        
        public bool GetSnappedPosition(Vector3 worldPos, out Vector3 snappedPos)
        {
            snappedPos = default;
            var snap = FindNearestFreeSnapPoint(worldPos);
            if (snap == null) return false;
            snappedPos = new Vector3(snap.position.x, snap.position.y, 0f);
            return true;
        }

        
        public bool TryPlaceAt(Vector3 worldPos, CardDefinition card)
        {
            if (Camera.main == null || snapPointsRoot == null || card == null) return false;
            var snap = FindNearestFreeSnapPoint(worldPos);
            if (snap == null) return false;
            int level = 1;
            var cp = PlayerProfile.GetOrCreateCard(card.CardId);
            level = Mathf.Max(1, cp.level);
            int cost = card.GetStatsForLevel(level).cost;
            
            if (TR.Systems.EffectLimitService.IsEnabled)
            {
                if (!TR.Systems.EffectLimitService.CanPlace(card, level, out var blockType, out var cap, out var current))
                {
                    Debug.LogWarning($"[Placement] Limit reached for {blockType}: {current}/{cap}. Cannot place {card.DisplayName}.");
                    
                    TR.UI.BattleToast.Show($"Limit reached: {blockType} ({current}/{cap})");
                    return false;
                }
            }
            
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
                
                var moneyUI = FindFirstObjectByType<TR.Battle.BattleEconomyUI>(FindObjectsInactive.Include);
                if (moneyUI != null) moneyUI.PulseInsufficient();
                return false;
            }
            if (_economy != null) _economy.Spend(cost);
            var pos = new Vector3(snap.position.x, snap.position.y, 0f);
            var towerGO = PlaceTower(card, level, pos);
            
            if (towerGO != null && TR.Systems.EffectLimitService.IsEnabled)
            {
                TR.Systems.EffectLimitService.Register(card, level);
                var eff = towerGO.GetComponent<EffectLimitBinding>();
                if (eff == null) eff = towerGO.AddComponent<EffectLimitBinding>();
                var types = TR.Systems.EffectLimitService.GetEffectTypesForCard(card, level);
                eff.SetTypes(types);
            }
            
            if (towerGO != null && TR.Systems.EffectLimitService.CardCapsEnabled)
            {
                TR.Systems.EffectLimitService.RegisterCard(card);
                
                var binder = towerGO.GetComponent<CardLimitBinding>();
                if (binder == null) binder = towerGO.AddComponent<CardLimitBinding>();
                binder.SetCardId(card.CardId);
            }
            _occupied.Add(snap);
            
            if (towerGO != null)
            {
                var bind = towerGO.GetComponent<TowerSnapBinding>();
                if (bind == null) bind = towerGO.AddComponent<TowerSnapBinding>();
                bind.Bind(snap, this);
            }
            
            RefreshSnapPointColors(worldPos);
            Debug.Log($"[Placement] Placed {card.DisplayName} L{level} at {pos} for cost {cost}.");
            return true;
        }

        
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
                if (sr == null) continue; 
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
                
                return;
            }
            
            var nearest = FindNearestFreeSnapPoint(worldPos);
            foreach (Transform child in snapPointsRoot)
            {
                if (child == null) continue;
                var sr = child.GetComponentInChildren<SpriteRenderer>(true);
                if (sr == null) continue;
                bool taken = _occupied.Contains(child);
                Color c = taken ? _colorTaken : _colorFree;
                
                if (!taken && nearest == child)
                {
                    c = _colorHighlight;
                }
                sr.color = c;
                sr.enabled = true;
            }
        }

        
        public void FreeSnap(Transform snap)
        {
            if (snap == null) return;
            if (_occupied.Remove(snap))
            {
                
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
