using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TR.Data;

namespace TR.Systems
{
    public static class ShopService
    {
        
        private static DateTimeOffset? _devRefreshedAtUtc = null;
        [Serializable]
        public class CardPointsOffer
        {
            public string cardId;
            public string rarityId; 
            public int points;
            public int cost;
            public bool sold;
        }

        
        
        
        
        public static List<CardPointsOffer> GetOrGenerateDailyCardPointOffers()
        {
            GameDB.EnsureLoaded();
            var cfg = GameDB.GetGameplayConfig();
            if (cfg == null || cfg.cardPointsOfferSlots == null || cfg.cardPointsOfferSlots.Length == 0)
            {
                return PlayerProfile.Data.cardPointOffers ?? new List<CardPointsOffer>();
            }

            
            bool hasEligible = HasAnyEligibleCard();
            if (!hasEligible)
            {
                
                if (PlayerProfile.Data.cardPointOffers == null) PlayerProfile.Data.cardPointOffers = new List<CardPointsOffer>();
                return PlayerProfile.Data.cardPointOffers;
            }

            int dayKey = GetCurrentDayKey(cfg.offersRefreshHourUTC);
            if (PlayerProfile.Data.cardPointOffers == null) PlayerProfile.Data.cardPointOffers = new List<CardPointsOffer>();

            
            if (PlayerProfile.Data.cardPointOffersDayKey != dayKey)
            {
                GenerateDailyCardPointOffers(cfg);
                PlayerProfile.Data.cardPointOffersDayKey = dayKey;
                PlayerProfile.Save();
                return PlayerProfile.Data.cardPointOffers;
            }

            
            var offers = PlayerProfile.Data.cardPointOffers;
            bool stale = offers == null || offers.Count == 0 || offers.TrueForAll(o => o == null || string.IsNullOrEmpty(o.cardId));
            if (stale)
            {
                GenerateDailyCardPointOffers(cfg);
                PlayerProfile.Data.cardPointOffersDayKey = dayKey; 
                PlayerProfile.Save();
            }
            return PlayerProfile.Data.cardPointOffers;
        }

        public static TimeSpan GetTimeUntilNextRefresh()
        {
            
            if (_devRefreshedAtUtc.HasValue)
            {
                var nowDev = DateTimeOffset.UtcNow;
                var elapsed = nowDev - _devRefreshedAtUtc.Value;
                var remainDev = TimeSpan.FromHours(24) - elapsed;
                if (remainDev <= TimeSpan.Zero)
                {
                    _devRefreshedAtUtc = null; 
                }
                else
                {
                    return remainDev;
                }
            }
            var cfg = GameDB.GetGameplayConfig();
            int hour = cfg != null ? Mathf.Clamp(cfg.offersRefreshHourUTC, 0, 23) : 0;
            var now = DateTimeOffset.UtcNow;
            var todayRefresh = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, TimeSpan.Zero);
            var next = now >= todayRefresh ? todayRefresh.AddDays(1) : todayRefresh;
            return next - now;
        }

        
        public static void ForceRefreshOffers()
        {
            
            var cfgs = Resources.LoadAll<GameplayConfig>("Config");
            var cfg = (cfgs != null && cfgs.Length > 0) ? cfgs[0] : GameDB.GetGameplayConfig();
            if (cfg == null || cfg.cardPointsOfferSlots == null || cfg.cardPointsOfferSlots.Length == 0) return;
            
            if (PlayerProfile.Data.cardPointOffers != null)
                PlayerProfile.Data.cardPointOffers.Clear();
            
            GenerateDailyCardPointOffers(cfg, forceRandomSeed: true);
            
            PlayerProfile.Data.cardPointOffersDayKey = GetCurrentDayKey(cfg.offersRefreshHourUTC);
            
            _devRefreshedAtUtc = DateTimeOffset.UtcNow;
            PlayerProfile.Save();
        }

        public static bool TryPurchaseCardPointsOffer(int index)
        {
            var offers = GetOrGenerateDailyCardPointOffers();
            if (index < 0 || index >= offers.Count) return false;
            var offer = offers[index];
            if (offer == null || offer.sold) return false;
            if (string.IsNullOrEmpty(offer.cardId)) return false;
            var card = GameDB.GetCardById(offer.cardId);
            if (card == null) return false;

            
            var cp = PlayerProfile.GetOrCreateCard(card.CardId);
            if (cp.ownedCount <= 0) return false;
            if (cp.level >= (card.Rarity != null ? card.Rarity.MaxLevel : int.MaxValue)) return false;

            if (!PlayerProfile.TrySpendSoftCurrency(Mathf.Max(0, offer.cost))) return false;

            cp.points += Mathf.Max(0, offer.points);
            offer.sold = true;
            PlayerProfile.Save();
            return true;
        }

