using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using TR.Data;
using TR.Systems;
using Photon.Pun;

namespace TR.Battle
{
    public class TowerPlacementController : MonoBehaviour
    {
        [Header("Placement Areas")]
        [UnityEngine.Serialization.FormerlySerializedAs("snapPointsRoot")]
        [SerializeField] private Transform placementAreasRoot;

        [SerializeField] private float minTowerSpacing = 0.05f;
        [SerializeField] private Color areaBaseColor = new Color(0.3f, 1f, 0.3f, 0.35f);
        [SerializeField] private float areaMergeMargin = 0.1f;

        private MatchEconomy _economy;
        private bool _areasVisible;
        private int _nextLocalPlacementId = 1;

        private readonly List<Area> _areas = new();
        private readonly Dictionary<int, GameObject> _towersByPlacementId = new();
        private readonly Dictionary<CardDefinition, float> _cardRadiusCache = new();
        private TR.Net.DuoBattleCoordinator _coordinator;

        private readonly List<SpriteRenderer> _combinedVisuals = new();

        [SerializeField] private int maxAreaTextureSize = 512;

        private struct Area
        {
            public Transform Transform;
            public BoxCollider2D Collider;
        }

        public void Configure(MatchEconomy economy)
        {
            _economy = economy;
            BuildAreaList();

            if (TR.Net.DuoRuntime.IsDuo)
            {
                _coordinator = TR.Net.DuoBattleCoordinator.Instance;
                if (_coordinator != null)
                {
                    _coordinator.OnTowerPlacedReceived -= OnRemoteTowerPlaced;
                    _coordinator.OnTowerPlacedReceived += OnRemoteTowerPlaced;
                    _coordinator.OnTowerRemovedReceived -= OnRemoteTowerRemoved;
                    _coordinator.OnTowerRemovedReceived += OnRemoteTowerRemoved;
                    _coordinator.OnTowerHpReceived -= OnRemoteTowerHp;
                    _coordinator.OnTowerHpReceived += OnRemoteTowerHp;
                    _coordinator.OnTowerSyncRequested -= OnTowerSyncRequested;
                    _coordinator.OnTowerSyncRequested += OnTowerSyncRequested;
                    _coordinator.OnTowerSyncReceived -= OnTowerSyncReceived;
                    _coordinator.OnTowerSyncReceived += OnTowerSyncReceived;
                }
            }
        }

        private void OnDestroy()
        {
            if (_coordinator != null)
            {
                _coordinator.OnTowerPlacedReceived -= OnRemoteTowerPlaced;
                _coordinator.OnTowerRemovedReceived -= OnRemoteTowerRemoved;
                _coordinator.OnTowerHpReceived -= OnRemoteTowerHp;
                _coordinator.OnTowerSyncRequested -= OnTowerSyncRequested;
                _coordinator.OnTowerSyncReceived -= OnTowerSyncReceived;
            }

            foreach (var visual in _combinedVisuals)
            {
                if (visual != null)
                    Destroy(visual.gameObject);
            }
            _combinedVisuals.Clear();
        }

        private void BuildAreaList()
        {
            _areas.Clear();
            if (placementAreasRoot == null)
            {
                Debug.LogWarning("[Placement] placementAreasRoot is not assigned.");
                return;
            }

            _combinedVisuals.Clear();

            int found = 0;
            int skipped = 0;
            foreach (Transform child in placementAreasRoot)
            {
                if (child == null) continue;

                if ((child.name == "PlacementAreaVisual" || child.name == "PlacementAreasVisual") && child.GetComponent<BoxCollider2D>() == null)
                {
                    Destroy(child.gameObject);
                    continue;
                }

                var col = child.GetComponent<BoxCollider2D>();
                if (col == null)
                {
                    skipped++;
                    continue;
                }

                _areas.Add(new Area { Transform = child, Collider = col });
                found++;
            }

            var clusters = BuildClusters();

            bool[] inCombinedCluster = new bool[_areas.Count];
            foreach (var cluster in clusters)
            {
                if (cluster.Count > 1)
                {
                    foreach (int idx in cluster)
                        inCombinedCluster[idx] = true;
                }
            }

            for (int i = 0; i < _areas.Count; i++)
            {
                if (!inCombinedCluster[i] || _areas[i].Transform == null) continue;
                Transform oldVisual = _areas[i].Transform.Find("PlacementAreaVisual");
                if (oldVisual != null)
                    Destroy(oldVisual.gameObject);
            }

            foreach (var cluster in clusters)
            {
                if (cluster.Count == 1)
                {
                    int idx = cluster[0];
                    if (_areas[idx].Transform == null) continue;
                    var visual = EnsureAreaVisual(_areas[idx].Transform, _areas[idx].Collider);
                    if (visual != null)
                    {
                        visual.gameObject.SetActive(false);
                        visual.color = areaBaseColor;
                    }
                }
                else
                {
                    BuildCombinedAreaVisual(cluster);
                }
            }

            Debug.Log($"[Placement] Built {found} area(s) from {placementAreasRoot.name}, skipped {skipped} children without BoxCollider2D.");
        }

