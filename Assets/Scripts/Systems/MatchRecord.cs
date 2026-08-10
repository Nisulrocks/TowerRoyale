namespace TR.Systems
{
    public enum MatchOutcome
    {
        Victory = 0,
        Defeat = 1,
        // The player left a match that had not resolved yet.
        Abandoned = 2
    }

    public enum MatchMode
    {
        Single = 0,
        Duo = 1
    }

    /// One line in the battle log. Kept deliberately small: it lives inside the signed profile
    /// blob that syncs to Firestore on every save, so every field here costs bandwidth on each
    /// write for as long as it stays in the ring buffer.
    [System.Serializable]
    public class MatchRecord
    {
        public long unixTime;

        // Stored as ints rather than enums so a future reorder of the enum cannot silently
        // reinterpret already-saved history.
        public int outcome;
        public int mode;

        public string arenaName;
        // Duo only; empty for single player.
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
