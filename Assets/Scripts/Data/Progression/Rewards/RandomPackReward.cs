using System.Collections.Generic;
using UnityEngine;
using TR.Systems;
using TR.Data;

namespace TR.Data.Progression
{
    [CreateAssetMenu(fileName = "RandomPackReward", menuName = "TR/Rewards/Random Pack Reward")] 
    public class RandomPackReward : RewardDefinition
    {
        [Tooltip("Candidate packs to choose from (one will be selected at random when claimed)")]
        [SerializeField] private List<PackDefinition> candidatePacks = new();
        [Min(1)] public int count = 1;
        [SerializeField] private Sprite icon;
        [SerializeField] private bool autoOpen = true; // if true, route to PackOpening scene immediately

        public override string GetDisplayName() => count > 1 ? $"Random Pack x{count}" : "Random Pack";
        public override Sprite GetIcon() => icon;

        public override void Grant(PlayerProfileDTO profile)
        {
            if (candidatePacks == null || candidatePacks.Count == 0 || count <= 0) return;
            System.Random rng = new System.Random();
            for (int i = 0; i < count; i++)
            {
                var pack = candidatePacks[rng.Next(0, candidatePacks.Count)];
                if (pack != null)
                {
                    if (autoOpen)
                    {
                        // Mirror ShopUI flow: set SceneParams and load PackOpening scene
                        TR.Systems.PackOpeningService.OpenPackScene(pack.PackId, 1);
                    }
                    else
                    {
                        profile.AddPacks(pack.PackId, 1);
                    }
                }
            }
            PlayerProfile.Save();
        }
    }
}