        private void BuildCombinedAreaVisual(List<int> cluster)
        {
            BoxCollider2D[] cols = new BoxCollider2D[cluster.Count];
            Bounds[] originalBounds = new Bounds[cluster.Count];
            Bounds total = new Bounds();
            bool totalSet = false;

            for (int k = 0; k < cluster.Count; k++)
            {
                cols[k] = _areas[cluster[k]].Collider;
                if (cols[k] == null) continue;

                originalBounds[k] = cols[k].bounds;
                Bounds expanded = GetExpandedBounds(cols[k], areaMergeMargin);

                if (!totalSet)
                {
                    total = expanded;
                    totalSet = true;
                }
                else
                {
                    total.Encapsulate(expanded);
                }
            }

            const float padding = 0.1f;
            Vector2 min = new Vector2(total.min.x - padding, total.min.y - padding);
            Vector2 max = new Vector2(total.max.x + padding, total.max.y + padding);
            Vector2 size = max - min;

            float maxDim = Mathf.Max(size.x, size.y);
            float ppu = maxDim > 0.001f ? maxAreaTextureSize / maxDim : 32f;
            int texWidth = Mathf.Max(1, Mathf.CeilToInt(size.x * ppu));
            int texHeight = Mathf.Max(1, Mathf.CeilToInt(size.y * ppu));

            Texture2D texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[texWidth * texHeight];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            for (int y = 0; y < texHeight; y++)
            {
                int row = y * texWidth;
                float worldY = min.y + y / ppu;
                for (int x = 0; x < texWidth; x++)
                {
                    Vector2 worldPos = new Vector2(min.x + x / ppu, worldY);
                    for (int k = 0; k < cluster.Count; k++)
                    {
                        if (cols[k] == null) continue;
                        if (IsPointInsideBox(worldPos, cols[k], originalBounds[k]))
                        {
                            pixels[row + x] = areaBaseColor;
                            break;
                        }
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texWidth, texHeight), new Vector2(0f, 0f), ppu);

            GameObject go = new GameObject("PlacementAreasVisual");
            go.transform.SetParent(placementAreasRoot, false);
            go.transform.position = new Vector3(min.x, min.y, 0f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = 1;
            sr.gameObject.SetActive(false);

            _combinedVisuals.Add(sr);
        }

        private List<List<int>> BuildClusters()
        {
            int n = _areas.Count;
            bool[] visited = new bool[n];
            List<List<int>> clusters = new();

            for (int i = 0; i < n; i++)
            {
                if (visited[i]) continue;

                List<int> cluster = new();
                Queue<int> queue = new();
                visited[i] = true;
                queue.Enqueue(i);

                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    cluster.Add(cur);

                    for (int j = 0; j < n; j++)
                    {
                        if (visited[j]) continue;
                        if (AreasIntersect(cur, j))
                        {
                            visited[j] = true;
                            queue.Enqueue(j);
                        }
                    }
                }

                clusters.Add(cluster);
            }

            return clusters;
        }

        private bool AreasIntersect(int a, int b)
        {
            return BoxIntersects(_areas[a].Collider, _areas[b].Collider, areaMergeMargin);
        }

        private static bool BoxIntersects(BoxCollider2D a, BoxCollider2D b, float margin)
        {
            Vector2 ca, hax, hay;
            Vector2 cb, hbx, hby;
            GetBoxHalfVectors(a, margin, out ca, out hax, out hay);
            GetBoxHalfVectors(b, margin, out cb, out hbx, out hby);

            Vector2[] axes = new Vector2[]
            {
                new Vector2(-hax.y, hax.x),
                new Vector2(-hay.y, hay.x),
                new Vector2(-hbx.y, hbx.x),
                new Vector2(-hby.y, hby.x)
            };

            Vector2[] va = GetBoxVertices(ca, hax, hay);
            Vector2[] vb = GetBoxVertices(cb, hbx, hby);

            foreach (var axis in axes)
            {
                if (axis.sqrMagnitude < 0.000001f) continue;
                if (Separated(va, vb, axis))
                    return false;
            }
            return true;
        }

        private static void GetBoxHalfVectors(BoxCollider2D box, float margin, out Vector2 center, out Vector2 hx, out Vector2 hy)
        {
            center = box.transform.TransformPoint(new Vector3(box.offset.x, box.offset.y, 0f));

            float scaleX = Mathf.Max(0.0001f, Mathf.Abs(box.transform.lossyScale.x));
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(box.transform.lossyScale.y));

            float halfX = box.size.x * 0.5f + margin / scaleX;
            float halfY = box.size.y * 0.5f + margin / scaleY;

            hx = box.transform.TransformVector(new Vector3(halfX, 0f, 0f));
            hy = box.transform.TransformVector(new Vector3(0f, halfY, 0f));
        }

