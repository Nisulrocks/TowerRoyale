using UnityEngine;

namespace TR.Data
{
    [CreateAssetMenu(fileName = "BuffCardDefinition", menuName = "TR/Data/Buff Card Definition")]
    public class BuffCardDefinition : CardDefinition
    {
        [Header("Buff Aura (base + perLevel*(level-1))")]
        [SerializeField] private float buffRangeBase = 3f;         [SerializeField] private float buffRangePerLevel = 0.25f;

        [Header("Buff Toggles")] 
        [SerializeField] private bool buffDps = true;
        [SerializeField] private bool buffFireRate = false;
        [SerializeField] private bool buffRange = false;
        [SerializeField] private bool buffSplash = false;

        [Header("Affects Only These Rarities (optional)")]
        [Tooltip("If empty, buffs affect ALL rarities. If set, only towers with a rarity in this list are affected.")]
        [SerializeField] private RarityDefinition[] allowedRarities;

        [Header("Buff Amounts (as fraction, e.g., 0.2 = +20%)")]
        [SerializeField] private float dpsPercentBase = 0.0f;      [SerializeField] private float dpsPercentPerLevel = 0.0f;
        [SerializeField] private float fireRatePercentBase = 0.0f;  [SerializeField] private float fireRatePercentPerLevel = 0.0f;
        [SerializeField] private float rangePercentBase = 0.0f;     [SerializeField] private float rangePercentPerLevel = 0.0f;
        [SerializeField] private float splashPercentBase = 0.0f;    [SerializeField] private float splashPercentPerLevel = 0.0f;

        [Header("On-Hit Effect Buffs (as fraction, e.g., 0.25 = +25%)")]
        [SerializeField] private bool buffBurn = false;
        [SerializeField] private float burnDpsPercentBase = 0.0f;    [SerializeField] private float burnDpsPercentPerLevel = 0.0f;
        [SerializeField] private float burnDurPercentBase = 0.0f;    [SerializeField] private float burnDurPercentPerLevel = 0.0f;

        [SerializeField] private bool buffPoison = false;
        [SerializeField] private float poisonDpsPercentBase = 0.0f;  [SerializeField] private float poisonDpsPercentPerLevel = 0.0f;
        [SerializeField] private float poisonDurPercentBase = 0.0f;  [SerializeField] private float poisonDurPercentPerLevel = 0.0f;

        [SerializeField] private bool buffSlow = false;
        [SerializeField] private float slowPercentBuffBase = 0.0f;   [SerializeField] private float slowPercentBuffPerLevel = 0.0f;
        [SerializeField] private float slowDurPercentBase = 0.0f;    [SerializeField] private float slowDurPercentPerLevel = 0.0f;
        
        [SerializeField] private bool buffStun = false;
        [Tooltip("Percent bonus applied multiplicatively to stun chance, e.g., 0.25 = +25% more chance (capped at 100%)")] 
        [SerializeField] private float stunChancePercentBase = 0.0f;  [SerializeField] private float stunChancePercentPerLevel = 0.0f;
        [Tooltip("Percent bonus applied multiplicatively to stun duration, e.g., 0.25 = +25% duration")] 
        [SerializeField] private float stunDurPercentBase = 0.0f;     [SerializeField] private float stunDurPercentPerLevel = 0.0f;

        [Header("Economy Buffs")]
        [SerializeField] private bool buffEconomyIncome = false;
        [Tooltip("Percent increase to EconomyTower income per second (e.g., 0.25 = +25%)")] 
        [SerializeField] private float economyIncomePercentBase = 0.0f; [SerializeField] private float economyIncomePercentPerLevel = 0.0f;

        [Header("Lifetime (base + perLevel*(level-1))")]
        [Tooltip("Maximum HP for the buff tower (decays over time)")]
        [SerializeField] private float maxHealthBase = 10f;          [SerializeField] private float maxHealthPerLevel = 0f;
        [Tooltip("HP decay per second (how fast it expires)")]
        [SerializeField] private float decayPerSecBase = 1f;         [SerializeField] private float decayPerSecPerLevel = 0f;

        public float GetBuffRange(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, buffRangeBase + buffRangePerLevel * (lv - 1));
        }

        public bool BuffDps => buffDps;
        public bool BuffFireRate => buffFireRate;
        public bool BuffRange => buffRange;
        public bool BuffSplash => buffSplash;
        public System.Collections.Generic.IReadOnlyList<RarityDefinition> AllowedRarities => allowedRarities;
        public bool BuffBurn => buffBurn;
        public bool BuffPoison => buffPoison;
        public bool BuffSlow => buffSlow;
        public bool BuffStun => buffStun;
        public bool BuffEconomyIncome => buffEconomyIncome;

        public float GetDpsPercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Clamp01(dpsPercentBase + dpsPercentPerLevel * (lv - 1));
        }
        public float GetFireRatePercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Clamp01(fireRatePercentBase + fireRatePercentPerLevel * (lv - 1));
        }
        public float GetRangePercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Clamp01(rangePercentBase + rangePercentPerLevel * (lv - 1));
        }
        public float GetSplashPercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Clamp01(splashPercentBase + splashPercentPerLevel * (lv - 1));
        }

        public float GetMaxHealth(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, maxHealthBase + maxHealthPerLevel * (lv - 1));
        }
        public float GetDecayPerSecond(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, decayPerSecBase + decayPerSecPerLevel * (lv - 1));
        }

        // On-hit effect buff getters (return fraction to add, e.g., 0.2 => +20%)
        public float GetBurnDpsBuffPercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, burnDpsPercentBase + burnDpsPercentPerLevel * (lv - 1));
        }
        public float GetBurnDurBuffPercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, burnDurPercentBase + burnDurPercentPerLevel * (lv - 1));
        }
        public float GetPoisonDpsBuffPercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, poisonDpsPercentBase + poisonDpsPercentPerLevel * (lv - 1));
        }
        public float GetPoisonDurBuffPercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, poisonDurPercentBase + poisonDurPercentPerLevel * (lv - 1));
        }
        public float GetSlowPercentBuffPercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, slowPercentBuffBase + slowPercentBuffPerLevel * (lv - 1));
        }
        public float GetSlowDurBuffPercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, slowDurPercentBase + slowDurPercentPerLevel * (lv - 1));
        }

        public float GetStunChanceBuffPercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, stunChancePercentBase + stunChancePercentPerLevel * (lv - 1));
        }
        public float GetStunDurBuffPercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, stunDurPercentBase + stunDurPercentPerLevel * (lv - 1));
        }

        public float GetEconomyIncomePercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, economyIncomePercentBase + economyIncomePercentPerLevel * (lv - 1));
        }

        // Returns true if the given card should be affected by this buff based on rarity filters
        public bool ShouldAffect(CardDefinition target)
        {
            if (target == null) return false;
            if (allowedRarities == null || allowedRarities.Length == 0) return true; // no filter => all
            var r = target.Rarity;
            if (r == null) return false;
            for (int i = 0; i < allowedRarities.Length; i++)
            {
                var ar = allowedRarities[i];
                if (ar == null) continue;
                if (ReferenceEquals(ar, r)) return true;
                if (!string.IsNullOrEmpty(ar.RarityId) && !string.IsNullOrEmpty(r.RarityId) && ar.RarityId == r.RarityId) return true;
            }
            return false;
        }
    }
}
