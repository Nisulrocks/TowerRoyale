using System.Linq;
using UnityEngine;
using TR.Data;

namespace TR.Systems
{
    
    public static class ArenaService
    {
        [System.Serializable]
        public struct MatchRewards
        {
            public int trophiesEarned;
            public int moneyEarned;
            public int castleXPEarned;
            public bool victory;
            public int totalTrophiesAfter;
            public ArenaDefinition arenaBefore;
            public ArenaDefinition arenaAfter;
            
            public bool trophiesCapped;
        }

        [Header("Reward Config")] 
        public static int MinTrophiesPerMatch = 5;
        public static int MaxTrophiesPerMatch = 10;
        public static int BaseMoneyPerMatch = 100;     
        public static int MoneyPerWave = 0;            
        public static int CastleXPOnVictory = 50;      
        public static int CastleXPPerWave = 5;         
        public static int CastleXPOnDefeat = 25;       

        
        public static ArenaDefinition GetCurrentArena()
        {
            GameDB.EnsureLoaded();
            int trophies = PlayerProfile.GetTrophies();
            return GetArenaForTrophies(trophies);
        }

        public static ArenaDefinition GetArenaForTrophies(int trophies)
        {
            GameDB.EnsureLoaded();
            var arenas = GameDB.GetArenasSortedByRequirement();
            if (arenas == null || arenas.Count == 0) return null;
            ArenaDefinition current = arenas[0];
            foreach (var a in arenas)
            {
                if (trophies >= a.TrophyRequirement) current = a; else break;
            }
            return current;
        }

        public static ArenaDefinition GetNextArena()
        {
            GameDB.EnsureLoaded();
            int trophies = PlayerProfile.GetTrophies();
            var arenas = GameDB.GetArenasSortedByRequirement();
            if (arenas == null || arenas.Count == 0) return null;
            foreach (var a in arenas)
            {
                if (trophies < a.TrophyRequirement) return a;
            }
            return null; 
        }

        
        
        public static MatchRewards AwardMatchCompletion(ArenaDefinition arena, int wavesCleared)
        {
            GameDB.EnsureLoaded();
            var beforeArena = GetCurrentArena();
            int trophiesBefore = PlayerProfile.GetTrophies();

            int trophiesGain;
            if (arena != null && arena.VictoryTrophiesMax > 0)
            {
                int tmin = Mathf.Max(0, arena.VictoryTrophiesMin);
                int tmax = Mathf.Max(tmin, arena.VictoryTrophiesMax);
                trophiesGain = Random.Range(tmin, tmax + 1);
            }
            else
            {
                trophiesGain = Random.Range(MinTrophiesPerMatch, MaxTrophiesPerMatch + 1);
            }
            int moneyGain;
            if (arena != null && arena.VictoryMoneyMax > 0)
            {
                int vmin = Mathf.Max(0, arena.VictoryMoneyMin);
                int vmax = Mathf.Max(vmin, arena.VictoryMoneyMax);
                moneyGain = Random.Range(vmin, vmax + 1);
            }
            else
            {
                moneyGain = BaseMoneyPerMatch + Mathf.Max(0, wavesCleared) * Mathf.Max(0, MoneyPerWave);
            }
            int castleXPGain;
            if (arena != null && arena.VictoryXPMax > 0)
            {
                int xmin = Mathf.Max(0, arena.VictoryXPMin);
                int xmax = Mathf.Max(xmin, arena.VictoryXPMax);
                castleXPGain = Random.Range(xmin, xmax + 1);
            }
            else
            {
                castleXPGain = Mathf.Max(0, CastleXPOnVictory + Mathf.Max(0, wavesCleared) * Mathf.Max(0, CastleXPPerWave));
            }

            
            PlayerProfile.AddTrophies(trophiesGain);

            var afterArena = GetCurrentArena();
            int trophiesAfter = PlayerProfile.GetTrophies();
            int actualGain = trophiesAfter - trophiesBefore;
            bool capped = actualGain < Mathf.Max(0, trophiesGain);

            PlayerProfile.AddSoftCurrency(moneyGain);
            PlayerProfile.AddCastleXP(castleXPGain);

            
            if (afterArena != null && beforeArena != null && afterArena != beforeArena)
            {
                
                int req = Mathf.Max(0, afterArena.TrophyRequirement);
                TR.Systems.PlayerProfile.SetTrophyFloorAtLeast(req);
                
                TR.Systems.PlayerProfile.SetPendingArenaUnlock(afterArena.DisplayName);
            }


            return new MatchRewards
            {
                trophiesEarned = actualGain,
                moneyEarned = moneyGain,
                castleXPEarned = castleXPGain,
                victory = true,
                totalTrophiesAfter = PlayerProfile.GetTrophies(),
                arenaBefore = beforeArena,
                arenaAfter = afterArena,
                trophiesCapped = capped
            };
        }

        
        public static MatchRewards AwardMatchDefeat(ArenaDefinition arena, int wavesCleared)
        {
            GameDB.EnsureLoaded();
            var beforeArena = GetCurrentArena();
            int trophiesBefore = PlayerProfile.GetTrophies();
            int trophiesLoss = 0;
            if (arena != null && arena.DefeatTrophiesMax > 0)
            {
                int dmin = Mathf.Max(0, arena.DefeatTrophiesMin);
                int dmax = Mathf.Max(dmin, arena.DefeatTrophiesMax);
                trophiesLoss = Random.Range(dmin, dmax + 1);
            }
            
            if (trophiesLoss > 0) PlayerProfile.RemoveTrophies(trophiesLoss);
            int castleXPGain;
            if (arena != null && arena.DefeatXPMax > 0)
            {
                int xmin = Mathf.Max(0, arena.DefeatXPMin);
                int xmax = Mathf.Max(xmin, arena.DefeatXPMax);
                castleXPGain = Random.Range(xmin, xmax + 1);
            }
            else
            {
                castleXPGain = Mathf.Max(0, CastleXPOnDefeat + Mathf.Max(0, wavesCleared) * Mathf.Max(0, CastleXPPerWave));
            }
            PlayerProfile.AddCastleXP(castleXPGain);

            var afterArena = GetCurrentArena();
            int trophiesAfter = PlayerProfile.GetTrophies();
            int actualDelta = trophiesAfter - trophiesBefore; 
            bool capped = (trophiesLoss > 0) && (trophiesAfter == 0) && (trophiesBefore > 0);

            return new MatchRewards
            {
                trophiesEarned = actualDelta, 
                moneyEarned = 0,
                castleXPEarned = castleXPGain,
                victory = false,
                totalTrophiesAfter = PlayerProfile.GetTrophies(),
                arenaBefore = beforeArena,
                arenaAfter = afterArena,
                trophiesCapped = capped
            };
        }

        
        public static MatchRewards AwardMatchDefeat(int wavesCleared)
        {
            return AwardMatchDefeat(GetCurrentArena(), wavesCleared);
        }
    }
}
