using System.Collections.Generic;
using UnityEngine;
using TR.Data;

namespace TR.Systems
{
    // Per-match tracker enforcing per-effect caps defined on the current ArenaDefinition
    public static class EffectLimitService
    {
        private static Dictionary<EffectType, int> _caps;
        private static Dictionary<EffectType, int> _counts;
        private static ArenaDefinition _arena;
        // Per-card caps
        private static Dictionary<string, int> _cardCaps;    // key: CardId
        private static Dictionary<string, int> _cardCounts;  // placements per card

        public static void Initialize(ArenaDefinition arena)
        {
            _arena = arena;
            _caps = new Dictionary<EffectType, int>();
            _counts = new Dictionary<EffectType, int>();
            _cardCaps = new Dictionary<string, int>();
            _cardCounts = new Dictionary<string, int>();
            if (arena != null)
            {
                var limits = arena.EffectLimits;
                if (limits != null)
                {
                    for (int i = 0; i < limits.Length; i++)
                    {
                        var t = limits[i].type;
                        int max = Mathf.Max(0, limits[i].maxCount);
                        if (t != EffectType.None)
                        {
                            _caps[t] = max;
                            if (!_counts.ContainsKey(t)) _counts[t] = 0;
                        }
                    }
                }
                var cardLimits = arena.CardLimits;
                if (cardLimits != null)
                {
                    for (int i = 0; i < cardLimits.Length; i++)
                    {
                        var c = cardLimits[i].card;
                        if (c == null) continue;
                        int max = Mathf.Max(0, cardLimits[i].maxCount);
                        string id = c.CardId;
                        if (string.IsNullOrEmpty(id)) continue;
                        _cardCaps[id] = max;
                        if (!_cardCounts.ContainsKey(id)) _cardCounts[id] = 0;
                    }
                }
            }
        }

        public static bool IsEnabled => _caps != null && _caps.Count > 0;
        public static bool CardCapsEnabled => _cardCaps != null && _cardCaps.Count > 0;

        public static IReadOnlyDictionary<EffectType, int> CurrentCounts => _counts;
        public static IReadOnlyDictionary<EffectType, int> CurrentCaps => _caps;

        public static HashSet<EffectType> GetEffectTypesForCard(CardDefinition def, int level)
        {
            var set = new HashSet<EffectType>();
            if (def == null) return set;
            level = Mathf.Max(1, level);
            // Slow
            if (def.HasSlowOnHit() && def.GetSlowPercent(level) > 0f && def.GetSlowDuration(level) > 0f)
                set.Add(EffectType.Slow);
            // Stun
            if (def.HasStunOnHit() && def.GetStunChance(level) > 0f && def.GetStunDuration(level) > 0f)
                set.Add(EffectType.Stun);
            // Burn
            if (def.GetBurnDps(level) > 0f && def.GetBurnDuration(level) > 0f)
                set.Add(EffectType.Burn);
            // Poison
            if (def.GetPoisonDps(level) > 0f && def.GetPoisonDuration(level) > 0f)
                set.Add(EffectType.Poison);
            // Frostbite (requires Frostbite toggle and positive values)
            if (def.HasFrostbiteOnHit() && def.GetFrostbiteDps(level) > 0f && def.GetFrostbiteDuration(level) > 0f)
                set.Add(EffectType.Frostbite);
            return set;
        }

        public static bool CanPlace(CardDefinition def, int level, out EffectType blockingType, out int cap, out int current)
        {
            blockingType = EffectType.None; cap = 0; current = 0;
            if (!IsEnabled) return true;
            var types = GetEffectTypesForCard(def, level);
            foreach (var t in types)
            {
                if (!_caps.TryGetValue(t, out var max) || max <= 0) continue; // no cap for this type
                int cnt = _counts.TryGetValue(t, out var c) ? c : 0;
                if (cnt + 1 > max)
                {
                    blockingType = t; cap = max; current = cnt;
                    return false;
                }
            }
            return true;
        }

        public static bool CanPlaceCard(CardDefinition def, out int cap, out int current)
        {
            cap = 0; current = 0;
            if (!CardCapsEnabled || def == null) return true;
            string id = def.CardId;
            if (string.IsNullOrEmpty(id)) return true;
            if (!_cardCaps.TryGetValue(id, out var max) || max <= 0) return true; // no cap for this card
            int cnt = _cardCounts.TryGetValue(id, out var c) ? c : 0;
            cap = max; current = cnt;
            return (cnt + 1) <= max;
        }

        public static void Register(CardDefinition def, int level)
        {
            if (!IsEnabled || def == null) return;
            var types = GetEffectTypesForCard(def, level);
            foreach (var t in types)
            {
                if (!_counts.ContainsKey(t)) _counts[t] = 0;
                _counts[t] = _counts[t] + 1;
            }
        }

        public static void Unregister(HashSet<EffectType> types)
        {
            if (!IsEnabled || types == null) return;
            foreach (var t in types)
            {
                if (_counts.TryGetValue(t, out var c))
                {
                    _counts[t] = Mathf.Max(0, c - 1);
                }
            }
        }

        public static void RegisterCard(CardDefinition def)
        {
            if (!CardCapsEnabled || def == null) return;
            string id = def.CardId;
            if (string.IsNullOrEmpty(id)) return;
            if (!_cardCounts.ContainsKey(id)) _cardCounts[id] = 0;
            _cardCounts[id] = _cardCounts[id] + 1;
        }

        public static void UnregisterCard(string cardId)
        {
            if (!CardCapsEnabled || string.IsNullOrEmpty(cardId)) return;
            if (_cardCounts.TryGetValue(cardId, out var c))
            {
                _cardCounts[cardId] = Mathf.Max(0, c - 1);
            }
        }
    }
}
