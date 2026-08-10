using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using TR.Data;
using TR.Systems;

namespace TR.Battle
{
    
    public class CardDragPlacement : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler, IPointerDownHandler
    {
        [SerializeField] private CardDefinition card;

        public CardDefinition Card => card;
        [SerializeField] private TowerPlacementController placement;

        private GameObject _ghost;
        private bool _valid;
        private RangeRing _rangeRing;
        private float _cachedRange;
        private float _towerRadius;
        private bool _dragActive;

        
        private const float DragStreamInterval = 0.08f; 
        private float _nextDragStreamTime;
        private bool _dragStreamStarted;

        public void Init(CardDefinition def, TowerPlacementController placementController)
        {
            card = def;
            placement = placementController;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            
            eventData.useDragThreshold = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            
            _dragActive = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (card == null || placement == null)
            {
                Debug.LogWarning("[Drag] Missing card or placement controller on drag start.");
                return;
            }
            InputLocks.SetPlacementDragging(true);
            Debug.Log($"[Drag] Begin drag: {card.DisplayName}");
            _dragActive = true;
            if (_ghost == null)
            {
                CreateGhost();
                UpdateGhostPosition();
            }
            placement.SetSnapPointsVisible(true);
            placement.RefreshSnapPointColors(GetMouseWorld());
            BeginDragStream();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragActive)
            {
                
                if (card == null || placement == null) return;
                _dragActive = true;
                if (_ghost == null) { CreateGhost(); UpdateGhostPosition(); }
                placement.SetSnapPointsVisible(true);
            }
            if (_ghost == null) return;
            
            UpdateGhostPosition();
            placement.RefreshSnapPointColors(GetMouseWorld());
            StreamDragState(false);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragActive = false;
            if (_ghost != null)
            {
                Vector3 world = GetMouseWorld();
                if (_valid)
                {
                    bool ok = placement.TryPlaceAt(world, card, _towerRadius);
                    Debug.Log($"[Drag] Drop {(ok ? "placed" : "failed")} at {world}");
                }
                GameObject.Destroy(_ghost);
                _ghost = null;
                if (_rangeRing != null)
                {
                    GameObject.Destroy(_rangeRing.gameObject);
                    _rangeRing = null;
                }
            }
            else
            {
                Debug.Log("[Drag] End drag with no ghost (ignored)");
            }
            if (placement != null)
            {
                placement.SetSnapPointsVisible(false);
            }
            InputLocks.SetPlacementDragging(false);
            EndDragStream();
        }

        
        private void BeginDragStream()
        {
            if (!TR.Net.DuoRuntime.IsDuo) return;
            _dragStreamStarted = true;
            _nextDragStreamTime = 0f;
            StreamDragState(true);
        }

        private void StreamDragState(bool force)
        {
            if (!_dragStreamStarted || !TR.Net.DuoRuntime.IsDuo || card == null) return;
            if (!force && Time.unscaledTime < _nextDragStreamTime) return;
            _nextDragStreamTime = Time.unscaledTime + DragStreamInterval;
            var coord = TR.Net.DuoBattleCoordinator.Instance;
            if (coord == null) return;
            Vector3 world = GetMouseWorld();
            coord.SendDragState(true, card.CardId, GetCardLevel(), world.x, world.y);
        }

        private void EndDragStream()
        {
            if (!_dragStreamStarted) return;
            _dragStreamStarted = false;
            if (!TR.Net.DuoRuntime.IsDuo) return;
            var coord = TR.Net.DuoBattleCoordinator.Instance;
            if (coord != null) coord.SendDragState(false, card != null ? card.CardId : string.Empty, 0, 0f, 0f);
        }

        private int GetCardLevel()
        {
            if (card == null) return 1;
            try
            {
                var cp = PlayerProfile.GetOrCreateCard(card.CardId);
                return Mathf.Max(1, cp != null ? cp.level : 1);
            }
            catch { return 1; }
        }