        private static Bounds GetExpandedBounds(BoxCollider2D box, float margin)
        {
            Vector2 c, hx, hy;
            GetBoxHalfVectors(box, margin, out c, out hx, out hy);

            Bounds b = new Bounds(new Vector3(c.x, c.y, 0f), Vector3.zero);
            b.Encapsulate(new Vector3(c.x + hx.x + hy.x, c.y + hx.y + hy.y, 0f));
            b.Encapsulate(new Vector3(c.x - hx.x + hy.x, c.y - hx.y + hy.y, 0f));
            b.Encapsulate(new Vector3(c.x + hx.x - hy.x, c.y + hx.y - hy.y, 0f));
            b.Encapsulate(new Vector3(c.x - hx.x - hy.x, c.y - hx.y - hy.y, 0f));
            return b;
        }

        private static Vector2[] GetBoxVertices(Vector2 c, Vector2 hx, Vector2 hy)
        {
            return new Vector2[]
            {
                c - hx - hy,
                c + hx - hy,
                c + hx + hy,
                c - hx + hy
            };
        }

        private static bool Separated(Vector2[] a, Vector2[] b, Vector2 axis)
        {
            float minA = float.MaxValue, maxA = float.MinValue;
            float minB = float.MaxValue, maxB = float.MinValue;

            foreach (var v in a)
            {
                float p = Vector2.Dot(v, axis);
                if (p < minA) minA = p;
                if (p > maxA) maxA = p;
            }
            foreach (var v in b)
            {
                float p = Vector2.Dot(v, axis);
                if (p < minB) minB = p;
                if (p > maxB) maxB = p;
            }

            return maxA < minB || maxB < minA;
        }

        private bool IsPointInsideBox(Vector2 worldPoint, BoxCollider2D box)
        {
            return IsPointInsideBox(worldPoint, box, box.bounds);
        }

        private bool IsPointInsideBox(Vector2 worldPoint, BoxCollider2D box, Bounds bounds)
        {
            Bounds expanded = bounds;
            expanded.Expand(areaMergeMargin * 1.5f);
            if (!expanded.Contains(new Vector3(worldPoint.x, worldPoint.y, expanded.center.z)))
                return false;

            Vector3 local = box.transform.InverseTransformPoint(new Vector3(worldPoint.x, worldPoint.y, 0f));
            Vector2 localRel = new Vector2(local.x - box.offset.x, local.y - box.offset.y);

            float scaleX = Mathf.Max(0.0001f, Mathf.Abs(box.transform.lossyScale.x));
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(box.transform.lossyScale.y));
            float halfX = box.size.x * 0.5f + areaMergeMargin / scaleX;
            float halfY = box.size.y * 0.5f + areaMergeMargin / scaleY;

            return Mathf.Abs(localRel.x) <= halfX + 0.0001f
                && Mathf.Abs(localRel.y) <= halfY + 0.0001f;
        }