        private static void GenerateDailyCardPointOffers(GameplayConfig cfg, bool forceRandomSeed = false)
        {
            var slots = cfg.cardPointsOfferSlots;
            var list = new List<CardPointsOffer>(slots.Length);
            
            var rng = forceRandomSeed
                ? new System.Random(unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode())
                : new System.Random(CreateDailySeed());

            
            var rarityById = GameDB.Rarities.ToDictionary(r => r.RarityId, r => r);
            
            var fallbackOrder = GameDB.Rarities.OrderBy(r => r.MaxLevel).ToList();

            
            var unlockedByRarity = new Dictionary<RarityDefinition, List<CardDefinition>>();
            foreach (var r in GameDB.Rarities)
            {
                var cards = GameDB.GetCardsByRarity(r).Where(c =>
                {
                    var cp = PlayerProfile.GetOrCreateCard(c.CardId);
                    if (cp.ownedCount <= 0) return false; 
                    if (cp.level >= r.MaxLevel) return false; 
                    return true;
                }).ToList();
                unlockedByRarity[r] = cards;
            }

            
            var usedCardIds = new HashSet<string>();

            foreach (var slot in slots)
            {
                if (slot == null) continue;
                
                RarityDefinition primary = null;
                if (!string.IsNullOrEmpty(slot.rarityId)) rarityById.TryGetValue(slot.rarityId, out primary);

                
                IEnumerable<RarityDefinition> searchOrder = Enumerable.Empty<RarityDefinition>();
                if (primary != null)
                {
                    searchOrder = new[] { primary }.Concat(fallbackOrder.Where(r => r != primary));
                }
                else
                {
                    searchOrder = fallbackOrder;
                }

                CardDefinition pickedCard = null;
                foreach (var r in searchOrder)
                {
                    var candidates = unlockedByRarity.TryGetValue(r, out var listForR) ? listForR : null;
                    if (candidates != null && candidates.Count > 0)
                    {
                        
                        var pool = candidates.Where(c => !usedCardIds.Contains(c.CardId)).ToList();
                        if (pool.Count > 0)
                        {
                            pickedCard = pool[rng.Next(0, pool.Count)];
                            break;
                        }
                    }
                }

                int points = 0;
                int cost = 0;
                string rarityIdOut = null;
                bool sold = false;

                if (pickedCard != null)
                {
                    int ptsMin = Math.Max(0, Math.Min(slot.pointsMin, slot.pointsMax));
                    int ptsMax = Math.Max(ptsMin, Math.Max(slot.pointsMin, slot.pointsMax));
                    points = rng.Next(ptsMin, ptsMax + 1);

                    int cppMin = Math.Max(0, Math.Min(slot.costPerPointMin, slot.costPerPointMax));
                    int cppMax = Math.Max(cppMin, Math.Max(slot.costPerPointMin, slot.costPerPointMax));
                    int cpp = rng.Next(cppMin, cppMax + 1);
                    cost = Math.Max(1, points * cpp);
                    rarityIdOut = pickedCard.Rarity != null ? pickedCard.Rarity.RarityId : null;
                    usedCardIds.Add(pickedCard.CardId);
                    sold = false;
                }
                else
                {
                    
                    points = 0;
                    cost = 0;
                    rarityIdOut = primary != null ? primary.RarityId : null;
                    sold = true;
                }

                list.Add(new CardPointsOffer
                {
                    cardId = pickedCard != null ? pickedCard.CardId : null,
                    rarityId = rarityIdOut,
                    points = points,
                    cost = cost,
                    sold = sold
                });
            }

            PlayerProfile.Data.cardPointOffers = list;
        }

        private static bool HasAnyEligibleCard()
        {
            
            foreach (var r in GameDB.Rarities)
            {
                var cards = GameDB.GetCardsByRarity(r);
                if (cards == null) continue;
                foreach (var c in cards)
                {
                    var cp = PlayerProfile.GetOrCreateCard(c.CardId);
                    if (cp.ownedCount > 0 && (c.Rarity == null || cp.level < c.Rarity.MaxLevel))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static int GetCurrentDayKey(int refreshHourUtc)
        {
            int hour = Mathf.Clamp(refreshHourUtc, 0, 23);
            var now = DateTimeOffset.UtcNow;
            var todayRefresh = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, TimeSpan.Zero);
            var effective = now >= todayRefresh ? todayRefresh : todayRefresh.AddDays(-1);
            return effective.Year * 10000 + effective.Month * 100 + effective.Day;
        }

        private static int CreateDailySeed()
        {
            var now = DateTimeOffset.UtcNow;
            return now.Year * 10000 + now.Month * 100 + now.Day;
        }
    }
}
