using UnityEngine;
using TR.Systems;

namespace TR.Data.Progression
{
    [CreateAssetMenu(fileName = "SoftCurrencyReward", menuName = "TR/Rewards/Soft Currency Reward")]
    public class SoftCurrencyReward : RewardDefinition
    {
        [Min(0)] public int amount = 0;
        [SerializeField] private Sprite icon;

        public override string GetDisplayName() => $"Coins x{amount}";
        public override Sprite GetIcon() => icon;

        public override void Grant(PlayerProfileDTO profile)
        {
            if (amount <= 0) return;
            PlayerProfile.AddSoftCurrency(amount);
        }
    }
}
