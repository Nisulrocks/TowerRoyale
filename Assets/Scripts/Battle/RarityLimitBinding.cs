using UnityEngine;
using TR.Systems;

namespace TR.Battle
{
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
