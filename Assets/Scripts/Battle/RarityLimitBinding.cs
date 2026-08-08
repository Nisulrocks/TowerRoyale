using UnityEngine;
using TR.Systems;

namespace TR.Battle
{
    // Releases this tower's slot in its rarity cap when the tower is removed. Mirrors
    // CardLimitBinding, which does the same for per-card caps.
    public class RarityLimitBinding : MonoBehaviour
    {
        private string _rarityKey;

        public void SetRarityKey(string rarityKey)
        {
            _rarityKey = rarityKey;
        }

        private void OnDestroy()
        {
            if (!string.IsNullOrEmpty(_rarityKey))
            {
                EffectLimitService.UnregisterRarity(_rarityKey);
                _rarityKey = null;
            }
        }
    }
}
