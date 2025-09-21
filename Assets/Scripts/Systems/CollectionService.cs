using System.Collections.Generic;
using UnityEngine;
using TR.Data;

namespace TR.Systems
{
    // Handles awarding cards, duplicate conversions to points, and upgrading logic using rarity curves
    public static class CollectionService
    {
        public struct AwardResult
        {
            public CardDefinition card;
            public bool isNew;
            public int pointsAwarded; // 0 if new
            public int levelBefore;
            public int levelAfter;
        }

        // Award a list of cards (e.g., from a pack). Returns a summary string for quick debug/log.
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
                    // First copy unlocks the card at level 1
                    progress.level = 1;
                    progress.points = 0;
                    sb.AppendLine($"Unlocked {card.DisplayName} ({card.Rarity?.DisplayName})");
                }
                else
                {
                    // Duplicate -> convert to random points within rarity-defined range
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

        // Same as AwardCards, but returns per-card details for UI.
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

        // Attempts to upgrade repeatedly if enough points are available
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
                    // Award castle XP for this level upgrade
                    int castleXp = rarity.GetCastleXpForUpgradeLevel(nextLevel);
                    if (castleXp > 0) PlayerProfile.AddCastleXP(castleXp);
                    upgraded = true;
                }
                else break;
            }
            return upgraded;
        }

        // Paid single-level upgrade: requires enough points for next level and enough soft currency per rarity cost curve
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
            // Apply one level upgrade; surplus points carry to next
            progress.points -= ptsRequired;
            progress.level = nextLevel;
            // Award castle XP for this level upgrade
            int castleXp = rarity.GetCastleXpForUpgradeLevel(nextLevel);
            if (castleXp > 0) PlayerProfile.AddCastleXP(castleXp);
            PlayerProfile.Save();
            return true;
        }

        // Helper: returns if an upgrade is available now and the points/cost for next level
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

        // Public convenience upgrade by id (e.g., from UI button) if enough points
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
