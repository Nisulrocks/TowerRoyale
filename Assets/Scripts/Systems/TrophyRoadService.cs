using System.Collections.Generic;
using UnityEngine;
using TR.Data.Progression;

namespace TR.Systems
{
    public static class TrophyRoadService
    {
        public struct ClaimResult
        {
            public bool ok;
            public int milestoneIndex;
            public string message;
        }

        public static TrophyRoadDefinition GetActiveRoad()
        {
            return GameDB.GetTrophyRoad();
        }

        public static int GetMaxTrophies()
        {
            var road = GetActiveRoad();
            return road != null ? Mathf.Max(0, road.MaxTrophies) : int.MaxValue;
        }

        public static List<int> GetClaimableMilestoneIndices()
        {
            var list = new List<int>();
            var road = GetActiveRoad();
            if (road == null) return list;
            int trophies = PlayerProfile.GetTrophies();
            var milestones = road.Milestones;
            if (milestones == null) return list;
            for (int i = 0; i < milestones.Count; i++)
            {
                var ms = milestones[i];
                if (ms == null) continue;
                if (trophies >= Mathf.Max(0, ms.trophyRequired) && !PlayerProfile.IsTrophyMilestoneClaimed(i))
                {
                    list.Add(i);
                }
            }
            return list;
        }

        public static int GetNextMilestoneIndex()
        {
            var road = GetActiveRoad();
            if (road == null) return -1;
            int trophies = PlayerProfile.GetTrophies();
            var milestones = road.Milestones;
            if (milestones == null || milestones.Count == 0) return -1;
            for (int i = 0; i < milestones.Count; i++)
            {
                if (milestones[i] != null && trophies < milestones[i].trophyRequired)
                    return i;
            }
            return -1; 
        }

        public static float GetProgress01()
        {
            var road = GetActiveRoad();
            if (road == null) return 0f;
            int max = Mathf.Max(1, road.MaxTrophies);
            return Mathf.Clamp01((float)PlayerProfile.GetTrophies() / max);
        }

        public static ClaimResult Claim(int milestoneIndex)
        {
            var road = GetActiveRoad();
            if (road == null) return new ClaimResult { ok = false, milestoneIndex = milestoneIndex, message = "No Trophy Road configured" };
            var milestones = road.Milestones;
            if (milestones == null || milestoneIndex < 0 || milestoneIndex >= milestones.Count)
                return new ClaimResult { ok = false, milestoneIndex = milestoneIndex, message = "Invalid milestone" };

            var ms = milestones[milestoneIndex];
            if (ms == null) return new ClaimResult { ok = false, milestoneIndex = milestoneIndex, message = "Missing milestone" };
            int trophies = PlayerProfile.GetTrophies();
            if (trophies < Mathf.Max(0, ms.trophyRequired))
                return new ClaimResult { ok = false, milestoneIndex = milestoneIndex, message = "Not enough trophies" };
            if (PlayerProfile.IsTrophyMilestoneClaimed(milestoneIndex))
                return new ClaimResult { ok = false, milestoneIndex = milestoneIndex, message = "Already claimed" };

            
            try
            {
                ms.reward?.Grant(PlayerProfile.Data);
                PlayerProfile.MarkTrophyMilestoneClaimed(milestoneIndex);
                return new ClaimResult { ok = true, milestoneIndex = milestoneIndex, message = "Claimed" };
            }
            catch (System.SystemException ex)
            {
                Debug.LogError($"[TrophyRoadService] Claim error: {ex}");
                return new ClaimResult { ok = false, milestoneIndex = milestoneIndex, message = "Grant failed" };
            }
        }
    }
}
