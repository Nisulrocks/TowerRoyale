using System.Collections.Generic;
using UnityEngine;
using TR.Data;

namespace TR.Systems
{
    
    public static class CollectionService
    {
        public struct AwardResult
        {
            public CardDefinition card;
            public bool isNew;
            public int pointsAwarded; 
            public int levelBefore;
            public int levelAfter;
        }

        
        public static string AwardCards(IEnumerable<CardDefinition> cards)
        {
            if (cards == null) return "No cards";
            var sb = new System.Text.StringBuilder();
            foreach (var card in cards)
            {
                if (card == null) continue;
                var progress = PlayerProfile.GetOrCreateCard(card.CardId);

                bool isNew = progress.ownedCount <= 0;
                progress.ownedCount++;

                if (isNew)
                {
                    
                    progress.level = 1;
                    progress.points = 0;
                    sb.AppendLine($"Unlocked {card.DisplayName} ({card.Rarity?.DisplayName})");
                }
                else
                {
                    
                    int currentLevel = Mathf.Max(1, progress.level);
                    int awarded = card.Rarity != null ? card.Rarity.RollDuplicatePoints(currentLevel) : 1;
                    awarded = Mathf.Max(1, awarded);
                    progress.points += awarded;
                    sb.AppendLine($"Duplicate {card.DisplayName} -> +{awarded} pts (L{currentLevel})");
                }
            }

            PlayerProfile.Save();
            return sb.ToString();
        }

        
        public static List<AwardResult> AwardCardsDetailed(IEnumerable<CardDefinition> cards)
        {
            var results = new List<AwardResult>();
            if (cards == null) return results;

            foreach (var card in cards)
            {
                if (card == null) continue;
                var progress = PlayerProfile.GetOrCreateCard(card.CardId);
                int levelBefore = Mathf.Max(0, progress.level);
                bool isNew = progress.ownedCount <= 0;
                int pointsAwarded = 0;

                progress.ownedCount++;
                if (isNew)
                {
                    progress.level = 1;
                    progress.points = 0;
                }
                else
                {
                    int currentLevel = Mathf.Max(1, progress.level);
                    int awarded = card.Rarity != null ? card.Rarity.RollDuplicatePoints(currentLevel) : 1;
                    awarded = Mathf.Max(1, awarded);
                    progress.points += awarded;
                    pointsAwarded = awarded;
                }

                results.Add(new AwardResult
                {
                    card = card,
                    isNew = isNew,
                    pointsAwarded = pointsAwarded,
                    levelBefore = levelBefore,
                    levelAfter = progress.level
                });
            }

            PlayerProfile.Save();
            return results;
        }

        
        public static bool TryAutoUpgrade(CardDefinition card, CardProgress progress)
        {
            if (card == null || progress == null) return false;
            var rarity = card.Rarity;
            if (rarity == null) return false;

            bool upgraded = false;
            while (progress.level < rarity.MaxLevel)
            {
                int nextLevel = progress.level + 1;
                int required = rarity.GetPointsRequiredForLevel(nextLevel);
                if (progress.points >= required)
                {
                    progress.points -= required;
                    progress.level = nextLevel;
                    
                    int castleXp = rarity.GetCastleXpForUpgradeLevel(nextLevel);
                    if (castleXp > 0) PlayerProfile.AddCastleXP(castleXp);
                    upgraded = true;
                }
                else break;
            }
            return upgraded;
        }

        
        public static bool TryPurchaseUpgradeById(string cardId)
        {
            var card = GameDB.GetCardById(cardId);
            if (card == null) return false;
            var progress = PlayerProfile.GetOrCreateCard(cardId);
            return TryPurchaseUpgrade(card, progress);
        }

        public static bool TryPurchaseUpgrade(CardDefinition card, CardProgress progress)
        {
            if (card == null || progress == null) return false;
            var rarity = card.Rarity;
            if (rarity == null) return false;
            int currentLevel = Mathf.Max(1, progress.level);
            if (currentLevel >= rarity.MaxLevel) return false;
            int nextLevel = currentLevel + 1;
            int ptsRequired = rarity.GetPointsRequiredForLevel(nextLevel);
            int cost = rarity.GetUpgradeCostForLevel(nextLevel);
            if (progress.points < ptsRequired) return false;
            if (!PlayerProfile.TrySpendSoftCurrency(cost)) return false;
            
            progress.points -= ptsRequired;
            progress.level = nextLevel;
            
            int castleXp = rarity.GetCastleXpForUpgradeLevel(nextLevel);
            if (castleXp > 0) PlayerProfile.AddCastleXP(castleXp);
            PlayerProfile.Save();
            return true;
        }

        
        public static bool GetUpgradeInfo(CardDefinition card, out int nextLevel, out int pointsRequired, out int cost)
        {
            nextLevel = 0; pointsRequired = 0; cost = 0;
            if (card == null || card.Rarity == null) return false;
            var cp = PlayerProfile.GetOrCreateCard(card.CardId);
            int level = Mathf.Max(1, cp.level);
            if (level >= card.Rarity.MaxLevel) return false;
            nextLevel = level + 1;
            pointsRequired = card.Rarity.GetPointsRequiredForLevel(nextLevel);
            cost = card.Rarity.GetUpgradeCostForLevel(nextLevel);
            return cp.points >= pointsRequired;
        }

        
        public static bool TryUpgradeById(string cardId)
        {
            var card = GameDB.GetCardById(cardId);
            if (card == null) return false;
            var progress = PlayerProfile.GetOrCreateCard(cardId);
            bool res = TryAutoUpgrade(card, progress);
            if (res) PlayerProfile.Save();
            return res;
        }
    }
}
