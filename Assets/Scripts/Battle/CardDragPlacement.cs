using UnityEngine;
using UnityEngine.EventSystems;
using TR.Data;
using TR.Systems;

namespace TR.Battle
{
    
    public class CardDragPlacement : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler, IPointerDownHandler
    {
        [SerializeField] private CardDefinition card;
        [SerializeField] private TowerPlacementController placement;

        private GameObject _ghost;
        private bool _valid;
        private RangeRing _rangeRing;
        private float _cachedRange;
        private bool _dragActive;

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
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragActive = false;
            if (_ghost != null)
            {
                Vector3 world = GetMouseWorld();
                if (_valid)
                {
                    bool ok = placement.TryPlaceAt(world, card);
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
        }

        private void CreateGhost()
        {
            
            if (card != null && card.TowerPrefab != null)
            {
                _ghost = Instantiate(card.TowerPrefab);
                
                foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
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
        }

        private void UpdateGhostPosition()
        {
            Vector3 world = GetMouseWorld();
            if (placement != null && placement.GetSnappedPosition(world, out var snapped))
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
