using UnityEngine;

namespace TR.Data.Progression
{
    [System.Serializable]
    public class TrophyMilestone
    {
        [Min(0)] public int trophyRequired = 0;
        [SerializeField] public RewardDefinition reward; 
        public bool oneTimeOnly = true;
    }
}
