using UnityEngine;

namespace TR.Data
{
    [CreateAssetMenu(fileName = "RarityDefinition", menuName = "TR/Data/Rarity Definition")]
    public class RarityDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string rarityId;     
        [SerializeField] private string displayName;
        [SerializeField] private Color color = Color.white;

        [Header("Progression")]
        [Min(1)] [SerializeField] private int maxLevel = 10;

        [Header("Points Required Formula (L-1 -> L)")]
        [Min(0)] [SerializeField] private int pointsBase = 2;
        [Min(0)] [SerializeField] private int pointsPerLevel = 3;

        [Header("Duplicate -> Points Range (per duplicate)")]

        [Min(0)] [SerializeField] private int dupPointsMin = 1;
        [Min(0)] [SerializeField] private int dupPointsMax = 1;

        [Header("Duplicate -> Soft Currency Range (when max level)")]

        [Min(0)] [SerializeField] private int dupSoftCurrencyMin = 0;
        [Min(0)] [SerializeField] private int dupSoftCurrencyMax = 0;

        [Header("Upgrade Cost Formula (Soft Currency)")]
        [Min(0)] [SerializeField] private int upgradeCostBase = 50;
        [Min(0)] [SerializeField] private int upgradeCostPerLevel = 75;

        [Header("Castle XP Awarded On Upgrade")]
        [Min(0)] [SerializeField] private int castleXpBase = 5;
        [Min(0)] [SerializeField] private int castleXpPerLevel = 5;

        [Header("Pack Reveal FX")]

        [SerializeField] private bool confettiOnReveal = false;

        
        public string RarityId => rarityId;
        public string DisplayName => displayName;
        public Color Color => color;
        public int MaxLevel => maxLevel;
        public bool ConfettiOnReveal => confettiOnReveal;

        [Header("Pack Reveal")]
        [Range(0, 2)] [SerializeField] private int revealTier = 0;
        public int RevealTier => revealTier;

        
        public int GetPointsRequiredForLevel(int targetLevel)
        {
            targetLevel = Mathf.Clamp(targetLevel, 1, maxLevel);
            if (targetLevel <= 1) return 0; 
            int lIndex = targetLevel - 2; 
            long val = (long)pointsBase + (long)pointsPerLevel * lIndex;
            return (int)Mathf.Max(0, (int)Mathf.Clamp(val, 0, int.MaxValue));
        }

        
        public void GetDuplicatePointsRange(int currentLevel, out int minPoints, out int maxPoints)
        {
            
            int mn = Mathf.Max(0, dupPointsMin);
            int mx = Mathf.Max(mn, dupPointsMax);
            minPoints = mn;
            maxPoints = mx;
        }

        
        public int RollDuplicatePoints(int currentLevel, System.Random rng = null)
        {
            GetDuplicatePointsRange(currentLevel, out int minP, out int maxP);
            if (maxP <= minP) return minP;
            rng ??= new System.Random();
            return rng.Next(minP, maxP + 1);
        }

        
        public void GetDuplicateSoftCurrencyRange(int currentLevel, out int minCurrency, out int maxCurrency)
        {
            int mn = Mathf.Max(0, dupSoftCurrencyMin);
            int mx = Mathf.Max(mn, dupSoftCurrencyMax);
            minCurrency = mn;
            maxCurrency = mx;
        }

        
        public int RollDuplicateSoftCurrency(int currentLevel, System.Random rng = null)
        {
            GetDuplicateSoftCurrencyRange(currentLevel, out int minC, out int maxC);
            if (maxC <= minC) return minC;
            rng ??= new System.Random();
            return rng.Next(minC, maxC + 1);
        }

        
        public int GetUpgradeCostForLevel(int targetLevel)
        {
            targetLevel = Mathf.Clamp(targetLevel, 1, maxLevel);
            if (targetLevel <= 1) return 0;
            int lIndex = targetLevel - 2; 
            long val = (long)upgradeCostBase + (long)upgradeCostPerLevel * lIndex;
            return (int)Mathf.Max(0, (int)Mathf.Clamp(val, 0, int.MaxValue));
        }

        
        public int GetCastleXpForUpgradeLevel(int targetLevel)
        {
            targetLevel = Mathf.Clamp(targetLevel, 1, maxLevel);
            if (targetLevel <= 1) return 0;
            int lIndex = targetLevel - 2; 
            long val = (long)castleXpBase + (long)castleXpPerLevel * lIndex;
            return (int)Mathf.Max(0, (int)Mathf.Clamp(val, 0, int.MaxValue));
        }
    }
}