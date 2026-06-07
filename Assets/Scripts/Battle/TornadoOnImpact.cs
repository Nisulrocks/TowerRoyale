using UnityEngine;

namespace TR.Battle
{
    
    public class TornadoOnImpact : MonoBehaviour
    {
        private float _radius, _strength, _duration;
        private int _maxTargets = 9999;
        private bool _allowEasy = true, _allowMedium = true, _allowHard = true, _allowBoss = true;
        private string _vfxKey = string.Empty;
        private float _vfxScaleMul = 1f;
        private bool _allowCenterStack = false;
        private float _falloffPower = 1f;

        public void Configure(float radius, float strength, float duration)
        {
            _radius = Mathf.Max(0f, radius);
            _strength = Mathf.Max(0f, strength);
            _duration = Mathf.Max(0f, duration);
        }

        public TornadoOnImpact SetFilters(int maxTargets, bool allowEasy, bool allowMedium, bool allowHard, bool allowBoss)
        {
            _maxTargets = Mathf.Max(0, maxTargets);
            _allowEasy = allowEasy; _allowMedium = allowMedium; _allowHard = allowHard; _allowBoss = allowBoss;
            return this;
        }

        public TornadoOnImpact SetVfx(string vfxKey, float vfxScaleMultiplier)
        {
            _vfxKey = vfxKey ?? string.Empty;
            _vfxScaleMul = Mathf.Max(0f, vfxScaleMultiplier);
            return this;
        }

        public TornadoOnImpact SetBehavior(bool allowCenterStack, float falloffPower)
        {
            _allowCenterStack = allowCenterStack;
            _falloffPower = Mathf.Clamp(falloffPower, 0.1f, 5f);
            return this;
        }

        private void OnDestroy()
        {
            
            if (_radius > 0f && _strength > 0f && _duration > 0f)
            {
                TornadoField.Spawn(transform.position, _radius, _strength, _duration,
                                   _maxTargets, _allowEasy, _allowMedium, _allowHard, _allowBoss,
                                   _vfxKey, _vfxScaleMul,
                                   _allowCenterStack, _falloffPower);
            }
        }
    }
}
