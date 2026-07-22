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
        [SerializeField] private Color areaHighlightColor = new Color(0.4f, 1f, 0.4f, 0.65f);

        private MatchEconomy _economy;
        private bool _areasVisible;
        private int _nextLocalPlacementId = 1;

        private readonly List<Area> _areas = new();
        private readonly Dictionary<int, GameObject> _towersByPlacementId = new();
        private readonly Dictionary<CardDefinition, float> _cardRadiusCache = new();
        private TR.Net.DuoBattleCoordinator _coordinator;

        private SpriteRenderer _areaVisual;
        private Texture2D _areaTexture;
        private Color[] _areaBasePixels;
        private Color[] _areaWorkPixels;
        private int _areaTexWidth;
        private int _areaTexHeight;
        private float _areaPPU;
        private Vector2 _areaWorldMin;

        private readonly List<int> _highlighted = new();
        private readonly List<int> _lastHighlighted = new();

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

            if (_areaVisual != null)
            {
                Destroy(_areaVisual.gameObject);
                _areaVisual = null;
            }
        }

        private void BuildAreaList()
        {
            _areas.Clear();
            if (placementAreasRoot == null)
            {
                Debug.LogWarning("[Placement] placementAreasRoot is not assigned.");
                return;
            }

            if (_areaVisual != null)
            {
                Destroy(_areaVisual.gameObject);
                _areaVisual = null;
            }

            int found = 0;
            int skipped = 0;
            foreach (Transform child in placementAreasRoot)
            {
                if (child == null) continue;

                if (child.name == "PlacementAreaVisual" && child.GetComponent<BoxCollider2D>() == null)
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

            if (_areas.Count > 1)
                BuildCombinedAreaVisual();
            else
                BuildPerAreaVisuals();

            Debug.Log($"[Placement] Built {found} area(s) from {placementAreasRoot.name}, skipped {skipped} children without BoxCollider2D.");
        }

        private void BuildCombinedAreaVisual()
        {
            Bounds total = new Bounds(_areas[0].Collider.bounds.center, _areas[0].Collider.bounds.size);
            for (int i = 1; i < _areas.Count; i++)
            {
                if (_areas[i].Collider != null)
                    total.Encapsulate(_areas[i].Collider.bounds);
            }

            const float padding = 0.1f;
            Vector2 min = new Vector2(total.min.x - padding, total.min.y - padding);
            Vector2 max = new Vector2(total.max.x + padding, total.max.y + padding);
            Vector2 size = max - min;

            _areaWorldMin = min;
            float maxDim = Mathf.Max(size.x, size.y);
            _areaPPU = maxDim > 0.001f ? maxAreaTextureSize / maxDim : 32f;
            _areaTexWidth = Mathf.Max(1, Mathf.CeilToInt(size.x * _areaPPU));
            _areaTexHeight = Mathf.Max(1, Mathf.CeilToInt(size.y * _areaPPU));

            _areaTexture = new Texture2D(_areaTexWidth, _areaTexHeight, TextureFormat.RGBA32, false);
            _areaTexture.filterMode = FilterMode.Bilinear;
            _areaTexture.wrapMode = TextureWrapMode.Clamp;

            _areaBasePixels = new Color[_areaTexWidth * _areaTexHeight];
            _areaWorkPixels = new Color[_areaTexWidth * _areaTexHeight];

            for (int i = 0; i < _areaBasePixels.Length; i++) _areaBasePixels[i] = Color.clear;

            foreach (var area in _areas)
            {
                if (area.Collider == null) continue;
                FillRect(_areaBasePixels, area.Collider.bounds, areaBaseColor);
            }

            Array.Copy(_areaBasePixels, _areaWorkPixels, _areaBasePixels.Length);
            _areaTexture.SetPixels(_areaWorkPixels);
            _areaTexture.Apply();

            var sprite = Sprite.Create(_areaTexture, new Rect(0, 0, _areaTexWidth, _areaTexHeight), new Vector2(0f, 0f), _areaPPU);

            var go = new GameObject("PlacementAreasVisual");
            go.transform.SetParent(placementAreasRoot, false);
            go.transform.position = new Vector3(min.x, min.y, 0f);

            _areaVisual = go.AddComponent<SpriteRenderer>();
            _areaVisual.sprite = sprite;
            _areaVisual.color = Color.white;
            _areaVisual.sortingOrder = 1;
            _areaVisual.gameObject.SetActive(false);
        }

        private void BuildPerAreaVisuals()
        {
            foreach (var area in _areas)
            {
                if (area.Collider == null) continue;
                var visual = EnsureAreaVisual(area.Transform, area.Collider);
                if (visual != null)
                {
                    visual.gameObject.SetActive(false);
                    visual.color = areaBaseColor;
                }
            }
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
                renderer.transform.localScale = new Vector3(box.size.x, box.size.y, 1f);
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

        private void FillRect(Color[] pixels, Bounds b, Color color)
        {
            int xStart = Mathf.Clamp(Mathf.FloorToInt((b.min.x - _areaWorldMin.x) * _areaPPU), 0, _areaTexWidth - 1);
            int xEnd = Mathf.Clamp(Mathf.CeilToInt((b.max.x - _areaWorldMin.x) * _areaPPU), 0, _areaTexWidth - 1);
            int yStart = Mathf.Clamp(Mathf.FloorToInt((b.min.y - _areaWorldMin.y) * _areaPPU), 0, _areaTexHeight - 1);
            int yEnd = Mathf.Clamp(Mathf.CeilToInt((b.max.y - _areaWorldMin.y) * _areaPPU), 0, _areaTexHeight - 1);

            for (int y = yStart; y <= yEnd; y++)
            {
                int row = y * _areaTexWidth;
                for (int x = xStart; x <= xEnd; x++)
                {
                    pixels[row + x] = color;
                }
            }
        }

        private int GeneratePlacementId()
        {
            int actor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 1;
            return actor * 100000 + _nextLocalPlacementId++;
        }

        public void SetSnapPointsVisible(bool visible)
        {
            _areasVisible = visible;
            if (visible)
            {
                Debug.Log($"[Placement] Showing {_areas.Count} placement area overlay(s).");
                _lastHighlighted.Clear();
                RebuildAreaVisual(null);
            }
            if (_areaVisual != null)
                _areaVisual.gameObject.SetActive(visible);
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

        private void RebuildAreaVisual(List<int> highlightedIndices)
        {
            if (_areaTexture == null) return;

            Array.Copy(_areaBasePixels, _areaWorkPixels, _areaBasePixels.Length);

            if (highlightedIndices != null)
            {
                foreach (int idx in highlightedIndices)
                {
                    if (idx < 0 || idx >= _areas.Count) continue;
                    var col = _areas[idx].Collider;
                    if (col == null) continue;
                    FillRect(_areaWorkPixels, col.bounds, areaHighlightColor);
                }
            }

            _areaTexture.SetPixels(_areaWorkPixels);
            _areaTexture.Apply();
        }

        private static bool ListsEqual(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private int GetAreaIndex(Vector3 pos)
        {
            Vector2 p = new Vector2(pos.x, pos.y);
            for (int i = 0; i < _areas.Count; i++)
            {
                var col = _areas[i].Collider;
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                var b = col.bounds;
                if (b.Contains(new Vector3(p.x, p.y, b.center.z))) return i;
            }
            return -1;
        }

        private void GetAllAreaIndices(Vector3 pos, List<int> result)
        {
            Vector2 p = new Vector2(pos.x, pos.y);
            for (int i = 0; i < _areas.Count; i++)
            {
                var col = _areas[i].Collider;
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                var b = col.bounds;
                if (b.Contains(new Vector3(p.x, p.y, b.center.z)))
                    result.Add(i);
            }
        }

        public bool IsInsideAnyArea(Vector3 pos)
        {
            return GetAreaIndex(pos) >= 0;
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
        }

        private void OnRemoteTowerPlaced(string cardId, int level, Vector3 position, int placementId, int ownerActorNumber)
        {
            var card = GameDB.GetCardById(cardId);
            if (card == null) return;
            if (_towersByPlacementId.ContainsKey(placementId)) return;

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

        private void OnRemoteTowerRemoved(int placementId)
        {
            if (_towersByPlacementId.TryGetValue(placementId, out var go))
            {
                _towersByPlacementId.Remove(placementId);
                if (go != null)
                {
                    var bind = go.GetComponent<TowerSnapBinding>();
                    bind?.Unbind();

                    var tb = go.GetComponent<TowerBase>();
                    if (tb != null && tb.IsLocalOwner)
                    {
                        tb.DestroyForRefund(1f);
                    }
                    else
                    {
                        Destroy(go);
                    }
                }
            }
            Debug.Log($"[Placement] Removed tower ID {placementId} (remote).");
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
            if (_towersByPlacementId.ContainsKey(placementId)) return;

            float radius = GetCardRadius(card);
            Vector3 pos = new Vector3(position.x, position.y, 0f);

            if (!IsInsideAnyArea(pos) || !IsPositionFree(pos, radius))
            {
                return;
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

        public void NotifyTowerDestroyed(int placementId, bool isMirror)
        {
            if (_towersByPlacementId.ContainsKey(placementId))
            {
                _towersByPlacementId.Remove(placementId);
            }

            if (!isMirror && TR.Net.DuoRuntime.IsDuo && _coordinator != null && placementId >= 0)
            {
                _coordinator.BroadcastTowerRemoved(placementId);
            }
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

        private void RemoveTower(int placementId, bool broadcast)
        {
            if (!_towersByPlacementId.TryGetValue(placementId, out var go)) return;

            var tb = go != null ? go.GetComponent<TowerBase>() : null;
            if (tb != null && tb.IsLocalOwner)
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
                _coordinator.BroadcastTowerRemoved(placementId);
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
