using UnityEngine;

namespace TR.Data.Progression
{
    [System.Serializable]
    public class TrophyMilestone
    {
        [Min(0)] public int trophyRequired = 0;
        [SerializeField] public RewardDefinition reward; // assign a concrete reward (e.g., SoftCurrencyReward, RandomPackReward)
        public bool oneTimeOnly = true;
    }
}