        private SpriteRenderer EnsureAreaVisual(Transform area, BoxCollider2D box)
        {
            const string visualName = "PlacementAreaVisual";
            Transform visualChild = area.Find(visualName);
            if (visualChild == null)
            {
                var go = new GameObject(visualName);
                go.transform.SetParent(area, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CreateWhiteSprite();
                sr.color = areaBaseColor;
                sr.sortingOrder = 1;
                visualChild = go.transform;
            }

            var renderer = visualChild.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.transform.localPosition = new Vector3(box.offset.x, box.offset.y, 0f);

                float scaleX = Mathf.Max(0.0001f, Mathf.Abs(box.transform.lossyScale.x));
                float scaleY = Mathf.Max(0.0001f, Mathf.Abs(box.transform.lossyScale.y));
                float visualSizeX = box.size.x + 2f * areaMergeMargin / scaleX;
                float visualSizeY = box.size.y + 2f * areaMergeMargin / scaleY;

                renderer.transform.localScale = new Vector3(visualSizeX, visualSizeY, 1f);
                renderer.sortingOrder = 1;
            }
            return renderer;
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private int _placementIdBase = -1;

        private int GetPlacementIdBase()
        {
            if (_placementIdBase < 0)
            {
                int actor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 1;
                long baseId = (long)actor * 1000000L;
                _placementIdBase = (int)System.Math.Min(baseId, (long)int.MaxValue - 100000L);
            }
            return _placementIdBase;
        }

        // A fresh controller restarts the counter at 1, so after a rejoin it would hand out ids
        // that are still live on the partner. Push it past every id we already own.
        private void ReserveLocalPlacementId(int placementId)
        {
            if (placementId < 0) return;
            int offset = placementId - GetPlacementIdBase();
            if (offset < 0 || offset >= 1000000) return;
            if (offset >= _nextLocalPlacementId) _nextLocalPlacementId = offset + 1;
        }

        private int GeneratePlacementId()
        {
            int id;
            do
            {
                id = GetPlacementIdBase() + _nextLocalPlacementId++;
            } while (_towersByPlacementId.ContainsKey(id));
            return id;
        }

        public void SetSnapPointsVisible(bool visible)
        {
            _areasVisible = visible;
            if (visible)
                Debug.Log($"[Placement] Showing {_areas.Count} placement area overlay(s).");

            foreach (var visual in _combinedVisuals)
            {
                if (visual != null)
                    visual.gameObject.SetActive(visible);
            }

            foreach (var area in _areas)
            {
                if (area.Transform == null) continue;
                foreach (Transform child in area.Transform)
                {
                    if (child != null && child.name == "PlacementAreaVisual")
                        child.gameObject.SetActive(visible);
                }
            }
        }

        public void RefreshSnapPointColors(Vector3 worldPos)
        {
            // Highlighting disabled: base color is used for all areas.
        }

        private int GetAreaIndex(Vector3 pos)
        {
            Vector2 p = new Vector2(pos.x, pos.y);
            for (int i = 0; i < _areas.Count; i++)
            {
                var col = _areas[i].Collider;
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                if (IsPointInsideBox(p, col)) return i;
            }
            return -1;
        }

        public bool IsInsideAnyArea(Vector3 pos)
        {
            return GetAreaIndex(pos) >= 0;
        }

        // A world point inside a placement area that currently has room for a tower. Used by the
        // tutorial to demonstrate where a card should be dragged.
        public bool TryGetSuggestedPlacementPoint(out Vector3 worldPos, float radius = 0.4f)
        {
            worldPos = Vector3.zero;
            for (int i = 0; i < _areas.Count; i++)
            {
                var col = _areas[i].Collider;
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

                Vector3 center = col.bounds.center;
                center.z = 0f;
                if (IsPositionFree(center, radius))
                {
                    worldPos = center;
                    return true;
                }

                // Centre is taken; probe a few offsets before giving up on this area.
                Vector3 extents = col.bounds.extents;
                for (int step = 1; step <= 3; step++)
                {
                    float t = step / 4f;
                    Vector3[] probes =
                    {
                        center + new Vector3(extents.x * t, 0f, 0f),
                        center - new Vector3(extents.x * t, 0f, 0f),
                        center + new Vector3(0f, extents.y * t, 0f),
                        center - new Vector3(0f, extents.y * t, 0f),
                    };
                    for (int p = 0; p < probes.Length; p++)
                    {
                        var probe = probes[p];
                        probe.z = 0f;
                        if (IsInsideAnyArea(probe) && IsPositionFree(probe, radius))
                        {
                            worldPos = probe;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool IsPositionFree(Vector3 pos, float radius)
        {
            float minDist = Mathf.Max(0f, radius + minTowerSpacing);
            foreach (var tower in TowerBase.All)
            {
                if (tower == null) continue;
                if (Vector2.Distance(new Vector2(pos.x, pos.y), new Vector2(tower.transform.position.x, tower.transform.position.y)) < minDist + tower.PlacementRadius)
                    return false;
            }
            return true;
        }

        public bool IsValidPlacement(Vector3 pos, float radius, out Vector3 validPos)
        {
            validPos = new Vector3(pos.x, pos.y, 0f);
            return IsInsideAnyArea(validPos) && IsPositionFree(validPos, radius);
        }

        public bool GetSnappedPosition(Vector3 worldPos, out Vector3 snappedPos)
        {
            return IsValidPlacement(worldPos, 0f, out snappedPos);
        }

        public bool GetSnappedPosition(Vector3 worldPos, float towerRadius, out Vector3 snappedPos)
        {
            return IsValidPlacement(worldPos, towerRadius, out snappedPos);
        }

        public bool TryPlaceAt(Vector3 worldPos, CardDefinition card)
        {
            return TryPlaceAt(worldPos, card, 0f);
        }

        public bool TryPlaceAt(Vector3 worldPos, CardDefinition card, float towerRadius)
        {
            if (Camera.main == null || placementAreasRoot == null || card == null) return false;
            if (towerRadius <= 0f) towerRadius = GetCardRadius(card);
            if (!IsValidPlacement(worldPos, towerRadius, out Vector3 validPos)) return false;

            int level = 1;
            var cp = PlayerProfile.GetOrCreateCard(card.CardId);
            level = Mathf.Max(1, cp.level);
            int cost = card.GetStatsForLevel(level).cost;

            if (TR.Systems.EffectLimitService.IsEnabled)
            {
                if (!TR.Systems.EffectLimitService.CanPlace(card, level, out var blockType, out var cap, out var current))
                {
                    TR.UI.BattleToast.Show($"Limit reached: {blockType} ({current}/{cap})");
                    return false;
                }
            }

            if (TR.Systems.EffectLimitService.CardCapsEnabled)
            {
                if (!TR.Systems.EffectLimitService.CanPlaceCard(card, out var capCard, out var curCard))
                {
                    TR.UI.BattleToast.Show($"Limit reached: {card.DisplayName} ({curCard}/{capCard})");
                    return false;
                }
            }

            if (TR.Systems.EffectLimitService.RarityCapsEnabled)
            {
                if (!TR.Systems.EffectLimitService.CanPlaceRarity(card, out var capRarity, out var curRarity, out var rarityName))
                {
                    // A cap of 0 is a ban, not a "you've used them all" situation.
                    TR.UI.BattleToast.Show(capRarity <= 0
                        ? $"{rarityName} towers can't be used in this arena"
                        : $"Limit reached: {rarityName} ({curRarity}/{capRarity})");
                    return false;
                }
            }

            if (_economy != null && !_economy.CanAfford(cost))
            {
                var moneyUI = FindFirstObjectByType<TR.Battle.BattleEconomyUI>(FindObjectsInactive.Include);
                moneyUI?.PulseInsufficient();
                return false;
            }

            if (_economy != null) _economy.Spend(cost);

            int placementId = GeneratePlacementId();
            int owner = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;

            var towerGO = PlaceTower(card, level, validPos, owner, placementId, towerRadius);
            if (towerGO == null) return false;

            RegisterEffectLimits(towerGO, card, level);

            if (TR.Net.DuoRuntime.IsDuo && _coordinator != null)
            {
                _coordinator.BroadcastTowerPlaced(card.CardId, level, validPos, placementId);
            }

            Debug.Log($"[Placement] Placed {card.DisplayName} L{level} at {validPos} for cost {cost}.");
            return true;
        }

        private GameObject PlaceTower(CardDefinition def, int level, Vector3 position, int ownerActor, int placementId, float towerRadius)
        {
            var go = TowerFactory.CreateTower(def, level, position, Quaternion.identity);
            if (go == null) return null;

            var towerBase = go.GetComponent<TowerBase>();
            towerBase?.SetOwner(ownerActor);
            towerBase?.SetPlacementId(placementId);

            // Adopting one of our own towers (rejoin resync) must advance the local counter,
            // otherwise the next placement reuses an id the partner still has bound.
            if (PhotonNetwork.LocalPlayer != null && ownerActor == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                ReserveLocalPlacementId(placementId);
            }
            if (towerRadius > 0f) towerBase?.SetPlacementRadius(towerRadius);

            go.name = $"Tower_{def.DisplayName}_L{level}";
            _towersByPlacementId[placementId] = go;

            var bind = go.GetComponent<TowerSnapBinding>();
            if (bind == null) bind = go.AddComponent<TowerSnapBinding>();
            bind.Bind(this, placementId, false);

            RefreshSnapPointColors(position);
            return go;
        }

        private void RegisterEffectLimits(GameObject towerGO, CardDefinition card, int level)
        {
            if (towerGO == null || card == null) return;

            if (TR.Systems.EffectLimitService.IsEnabled)
            {
                TR.Systems.EffectLimitService.Register(card, level);
                var eff = towerGO.GetComponent<EffectLimitBinding>();
                if (eff == null) eff = towerGO.AddComponent<EffectLimitBinding>();
                var types = TR.Systems.EffectLimitService.GetEffectTypesForCard(card, level);
                eff.SetTypes(types);
            }

            if (TR.Systems.EffectLimitService.CardCapsEnabled)
            {
                TR.Systems.EffectLimitService.RegisterCard(card);
                var binder = towerGO.GetComponent<CardLimitBinding>();
                if (binder == null) binder = towerGO.AddComponent<CardLimitBinding>();
                binder.SetCardId(card.CardId);
            }

            if (TR.Systems.EffectLimitService.RarityCapsEnabled)
            {
                TR.Systems.EffectLimitService.RegisterRarity(card);
                var rarityBinder = towerGO.GetComponent<RarityLimitBinding>();
                if (rarityBinder == null) rarityBinder = towerGO.AddComponent<RarityLimitBinding>();
                rarityBinder.SetRarityKey(TR.Systems.EffectLimitService.GetRarityKey(card));
            }
        }

        private void OnRemoteTowerPlaced(string cardId, int level, Vector3 position, int placementId, int ownerActorNumber)
        {
            var card = GameDB.GetCardById(cardId);
            if (card == null) return;
            if (_towersByPlacementId.TryGetValue(placementId, out var staleGO) && staleGO != null)
            {
                var staleBase = staleGO.GetComponent<TowerBase>();
                if (staleBase != null && staleBase.OwnerActorNumber == ownerActorNumber &&
                    (staleBase.Definition == null || staleBase.Definition.CardId != cardId ||
                     Vector3.Distance(staleBase.transform.position, new Vector3(position.x, position.y, 0f)) > 0.01f))
                {
                    RemoveTower(staleBase.PlacementId, broadcast: false, refund: false);
                }
                else
                {
                    return;
                }
            }

            float radius = GetCardRadius(card);
            Vector3 pos = new Vector3(position.x, position.y, 0f);

            int masterActor = PhotonNetwork.MasterClient != null ? PhotonNetwork.MasterClient.ActorNumber : -1;
            bool incomingIsMaster = ownerActorNumber == masterActor;

            if (!IsPositionFree(pos, radius))
            {
                GameObject existingGO = FindTowerAt(pos, radius);
                if (existingGO == null) return;
                var existingBase = existingGO.GetComponent<TowerBase>();
                if (existingBase != null)
                {
                    if (existingBase.PlacementId == placementId) return;

                    if (incomingIsMaster)
                    {
                        RemoveTower(existingBase.PlacementId, broadcast: false);
                    }
                    else
                    {
                        return;
                    }
                }
            }

            if (!IsInsideAnyArea(pos))
            {
                return;
            }

            bool isLocalOwner = ownerActorNumber >= 1 && PhotonNetwork.LocalPlayer != null && ownerActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
            bool ownerInactive = ownerActorNumber < 1 || (PhotonNetwork.CurrentRoom != null && (!PhotonNetwork.CurrentRoom.Players.TryGetValue(ownerActorNumber, out var ownerPlayer) || ownerPlayer == null || ownerPlayer.IsInactive));
            bool shouldSimulate = isLocalOwner || ownerInactive;

            var go = PlaceTower(card, level, pos, ownerActorNumber, placementId, radius);
            if (go == null) return;

            if (isLocalOwner)
            {
                go.name = $"Tower_{card.DisplayName}_L{level}";
                RegisterEffectLimits(go, card, level);
            }
            else if (shouldSimulate)
            {
                go.name = $"Tower_{card.DisplayName}_L{level}";
            }
            else
            {
                go.name = $"Tower_{card.DisplayName}_L{level}_Mirror";
                MakeVisualOnly(go);
            }

            var bind = go.GetComponent<TowerSnapBinding>();
            if (bind != null) bind.SetMirror(!shouldSimulate);

            Debug.Log($"[Placement] {(isLocalOwner ? "Local" : "Remote")} tower {card.DisplayName} L{level} at {pos} ID {placementId}.");
        }

        private void OnRemoteTowerRemoved(int placementId, bool playDestroyFeedback)
        {
            if (_towersByPlacementId.TryGetValue(placementId, out var go))
            {
                _towersByPlacementId.Remove(placementId);
                if (go != null)
                {
                    // Mirror the destruction effect the remote side already played.
                    if (playDestroyFeedback) go.GetComponent<TowerBase>()?.PlayDestroyFeedback();

                    var bind = go.GetComponent<TowerSnapBinding>();
                    bind?.Unbind();
                    Destroy(go);
                }
            }
            Debug.Log($"[Placement] Removed tower ID {placementId} (remote, vfx={playDestroyFeedback}).");
        }

        private void OnRemoteTowerHp(int placementId, float hp)
        {
            if (!_towersByPlacementId.TryGetValue(placementId, out var go)) return;
            if (go == null) return;
            ApplyTowerHp(go, hp);
        }

        private static void ApplyTowerHp(GameObject go, float hp)
        {
            if (go == null) return;
            var buff = go.GetComponent<BuffTower>(); if (buff != null) buff.SetRemoteHP(hp);
            var econ = go.GetComponent<EconomyTower>(); if (econ != null) econ.SetRemoteHP(hp);
        }

        private void OnTowerSyncRequested(int requesterActor)
        {
            if (!TR.Net.DuoRuntime.IsSimulationAuthority) return;

            var cardIds = new List<string>();
            var levels = new List<int>();
            var positions = new List<Vector3>();
            var placementIds = new List<int>();
            var owners = new List<int>();
            var hps = new List<float>();

            foreach (var tower in TowerBase.All)
            {
                if (tower == null || tower.Definition == null) continue;

                float hp = 1f;
                var buff = tower.GetComponent<BuffTower>();
                var econ = tower.GetComponent<EconomyTower>();
                if (buff != null) hp = buff.GetCurrentHP();
                else if (econ != null) hp = econ.GetCurrentHP();

                cardIds.Add(tower.Definition.CardId);
                levels.Add(tower.Level);
                positions.Add(tower.transform.position);
                placementIds.Add(tower.PlacementId);
                owners.Add(tower.OwnerActorNumber);
                hps.Add(hp);
            }

            if (_coordinator != null && cardIds.Count > 0)
            {
                _coordinator.SendTowerSync(requesterActor, cardIds.ToArray(), levels.ToArray(), positions.ToArray(), placementIds.ToArray(), owners.ToArray(), hps.ToArray());
            }

            RemirrorTowersOwnedBy(requesterActor);
        }

        // While a player is away the host takes over their towers (TakeOverTowerSimulation).
        // Once they ask for a resync they are simulating those towers again, so drop back to
        // mirroring them here — otherwise both clients simulate the same tower.
        private void RemirrorTowersOwnedBy(int actorNumber)
        {
            if (actorNumber < 1 || PhotonNetwork.LocalPlayer == null) return;
            if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber) return;

            int mirrored = 0;
            foreach (var tower in TowerBase.All)
            {
                if (tower == null || tower.OwnerActorNumber != actorNumber) continue;
                if (tower.IsVisualOnly) continue;

                MakeVisualOnly(tower.gameObject);
                var bind = tower.GetComponent<TowerSnapBinding>();
                if (bind != null) bind.SetMirror(true);
                mirrored++;
            }
            if (mirrored > 0)
                Debug.Log($"[Placement] Handed {mirrored} tower(s) back to actor {actorNumber} after rejoin.");
        }

        private void OnTowerSyncReceived(string[] cardIds, int[] levels, Vector3[] positions, int[] placementIds, int[] owners, float[] hps)
        {
            if (cardIds == null || positions == null || placementIds == null) return;
            int count = Mathf.Min(cardIds.Length, levels?.Length ?? 0, positions.Length, placementIds.Length, owners?.Length ?? 0, hps?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                PlaceSyncedTower(cardIds[i], levels[i], positions[i], placementIds[i], owners[i], hps[i]);
            }
        }

        private void PlaceSyncedTower(string cardId, int level, Vector3 position, int placementId, int ownerActor, float hp)
        {
            var card = GameDB.GetCardById(cardId);
            if (card == null) return;
            if (placementId < 0) return;

            float radius = GetCardRadius(card);
            Vector3 pos = new Vector3(position.x, position.y, 0f);

            if (!IsInsideAnyArea(pos))
            {
                return;
            }

            // Remove any existing entry for this placement ID if it does not match exactly.
            if (_towersByPlacementId.TryGetValue(placementId, out var syncedStaleGO) && syncedStaleGO != null)
            {
                var syncedStaleBase = syncedStaleGO.GetComponent<TowerBase>();
                if (syncedStaleBase != null && syncedStaleBase.OwnerActorNumber == ownerActor &&
                    syncedStaleBase.Definition != null && syncedStaleBase.Definition.CardId == cardId &&
                    Vector3.Distance(syncedStaleBase.transform.position, pos) <= 0.01f)
                {
                    // Already in sync; just refresh HP and bail.
                    ApplyTowerHp(syncedStaleGO, hp);
                    return;
                }
                RemoveTower(placementId, broadcast: false, refund: false);
            }

            // The sync is authoritative: clear any other tower occupying this position.
            GameObject existingAtPos = FindTowerAt(pos, radius);
            if (existingAtPos != null)
            {
                var existingBase = existingAtPos.GetComponent<TowerBase>();
                if (existingBase != null)
                {
                    if (existingBase.PlacementId == placementId)
                    {
                        ApplyTowerHp(existingAtPos, hp);
                        return;
                    }
                    RemoveTower(existingBase.PlacementId, broadcast: false, refund: false);
                }
            }

            bool isLocalOwner = ownerActor >= 1 && PhotonNetwork.LocalPlayer != null && ownerActor == PhotonNetwork.LocalPlayer.ActorNumber;
            bool ownerInactive = ownerActor < 1 || (PhotonNetwork.CurrentRoom != null && (!PhotonNetwork.CurrentRoom.Players.TryGetValue(ownerActor, out var ownerPlayer) || ownerPlayer == null || ownerPlayer.IsInactive));
            bool shouldSimulate = isLocalOwner || ownerInactive;

            var go = PlaceTower(card, level, pos, ownerActor, placementId, radius);
            if (go == null) return;

            if (isLocalOwner)
            {
                go.name = $"Tower_{card.DisplayName}_L{level}";
                RegisterEffectLimits(go, card, level);
            }
            else if (shouldSimulate)
            {
                go.name = $"Tower_{card.DisplayName}_L{level}";
            }
            else
            {
                go.name = $"Tower_{card.DisplayName}_L{level}_Mirror";
                MakeVisualOnly(go);
            }

            var bind = go.GetComponent<TowerSnapBinding>();
            if (bind != null) bind.SetMirror(!shouldSimulate);

            ApplyTowerHp(go, hp);
            RefreshSnapPointColors(pos);
        }

        public void NotifyTowerDestroyed(int placementId, bool isMirror, bool playedDestroyFeedback = false)
        {
            if (_towersByPlacementId.ContainsKey(placementId))
            {
                _towersByPlacementId.Remove(placementId);
            }

            if (!TR.Net.DuoRuntime.IsDuo || _coordinator == null || placementId < 0) return;

            // The owner reports its own towers. The simulation authority also reports mirrors it
            // destroyed, because enemy abilities (pulse nuke) only run on the authority and would
            // otherwise leave the tower standing on the owner's client.

            if (isMirror && !TR.Net.DuoRuntime.IsSimulationAuthority) return;

            _coordinator.BroadcastTowerRemoved(placementId, playedDestroyFeedback);
        }

        private static void MakeVisualOnly(GameObject go)
        {
            var tb = go.GetComponent<TowerBase>();
            if (tb != null) tb.SetVisualOnly(true);

            var inf = go.GetComponent<InfernoTower>(); if (inf != null) inf.SetVisualOnly(true);
            var buff = go.GetComponent<BuffTower>(); if (buff != null) buff.SetVisualOnly(true);
            var econ = go.GetComponent<EconomyTower>(); if (econ != null) econ.SetVisualOnly(true);
        }

        private GameObject FindTowerAt(Vector3 pos, float radius)
        {
            float min = radius + minTowerSpacing;
            foreach (var tower in TowerBase.All)
            {
                if (tower == null) continue;
                if (Vector2.Distance(new Vector2(pos.x, pos.y), new Vector2(tower.transform.position.x, tower.transform.position.y)) < min + tower.PlacementRadius)
                    return tower.gameObject;
            }
            return null;
        }

        private void RemoveTower(int placementId, bool broadcast, bool refund = true)
        {
            if (!_towersByPlacementId.TryGetValue(placementId, out var go)) return;

            var bind = go != null ? go.GetComponent<TowerSnapBinding>() : null;
            bind?.Unbind();

            var tb = go != null ? go.GetComponent<TowerBase>() : null;
            if (tb != null && refund && tb.IsLocalOwner)
            {
                tb.DestroyForRefund(1f);
            }
            else
            {
                if (go != null) Destroy(go);
            }

            _towersByPlacementId.Remove(placementId);

            if (broadcast && tb != null && !tb.IsVisualOnly && TR.Net.DuoRuntime.IsDuo && _coordinator != null)
            {
                _coordinator.BroadcastTowerRemoved(placementId, tb.PlayedDestroyFeedback);
            }
        }

        public float GetCardRadius(CardDefinition card)
        {
            if (card == null) return 0.4f;
            if (_cardRadiusCache.TryGetValue(card, out float r)) return r;

            float radius = 0.4f;
            GameObject prefab = card.TowerPrefab;
            if (prefab != null)
            {
                var col = prefab.GetComponent<Collider2D>();
                if (col == null) col = prefab.GetComponentInChildren<Collider2D>(true);
                if (col is CircleCollider2D cc)
                {
                    radius = cc.radius * Mathf.Max(col.transform.lossyScale.x, col.transform.lossyScale.y);
                }
                else if (col is BoxCollider2D bc)
                {
                    float scale = Mathf.Max(col.transform.lossyScale.x, col.transform.lossyScale.y);
                    radius = Mathf.Max(bc.size.x, bc.size.y) * scale * 0.5f;
                }

                var sr = prefab.GetComponent<SpriteRenderer>();
                if (sr == null) sr = prefab.GetComponentInChildren<SpriteRenderer>(true);
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
            }

            radius = Mathf.Max(0.25f, radius);
            _cardRadiusCache[card] = radius;
            return radius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            foreach (var area in _areas)
            {
                if (area.Collider == null) continue;
                Bounds b = area.Collider.bounds;
                Gizmos.DrawWireCube(b.center, b.size);
            }
        }
    }
}
