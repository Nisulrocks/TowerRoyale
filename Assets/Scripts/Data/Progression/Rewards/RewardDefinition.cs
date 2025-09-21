using UnityEngine;
using TR.Systems;

namespace TR.Data.Progression
{
    public abstract class RewardDefinition : ScriptableObject
    {
        public abstract string GetDisplayName();
        public abstract Sprite GetIcon();
        public abstract void Grant(PlayerProfileDTO profile);
    }
}
