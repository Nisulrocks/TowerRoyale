using System.Collections.Generic;
using UnityEngine;

namespace TR.Systems
{
    public static class BattleLogService
    {
        public const int MaxEntries = 40;

        public static event System.Action OnLogChanged;

        public static IReadOnlyList<MatchRecord> Entries
        {
            get
            {
                var data = PlayerProfile.Data;
                if (data.matchLog == null) data.matchLog = new List<MatchRecord>();
                return data.matchLog;
            }
        }

        public static void Record(
            MatchOutcome outcome,
            MatchMode mode,
            string arenaName,
            string partnerName,
            int trophyDelta,
            int wavesCleared,
            int totalWaves)
        {
            var data = PlayerProfile.Data;
            if (data == null) return;
            if (data.matchLog == null) data.matchLog = new List<MatchRecord>();

            var record = new MatchRecord
            {
                unixTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                outcome = (int)outcome,
                mode = (int)mode,
                arenaName = arenaName ?? string.Empty,
                partnerName = partnerName ?? string.Empty,
                trophyDelta = trophyDelta,
                wavesCleared = Mathf.Max(0, wavesCleared),
                totalWaves = Mathf.Max(0, totalWaves)
            };

            data.matchLog.Insert(0, record);
            if (data.matchLog.Count > MaxEntries)
                data.matchLog.RemoveRange(MaxEntries, data.matchLog.Count - MaxEntries);

            ApplyCounters(outcome);

            PlayerProfile.Save();
            OnLogChanged?.Invoke();

            Debug.Log($"[BattleLog] {outcome} ({mode}) in {record.arenaName}, " +
                      $"trophies {trophyDelta:+#;-#;0}, waves {record.wavesCleared}/{record.totalWaves}.");
        }

        private static void ApplyCounters(MatchOutcome outcome)
        {
            var data = PlayerProfile.Data;
            data.lifetimeMatches++;

            switch (outcome)
            {
                case MatchOutcome.Victory:
                    data.lifetimeWins++;
                    data.currentWinStreak++;
                    if (data.currentWinStreak > data.longestWinStreak)
                        data.longestWinStreak = data.currentWinStreak;
                    break;

                case MatchOutcome.Defeat:
                    data.lifetimeLosses++;
                    data.currentWinStreak = 0;
                    break;

                default:
                    data.lifetimeAbandons++;
                    data.currentWinStreak = 0;
                    break;
            }

            int trophies = PlayerProfile.GetTrophies();
            if (trophies > data.bestTrophies) data.bestTrophies = trophies;
        }


        public static int TotalMatches => PlayerProfile.Data.lifetimeMatches;
        public static int Wins => PlayerProfile.Data.lifetimeWins;
        public static int Losses => PlayerProfile.Data.lifetimeLosses;
        public static int Abandons => PlayerProfile.Data.lifetimeAbandons;
        public static int CurrentWinStreak => PlayerProfile.Data.currentWinStreak;
        public static int LongestWinStreak => PlayerProfile.Data.longestWinStreak;

        public static int BestTrophies
        {
            get
            {
                var data = PlayerProfile.Data;
                int current = PlayerProfile.GetTrophies();
                if (data.bestTrophies < current) data.bestTrophies = current;
                return data.bestTrophies;
            }
        }

        public static float WinRate
        {
            get
            {
                int total = TotalMatches;
                return total <= 0 ? 0f : Wins / (float)total;
            }
        }
    }
}
