using UnityEngine;
using TR.Systems;

namespace TR.Battle
{
    
    public class CardLimitBinding : MonoBehaviour
    {
        private string _cardId;
        public void SetCardId(string cardId)
        {
            _cardId = cardId;
        }

        private void OnDestroy()
        {
            if (!string.IsNullOrEmpty(_cardId))
            {
                EffectLimitService.UnregisterCard(_cardId);
                _cardId = null;
            }
        }
    }
}
