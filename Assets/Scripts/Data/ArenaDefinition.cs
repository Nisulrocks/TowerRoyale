using UnityEngine;

namespace TR.Data
{
    [CreateAssetMenu(fileName = "ArenaDefinition", menuName = "TR/Data/Arena Definition")]
    public class ArenaDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string arenaId; 
        [SerializeField] private string displayName = "Arena";
        [SerializeField] private Sprite arenaImage; 

        [Header("Progression")]
        [SerializeField] private int trophyRequirement = 0; 

        [Header("Waves")]
        [SerializeField] private int waveCount = 10;
        [SerializeField] private float waveInterval = 60f; 

        [Header("Enemy Mix Weights (0..1)")]
        [Tooltip("Wave progress (0..1) at which Medium enemies start to be considered.")]
        [Range(0f, 1f)] [SerializeField] private float mediumStartPercent = 0.25f;
        [Tooltip("Wave progress (0..1) at which Hard enemies start to be considered.")]
        [Range(0f, 1f)] [SerializeField] private float hardStartPercent = 0.6f;

        [Header("Enemies by Tier")] 
        [SerializeField] private EnemyDefinition[] easyEnemies;   
        [SerializeField] private EnemyDefinition[] mediumEnemies; 
        [SerializeField] private EnemyDefinition[] hardEnemies;   

        [System.Serializable]
        public struct WaveEnemyCountRange
        {
            [Min(0)] public int min;
            [Min(0)] public int max;
        }

        [Header("Per-Wave Enemy Counts (1-based)")]

        [SerializeField] private WaveEnemyCountRange[] waveEnemyCounts;

        [Header("Boss Settings")]

        [SerializeField] private EnemyDefinition bossEnemy;

        [SerializeField] private bool spawnBossOnSpecificWave = false;

        [Min(1)] [SerializeField] private int bossSpecificWave = 1;
        [Tooltip("If 'Spawn on Specific wave no.' is disabled, spawn a boss every X waves (e.g., 5 means waves 5,10,15...).")]
        [Min(1)] [SerializeField] private int bossEveryXWaves = 5;
        [Header("Boss Per-Spawn Scaling (Periodic Mode)")]
        [Tooltip("Health multiplier applied per boss spawn in periodic mode. Final = base * (mult^spawnIndex-1)")]
        [Min(0f)] [SerializeField] private float bossHealthMultPerSpawn = 1.0f;
        [Tooltip("Damage multiplier applied per boss spawn in periodic mode. Final = base * (mult^spawnIndex-1)")]
        [Min(0f)] [SerializeField] private float bossDamageMultPerSpawn = 1.0f;
        [Tooltip("Move speed multiplier applied per boss spawn in periodic mode. Final = base * (mult^spawnIndex-1)")]
        [Min(0f)] [SerializeField] private float bossSpeedMultPerSpawn = 1.0f;

        [Header("Kill Rewards (Money Range)")]
        [Min(0)] [SerializeField] private int easyKillMin = 5;
        [Min(0)] [SerializeField] private int easyKillMax = 10;
        [Min(0)] [SerializeField] private int mediumKillMin = 10;
        [Min(0)] [SerializeField] private int mediumKillMax = 20;
        [Min(0)] [SerializeField] private int hardKillMin = 20;
        [Min(0)] [SerializeField] private int hardKillMax = 40;
        [Min(0)] [SerializeField] private int bossKillMin = 50;
        [Min(0)] [SerializeField] private int bossKillMax = 100;

        [Header("Victory Rewards (Soft Currency)")]

        [Min(0)] [SerializeField] private int victoryMoneyMin = 100;
        [Min(0)] [SerializeField] private int victoryMoneyMax = 150;

        [Header("Victory Rewards (Castle XP)")]

        [Min(0)] [SerializeField] private int victoryXPMin = 50;
        [Min(0)] [SerializeField] private int victoryXPMax = 75;

        [Header("Defeat Rewards (Castle XP)")]

        [Min(0)] [SerializeField] private int defeatXPMin = 25;
        [Min(0)] [SerializeField] private int defeatXPMax = 40;

        [Header("Victory Rewards (Trophies)")]

        [Min(0)] [SerializeField] private int victoryTrophiesMin = 5;
        [Min(0)] [SerializeField] private int victoryTrophiesMax = 10;

        [Header("Battle UI Prefabs (optional)")]

        [SerializeField] private GameObject battleToastPrefab;

        [Header("Defeat Penalties (Trophies)")]

        [Min(0)] [SerializeField] private int defeatTrophiesMin = 0;
        [Min(0)] [SerializeField] private int defeatTrophiesMax = 3;

        [System.Serializable]
        public struct EffectLimit
        {
            public EffectType type;
            [Min(0)] public int maxCount;
        }

