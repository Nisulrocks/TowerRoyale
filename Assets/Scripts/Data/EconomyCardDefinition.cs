using UnityEngine;

namespace TR.Data
{
    [CreateAssetMenu(fileName = "EconomyCardDefinition", menuName = "TR/Data/Economy Card Definition")]
    public class EconomyCardDefinition : CardDefinition
    {
        [Header("Economy Stats Formula (value = base + perLevel*(level-1))")]
        [SerializeField] private float incomePerSecBase = 3f;     [SerializeField] private float incomePerSecPerLevel = 0.6f;
        [SerializeField] private float maxHealthBase = 50f;        [SerializeField] private float maxHealthPerLevel = 15f;
        [SerializeField] private float decayPerSecBase = 5f;       [SerializeField] private float decayPerSecPerLevel = 0.5f;

        public float GetIncomePerSecond(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Clamp(incomePerSecBase + incomePerSecPerLevel * (lv - 1), 0f, float.MaxValue);
        }
        public float GetMaxHealth(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Clamp(maxHealthBase + maxHealthPerLevel * (lv - 1), 1f, float.MaxValue);
        }
        public float GetDecayPerSecond(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Clamp(decayPerSecBase + decayPerSecPerLevel * (lv - 1), 0f, float.MaxValue);
        }

        
        public override TowerStats GetStatsForLevel(int level)
        {
            
            var baseStats = base.GetStatsForLevel(level);
            baseStats.dps = 0f;
            baseStats.fireRate = 0f;
            baseStats.range = 0f;
            baseStats.splashRadius = 0f;
            return baseStats;
        }

        public override float GetBurnDps(int level) => 0f;
        public override float GetBurnDuration(int level) => 0f;
        public override float GetPoisonDps(int level) => 0f;
        public override float GetPoisonDuration(int level) => 0f;
    }
}
