using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TR.Data;

namespace TR.Systems
{
    
    
    
    
    
    public static class GameDB
    {
        private static bool _loaded;
        private static readonly List<RarityDefinition> _rarities = new();
        private static readonly List<CardDefinition> _cards = new();
        private static readonly List<PackDefinition> _packs = new();
        private static readonly List<ArenaDefinition> _arenas = new();
        private static readonly List<EnemyDefinition> _enemies = new();

        private static readonly Dictionary<string, RarityDefinition> _rarityById = new();
        private static readonly Dictionary<string, CardDefinition> _cardById = new();
        private static readonly Dictionary<string, PackDefinition> _packById = new();
        private static readonly Dictionary<RarityDefinition, List<CardDefinition>> _cardsByRarity = new();
        private static readonly Dictionary<string, ArenaDefinition> _arenaById = new();
        private static readonly Dictionary<string, EnemyDefinition> _enemyById = new();
        private static CastleProgression _castleProgression;
        private static TR.Data.GameplayConfig _gameplayConfig;
        private static TR.Data.Progression.TrophyRoadDefinition _trophyRoad;

        public static IReadOnlyList<RarityDefinition> Rarities => _rarities;
        public static IReadOnlyList<CardDefinition> Cards => _cards;
        public static IReadOnlyList<PackDefinition> Packs => _packs;
        public static IReadOnlyList<ArenaDefinition> Arenas => _arenas;
        public static IReadOnlyList<EnemyDefinition> Enemies => _enemies;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            _rarities.Clear();
            _cards.Clear();
            _packs.Clear();
            _rarityById.Clear();
            _cardById.Clear();
            _packById.Clear();
            _cardsByRarity.Clear();
            _arenas.Clear();
            _arenaById.Clear();
            _enemies.Clear();
            _enemyById.Clear();
            _castleProgression = null;
            _gameplayConfig = null;
            _trophyRoad = null;

            
            _rarities.AddRange(Resources.LoadAll<RarityDefinition>("Rarities"));
            _cards.AddRange(Resources.LoadAll<CardDefinition>("Cards"));
            _packs.AddRange(Resources.LoadAll<PackDefinition>("Packs"));
            _arenas.AddRange(Resources.LoadAll<ArenaDefinition>("Arenas"));
            _enemies.AddRange(Resources.LoadAll<EnemyDefinition>("Enemies"));
            var castleConfigs = Resources.LoadAll<CastleProgression>("Config");
            if (castleConfigs != null && castleConfigs.Length > 0)
            {
                _castleProgression = castleConfigs[0];
            }
            var gameplayConfigs = Resources.LoadAll<TR.Data.GameplayConfig>("Config");
            if (gameplayConfigs != null && gameplayConfigs.Length > 0)
            {
                _gameplayConfig = gameplayConfigs[0];
            }

            
            var roads = Resources.LoadAll<TR.Data.Progression.TrophyRoadDefinition>("Progression");
            if (roads != null && roads.Length > 0)
            {
                _trophyRoad = roads[0];
            }

            foreach (var r in _rarities)
            {
                if (!string.IsNullOrWhiteSpace(r.RarityId) && !_rarityById.ContainsKey(r.RarityId))
                    _rarityById.Add(r.RarityId, r);
            }

            foreach (var c in _cards)
            {
                if (!string.IsNullOrWhiteSpace(c.CardId) && !_cardById.ContainsKey(c.CardId))
                    _cardById.Add(c.CardId, c);
                if (c.Rarity != null)
                {
                    if (!_cardsByRarity.TryGetValue(c.Rarity, out var list))
                    {
                        list = new List<CardDefinition>();
                        _cardsByRarity[c.Rarity] = list;
                    }
                    list.Add(c);
                }
            }

            foreach (var p in _packs)
            {
                if (!string.IsNullOrWhiteSpace(p.PackId) && !_packById.ContainsKey(p.PackId))
                    _packById.Add(p.PackId, p);
            }

            foreach (var a in _arenas)
            {
                if (!string.IsNullOrWhiteSpace(a.ArenaId) && !_arenaById.ContainsKey(a.ArenaId))
                    _arenaById.Add(a.ArenaId, a);
            }

            foreach (var e in _enemies)
            {
                if (!string.IsNullOrWhiteSpace(e.EnemyId) && !_enemyById.ContainsKey(e.EnemyId))
                    _enemyById.Add(e.EnemyId, e);
            }
        }

        public static RarityDefinition GetRarityById(string id)
        {
            EnsureLoaded();
            return id != null && _rarityById.TryGetValue(id, out var r) ? r : null;
        }

        public static CardDefinition GetCardById(string id)
        {
            EnsureLoaded();
            return id != null && _cardById.TryGetValue(id, out var c) ? c : null;
        }

        public static PackDefinition GetPackById(string id)
        {
            EnsureLoaded();
            return id != null && _packById.TryGetValue(id, out var p) ? p : null;
        }

        public static ArenaDefinition GetArenaById(string id)
        {
            EnsureLoaded();
            return id != null && _arenaById.TryGetValue(id, out var a) ? a : null;
        }

        public static IReadOnlyList<ArenaDefinition> GetArenasSortedByRequirement()
        {
            EnsureLoaded();
            return _arenas.OrderBy(a => a.TrophyRequirement).ToList();
        }

        public static EnemyDefinition GetEnemyById(string id)
        {
            EnsureLoaded();
            return id != null && _enemyById.TryGetValue(id, out var e) ? e : null;
        }

        public static CastleProgression GetCastleProgression()
        {
            EnsureLoaded();
            return _castleProgression;
        }

        public static TR.Data.GameplayConfig GetGameplayConfig()
        {
            EnsureLoaded();
            return _gameplayConfig;
        }

        public static TR.Data.Progression.TrophyRoadDefinition GetTrophyRoad()
        {
            EnsureLoaded();
            return _trophyRoad;
        }

        public static IReadOnlyList<CardDefinition> GetCardsByRarity(RarityDefinition rarity)
        {
            EnsureLoaded();
            if (rarity == null) return System.Array.Empty<CardDefinition>();
            return _cardsByRarity.TryGetValue(rarity, out var list) ? (IReadOnlyList<CardDefinition>)list : System.Array.Empty<CardDefinition>();
        }

        public static CardDefinition GetRandomCardByRarity(RarityDefinition rarity, System.Random rng = null)
        {
            EnsureLoaded();
            var list = GetCardsByRarity(rarity);
            if (list == null || list.Count == 0)
            {
                
                if (_cards.Count == 0) return null;
                rng ??= new System.Random();
                return _cards[rng.Next(0, _cards.Count)];
            }
            rng ??= new System.Random();
            return list[rng.Next(0, list.Count)];
        }
    }
}