        [Header("Effect Limits (per battle)")]
        [Tooltip("Optional caps per effect type (e.g., Slow=2 means at most 2 towers that apply Slow). Leave empty for no caps.")]
        [SerializeField] private EffectLimit[] effectLimits;

        [System.Serializable]
        public struct CardLimit
        {
            public CardDefinition card;
            [Min(0)] public int maxCount;
        }

        [Header("Card Limits (per battle)")]
        [Tooltip("Optional caps per specific card (e.g., Only 1 Freeze Tower). Leave empty for no caps.")]
        [SerializeField] private CardLimit[] cardLimits;

        public string ArenaId => arenaId;
        public string DisplayName => displayName;
        public Sprite ArenaImage => arenaImage;
        public int TrophyRequirement => Mathf.Max(0, trophyRequirement);
        public int WaveCount => Mathf.Max(1, waveCount);
        public float WaveInterval => Mathf.Max(0.1f, waveInterval);
        public float MediumStartPercent => Mathf.Clamp01(mediumStartPercent);
        public float HardStartPercent => Mathf.Clamp01(hardStartPercent);
        public EnemyDefinition[] EasyEnemies => easyEnemies ?? System.Array.Empty<EnemyDefinition>();
        public EnemyDefinition[] MediumEnemies => mediumEnemies ?? System.Array.Empty<EnemyDefinition>();
        public EnemyDefinition[] HardEnemies => hardEnemies ?? System.Array.Empty<EnemyDefinition>();
        public int EasyKillMin => Mathf.Min(easyKillMin, easyKillMax);
        public int EasyKillMax => Mathf.Max(easyKillMin, easyKillMax);
        public int MediumKillMin => Mathf.Min(mediumKillMin, mediumKillMax);
        public int MediumKillMax => Mathf.Max(mediumKillMin, mediumKillMax);
        public int HardKillMin => Mathf.Min(hardKillMin, hardKillMax);
        public int HardKillMax => Mathf.Max(hardKillMin, hardKillMax);
        public int BossKillMin => Mathf.Min(bossKillMin, bossKillMax);
        public int BossKillMax => Mathf.Max(bossKillMin, bossKillMax);
        public int VictoryMoneyMin => Mathf.Min(victoryMoneyMin, victoryMoneyMax);
        public int VictoryMoneyMax => Mathf.Max(victoryMoneyMin, victoryMoneyMax);
        public int VictoryXPMin => Mathf.Min(victoryXPMin, victoryXPMax);
        public int VictoryXPMax => Mathf.Max(victoryXPMin, victoryXPMax);
        public int DefeatXPMin => Mathf.Min(defeatXPMin, defeatXPMax);
        public int DefeatXPMax => Mathf.Max(defeatXPMin, defeatXPMax);
        public int VictoryTrophiesMin => Mathf.Min(victoryTrophiesMin, victoryTrophiesMax);
        public int VictoryTrophiesMax => Mathf.Max(victoryTrophiesMin, victoryTrophiesMax);
        public int DefeatTrophiesMin => Mathf.Min(defeatTrophiesMin, defeatTrophiesMax);
        public int DefeatTrophiesMax => Mathf.Max(defeatTrophiesMin, defeatTrophiesMax);
        public GameObject BattleToastPrefab => battleToastPrefab;
        public EffectLimit[] EffectLimits => effectLimits ?? System.Array.Empty<EffectLimit>();
        public CardLimit[] CardLimits => cardLimits ?? System.Array.Empty<CardLimit>();
        public EnemyDefinition BossEnemy => bossEnemy;
        public bool SpawnBossOnSpecificWave => spawnBossOnSpecificWave;
        public int BossSpecificWave => Mathf.Clamp(bossSpecificWave, 1, WaveCount);
        public int BossEveryXWaves => Mathf.Max(1, bossEveryXWaves);
        public float BossHealthMultPerSpawn => Mathf.Max(0f, bossHealthMultPerSpawn);
        public float BossDamageMultPerSpawn => Mathf.Max(0f, bossDamageMultPerSpawn);
        public float BossSpeedMultPerSpawn => Mathf.Max(0f, bossSpeedMultPerSpawn);
        
        public EnemyDefinition[] Enemies
        {
            get
            {
                int e = EasyEnemies.Length, m = MediumEnemies.Length, h = HardEnemies.Length;
                var all = new EnemyDefinition[e + m + h];
                int idx = 0;
                for (int i = 0; i < e; i++) all[idx++] = EasyEnemies[i];
                for (int i = 0; i < m; i++) all[idx++] = MediumEnemies[i];
                for (int i = 0; i < h; i++) all[idx++] = HardEnemies[i];
                return all;
            }
        }

        
        public void GetEnemyCountRangeForWave(int waveNumber, out int min, out int max)
        {
            int total = Mathf.Max(1, WaveCount);
            int idx = Mathf.Clamp(waveNumber, 1, total) - 1;
            if (waveEnemyCounts != null && idx < waveEnemyCounts.Length)
            {
                min = Mathf.Max(0, waveEnemyCounts[idx].min);
                max = Mathf.Max(min, waveEnemyCounts[idx].max);
                if (max == 0 && min == 0)
                {
                    
                    DefaultCountFallback(waveNumber, out min, out max);
                }
                return;
            }
            DefaultCountFallback(waveNumber, out min, out max);
        }

