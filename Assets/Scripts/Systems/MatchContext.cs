namespace TR.Systems
{
    
    public enum GameMode
    {
        Single,
        Duo
    }

    
    public static class MatchContext
    {
        private static GameMode _mode = GameMode.Single;

        
        public static GameMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        public static bool IsDuo => _mode == GameMode.Duo;

        public static bool IsMatchInProgress
        {
            get
            {
                if (TR.Battle.BattleSceneController.Instance != null) return true;

                var duo = TR.Net.DuoNetworkManager.Instance;
                return duo != null && duo.IsEnteringMatch;
            }
        }

        
        public static string ArenaId { get; set; }

        
        public static void Reset()
        {
            _mode = GameMode.Single;
            ArenaId = null;
        }
    }
}
