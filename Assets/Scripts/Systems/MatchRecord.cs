namespace TR.Systems
{
    public enum MatchOutcome
    {
        Victory = 0,
        Defeat = 1,
        Abandoned = 2
    }

    public enum MatchMode
    {
        Single = 0,
        Duo = 1
    }

    [System.Serializable]
    public class MatchRecord
    {
        public long unixTime;

        public int outcome;
        public int mode;

        public string arenaName;
        public string partnerName;

        public int trophyDelta;
        public int wavesCleared;
        public int totalWaves;

        public MatchOutcome Outcome => (MatchOutcome)outcome;
        public MatchMode Mode => (MatchMode)mode;

        public System.DateTime LocalTime =>
            System.DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime;
    }
}