        private void CreateGhost()
        {
            
            if (card != null && card.TowerPrefab != null)
            {
                _ghost = Instantiate(card.TowerPrefab);

                var ghostSg = _ghost.GetComponent<SortingGroup>();
                if (ghostSg == null) ghostSg = _ghost.AddComponent<SortingGroup>();
                ghostSg.sortingOrder = 10;

                foreach (var pooled in _ghost.GetComponentsInChildren<TR.VFX.PooledParticle>(true))
                {
                    if (pooled != null) pooled.ForceReturn();
                }

                foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;

                foreach (var pooled in _ghost.GetComponentsInChildren<TR.VFX.PooledParticle>(true))
                {
                    if (pooled != null) pooled.ForceReturn();
                }
                foreach (var col in _ghost.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                foreach (var col2d in _ghost.GetComponentsInChildren<Collider2D>(true)) col2d.enabled = false;
                foreach (var sr in _ghost.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var c = sr.color; c.a = 0.5f; sr.color = c;
                    sr.sortingOrder += 1;
                }
                _ghost.name = $"{card.DisplayName}_Ghost";
                Debug.Log("[Drag] Ghost created from TowerPrefab");
            }
            else
            {
                _ghost = new GameObject("TowerGhost2D");
                var sr = _ghost.AddComponent<SpriteRenderer>();
                sr.sprite = CreateSquareSprite();
                sr.color = new Color(1f, 1f, 1f, 0.5f);
                var ghostSg = _ghost.AddComponent<SortingGroup>();
                ghostSg.sortingOrder = 10;
                Debug.Log("[Drag] Ghost created as square placeholder (no TowerPrefab)");
            }
            var p = _ghost.transform.position; p.z = 0f; _ghost.transform.position = p;

            
            try
            {
                var cp = PlayerProfile.GetOrCreateCard(card.CardId);
                var stats = card.GetStatsForLevel(Mathf.Max(1, cp.level));
                
                int lv = Mathf.Max(1, cp.level);
                if (card is PulseCardDefinition pulse)
                {
                    _cachedRange = Mathf.Max(0f, pulse.GetPulseRadius(lv));
                }
                else if (card is BuffCardDefinition buff)
                {
                    _cachedRange = Mathf.Max(0f, buff.GetBuffRange(lv));
                }
                else
                {
                    _cachedRange = Mathf.Max(0f, stats.range);
                }
            }
            catch { _cachedRange = 0f; }

            if (_rangeRing == null)
            {
                var ringGO = new GameObject("RangeRing");
                ringGO.transform.SetParent(_ghost.transform, false);
                ringGO.transform.localPosition = Vector3.zero;
                _rangeRing = ringGO.AddComponent<RangeRing>();
                _rangeRing.Thickness = 0.05f;
                _rangeRing.Segments = 48;
            }
            _rangeRing.Radius = _cachedRange;
            _rangeRing.Color = new Color(0.2f, 1f, 0.2f, 0.6f);
            _rangeRing.gameObject.SetActive(true);

            _towerRadius = MeasureGhostRadius();
        }

        private float MeasureGhostRadius()
        {
            float radius = 0.4f;
            if (_ghost == null) return radius;

            var sr = _ghost.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null)
            {
                float srRadius = 0f;
                if (sr.sprite != null)
                {
                    Vector3 spriteExt = sr.sprite.bounds.extents;
                    srRadius = Mathf.Max(spriteExt.x, spriteExt.y) * Mathf.Max(sr.transform.lossyScale.x, sr.transform.lossyScale.y);
                }
                else
                {
                    Vector3 ext = sr.bounds.extents;
                    srRadius = Mathf.Max(ext.x, ext.y);
                }
                radius = Mathf.Max(radius, srRadius);
            }

            var col = _ghost.GetComponentInChildren<Collider2D>(true);
            if (col is CircleCollider2D cc)
            {
                radius = Mathf.Max(radius, cc.radius * Mathf.Max(col.transform.lossyScale.x, col.transform.lossyScale.y));
            }
            else if (col is BoxCollider2D bc)
            {
                float scale = Mathf.Max(col.transform.lossyScale.x, col.transform.lossyScale.y);
                radius = Mathf.Max(radius, Mathf.Max(bc.size.x, bc.size.y) * scale * 0.5f);
            }

            return Mathf.Max(0.25f, radius);
        }

        private void UpdateGhostPosition()
        {
            Vector3 world = GetMouseWorld();
            if (placement != null && placement.GetSnappedPosition(world, _towerRadius, out var snapped))
            {
                _valid = true;
                _ghost.transform.position = snapped;
                TintGhost(true);
            }
            else
            {
                _valid = false;
                _ghost.transform.position = new Vector3(world.x, world.y, 0f);
                TintGhost(false);
            }
        }

        private void TintGhost(bool valid)
        {
            Color colValid = new Color(0.4f, 1f, 0.4f, 0.6f);
            Color colInvalid = new Color(1f, 0.4f, 0.4f, 0.6f);
            var srs = _ghost.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs) sr.color = valid ? colValid : colInvalid;
            if (_rangeRing != null)
            {
                _rangeRing.Color = valid ? new Color(0.2f, 1f, 0.2f, 0.6f) : new Color(1f, 0.3f, 0.3f, 0.6f);
            }
        }

        private Vector3 GetMouseWorld()
        {
            if (Camera.main == null) return Vector3.zero;
            var w = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            w.z = 0f;
            return w;
        }

        private static Sprite CreateSquareSprite()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var cols = new Color[size * size];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
