using System.Collections.Generic;
using UnityEngine;
using TR.Data;

namespace TR.Systems
{
    public static class PackService
    {
        // Rolls cards from a given PackDefinition using its rarity weights.
        // Returns the list of CardDefinitions (duplicates allowed). Awarding/collection is handled elsewhere.
        public static List<CardDefinition> OpenPack(PackDefinition pack, System.Random rng = null)
        {
            GameDB.EnsureLoaded();
            var results = new List<CardDefinition>();
            if (pack == null)
                return results;

            rng ??= new System.Random();

            // Specific card mode: bypass rarity weights entirely
            if (pack.GiveSpecificCardOnly && pack.SpecificCard != null)
            {
                int countSpecific = Mathf.Max(0, pack.CardsPerPack);
                for (int i = 0; i < countSpecific; i++)
                {
                    results.Add(pack.SpecificCard);
                }
                return results;
            }

            var weights = BuildWeights(pack);
            int count = Mathf.Max(0, pack.CardsPerPack);

            // If a guaranteed rarity is specified, reserve one slot for it
            RarityDefinition guaranteed = pack.GuaranteedRarity;
            if (guaranteed != null && count > 0)
            {
                var guaranteedCard = GetRandomUnlockedCardByRarity(guaranteed, rng);
                if (guaranteedCard == null)
                {
                    // Fallback: any unlocked card
                    guaranteedCard = GetRandomUnlockedCardAny(rng);
                }
                if (guaranteedCard != null)
                {
                    results.Add(guaranteedCard);
                    count -= 1;
                }
            }

            // Fill remaining slots using weighted rolls
            for (int i = 0; i < count; i++)
            {
                int idx = WeightedRandom.PickIndex(weights, rng);
                RarityDefinition pickedRarity = null;
                if (idx >= 0 && idx < pack.RarityWeights.Length)
                    pickedRarity = pack.RarityWeights[idx].rarity;

                CardDefinition card = null;
                if (pickedRarity != null)
                {
                    card = GetRandomUnlockedCardByRarity(pickedRarity, rng);
                }
                // Fallback: any unlocked
                card ??= GetRandomUnlockedCardAny(rng);
                if (card != null) results.Add(card);
            }

            return results;
        }

        public static List<CardDefinition> OpenPackById(string packId, System.Random rng = null)
        {
            var pack = GameDB.GetPackById(packId);
            return OpenPack(pack, rng);
        }

        private static int[] BuildWeights(PackDefinition pack)
        {
            var entries = pack != null ? pack.RarityWeights : null;
            if (entries == null || entries.Length == 0) return System.Array.Empty<int>();
            var weights = new int[entries.Length];
            if (pack.UsePercentages)
            {
                // Convert percents to integer weights, normalizing to sum 100 if possible
                float sum = 0f;
                for (int i = 0; i < entries.Length; i++)
                {
                    sum += Mathf.Max(0f, entries[i]?.percent ?? 0f);
                }
                if (sum <= 0.0001f)
                {
                    // Even distribution fallback
                    for (int i = 0; i < entries.Length; i++) weights[i] = 1;
                    return weights;
                }
                // Scale to 100 then round to ints while ensuring total >= 1
                int totalInt = 0;
                for (int i = 0; i < entries.Length; i++)
                {
                    float p = Mathf.Max(0f, entries[i].percent);
                    int w = Mathf.Max(0, Mathf.RoundToInt((p / sum) * 100f));
                    weights[i] = w;
                    totalInt += w;
                }
                // Ensure at least one slot has weight if rounding dropped all to 0
                if (totalInt == 0)
                {
                    int idx = 0;
                    for (int i = 1; i < entries.Length; i++) if ((entries[i]?.percent ?? 0f) > (entries[idx]?.percent ?? 0f)) idx = i;
                    weights[idx] = 1;
                }
            }
            else
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    weights[i] = Mathf.Max(0, entries[i]?.weight ?? 0);
                }
            }
            return weights;
        }

        // Helpers: unlocked filters
        private static CardDefinition GetRandomUnlockedCardByRarity(RarityDefinition rarity, System.Random rng)
        {
            var list = GameDB.GetCardsByRarity(rarity);
            if (list == null || list.Count == 0) return null;
            // Filter to unlocked-for-player
            var pool = new List<CardDefinition>();
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c != null && c.IsUnlockedForPlayer()) pool.Add(c);
            }
            if (pool.Count == 0) return null;
            rng ??= new System.Random();
            return pool[rng.Next(0, pool.Count)];
        }

        private static CardDefinition GetRandomUnlockedCardAny(System.Random rng)
        {
            var all = GameDB.Cards;
            if (all == null || all.Count == 0) return null;
            var pool = new List<CardDefinition>();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && c.IsUnlockedForPlayer()) pool.Add(c);
            }
            if (pool.Count == 0) return null;
            rng ??= new System.Random();
            return pool[rng.Next(0, pool.Count)];
        }
    }
}
