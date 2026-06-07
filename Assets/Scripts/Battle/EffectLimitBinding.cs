using UnityEngine;
using System.Collections.Generic;
using TR.Systems;
using TR.Data;

namespace TR.Battle
{
    
    public class EffectLimitBinding : MonoBehaviour
    {
        private HashSet<EffectType> _types;
        public void SetTypes(HashSet<EffectType> types)
        {
            _types = types;
        }

        private void OnDestroy()
        {
            if (_types != null && _types.Count > 0)
            {
                EffectLimitService.Unregister(_types);
                _types.Clear();
            }
        }
    }
}
