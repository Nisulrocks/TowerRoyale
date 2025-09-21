using UnityEngine;

namespace TR.Data
{
    [CreateAssetMenu(fileName = "CastleProgression", menuName = "TR/Data/Castle Progression")] 
    public class CastleProgression : ScriptableObject
    {
        [Header("Levels")] 
        [Min(1)] [SerializeField] private int maxLevel = 10;
        [Tooltip("XP needed to go from level L to L+1. Index 1 => XP for going 1->2, etc.")]
        [SerializeField] private int[] xpPerLevel; // size >= maxLevel, index by level
        [Tooltip("Max health at each level. Index 1 => health for level 1.")]
        [SerializeField] private int[] healthPerLevel; // size >= maxLevel, index by level

        public int MaxLevel => Mathf.Max(1, maxLevel);

        public int GetXPForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            if (xpPerLevel == null || xpPerLevel.Length <= level) return 100; // default
            return Mathf.Max(0, xpPerLevel[level]);
        }

        public int GetHealthForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            if (healthPerLevel == null || healthPerLevel.Length <= level) return 100; // default
            return Mathf.Max(1, healthPerLevel[level]);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (xpPerLevel == null || xpPerLevel.Length < MaxLevel + 1)
            {
                var old = xpPerLevel;
                xpPerLevel = new int[MaxLevel + 1];
                if (old != null) System.Array.Copy(old, xpPerLevel, Mathf.Min(old.Length, xpPerLevel.Length));
            }
            if (healthPerLevel == null || healthPerLevel.Length < MaxLevel + 1)
            {
                var old = healthPerLevel;
                healthPerLevel = new int[MaxLevel + 1];
                if (old != null) System.Array.Copy(old, healthPerLevel, Mathf.Min(old.Length, healthPerLevel.Length));
            }
            // Ensure index 1 has reasonable defaults if unset
            if (healthPerLevel[1] <= 0) healthPerLevel[1] = 100;
            if (xpPerLevel[1] <= 0) xpPerLevel[1] = 100;
        }
#endif
    }
}