        private void DefaultCountFallback(int waveNumber, out int min, out int max)
        {
            
            int legacy = Mathf.Clamp(2 + Mathf.Max(1, waveNumber), 1, 20);
            min = legacy;
            max = legacy;
        }
        public enum EnemyTier { Unknown, Easy, Medium, Hard, Boss }

        public EnemyTier GetTier(EnemyDefinition def)
        {
            if (def == null) return EnemyTier.Unknown;
            if (bossEnemy != null && def == bossEnemy) return EnemyTier.Boss;
            foreach (var d in EasyEnemies) if (d == def) return EnemyTier.Easy;
            foreach (var d in MediumEnemies) if (d == def) return EnemyTier.Medium;
            foreach (var d in HardEnemies) if (d == def) return EnemyTier.Hard;
            return EnemyTier.Unknown;
        }

        
        public bool ShouldSpawnBoss(int waveNumber, out string warning)
        {
            warning = null;
            if (bossEnemy == null) return false;
            int total = Mathf.Max(1, WaveCount);
            int w = Mathf.Clamp(waveNumber, 1, total);
            if (spawnBossOnSpecificWave)
            {
                int target = Mathf.Clamp(bossSpecificWave, 1, total);
                return w == target;
            }
            
            int x = bossEveryXWaves;
            if (x <= 0)
            {
                warning = "Boss 'every X waves' is set to <= 0; boss will not spawn.";
                return false;
            }
            if (x > total)
            {
                warning = $"Boss 'every {x} waves' exceeds total waves ({total}); boss will not spawn.";
                return false;
            }
            return (w % x) == 0;
        }

        
        public void GetBossScalingForWave(int waveNumber, out float healthMult, out float damageMult, out float speedMult)
        {
            healthMult = 1f; damageMult = 1f; speedMult = 1f;
            if (bossEnemy == null) return;
            int total = Mathf.Max(1, WaveCount);
            int w = Mathf.Clamp(waveNumber, 1, total);
            if (spawnBossOnSpecificWave)
            {
                
                return;
            }
            int x = Mathf.Max(1, bossEveryXWaves);
            if (x <= 0) return;
            
            int occurrence = Mathf.Max(1, w / x);
            int power = Mathf.Max(0, occurrence - 1);
            float h = Mathf.Max(0f, bossHealthMultPerSpawn);
            float d = Mathf.Max(0f, bossDamageMultPerSpawn);
            float s = Mathf.Max(0f, bossSpeedMultPerSpawn);
            
            if (h <= 1e-4f) h = 1f;
            if (d <= 1e-4f) d = 1f;
            if (s <= 1e-4f) s = 1f;
            healthMult = Mathf.Pow(h, power);
            damageMult = Mathf.Pow(d, power);
            speedMult = Mathf.Pow(s, power);
        }

#if UNITY_EDITOR
        
        private void OnValidate()
        {
            int total = Mathf.Max(1, waveCount);
            if (waveEnemyCounts == null)
            {
                waveEnemyCounts = new WaveEnemyCountRange[total];
                for (int i = 0; i < total; i++)
                {
                    DefaultCountFallback(i + 1, out int defMin, out int defMax);
                    waveEnemyCounts[i].min = defMin;
                    waveEnemyCounts[i].max = defMax;
                }
                return;
            }

            if (waveEnemyCounts.Length != total)
            {
                var old = waveEnemyCounts;
                var resized = new WaveEnemyCountRange[total];
                int copy = Mathf.Min(old.Length, resized.Length);
                for (int i = 0; i < copy; i++)
                {
                    int mn = Mathf.Max(0, old[i].min);
                    int mx = Mathf.Max(mn, old[i].max);
                    resized[i].min = mn;
                    resized[i].max = mx;
                }
                for (int i = copy; i < total; i++)
                {
                    DefaultCountFallback(i + 1, out int defMin, out int defMax);
                    resized[i].min = defMin;
                    resized[i].max = defMax;
                }
                waveEnemyCounts = resized;
            }
            else
            {
                
                for (int i = 0; i < waveEnemyCounts.Length; i++)
                {
                    int mn = Mathf.Max(0, waveEnemyCounts[i].min);
                    int mx = Mathf.Max(mn, waveEnemyCounts[i].max);
                    waveEnemyCounts[i].min = mn;
                    waveEnemyCounts[i].max = mx;
                }
            }
        }
#endif
    }
}
