using System.Collections.Generic;
using UnityEngine;

namespace TR.Systems
{
    /// Records finished and abandoned matches, and derives the profile stats from them.
    ///
    /// Only clean leaves are logged as abandoned: a crash or a force-quit leaves no record at all,
    /// because nothing gets the chance to write one. Catching those would mean writing a record at
    /// match start and reconciling it on the next boot.
    public static class BattleLogService
    {
        /// The log lives inside the signed profile that uploads on every save, so it is a ring
        /// buffer rather than a full history. Lifetime counters carry the totals past this.
        public const int MaxEntries = 40;

        public static event System.Action OnLogChanged;

        /// Newest first.
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

        // ---------- derived stats ----------

        public static int TotalMatches => PlayerProfile.Data.lifetimeMatches;
        public static int Wins => PlayerProfile.Data.lifetimeWins;
        public static int Losses => PlayerProfile.Data.lifetimeLosses;
        public static int Abandons => PlayerProfile.Data.lifetimeAbandons;
        public static int CurrentWinStreak => PlayerProfile.Data.currentWinStreak;
        public static int LongestWinStreak => PlayerProfile.Data.longestWinStreak;

        /// Best trophies ever held. Back-fills from the current total for profiles that predate
        /// this being tracked, so an existing player does not see a best of zero.
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

        /// 0..1. Abandoned matches count against the total, the same as a loss.
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
