using UnityEngine;

namespace TR.VFX
{
    // Helper that notifies ParticleManager when a particle finishes playing
    [RequireComponent(typeof(ParticleSystem))]
    public class PooledParticle : MonoBehaviour
    {
        private ParticleSystem _ps;
        private ParticleManager _manager;
        private string _key;

        public void Bind(ParticleManager mgr, string key, ParticleSystem ps)
        {
            _manager = mgr;
            _key = key;
            _ps = ps;
        }

        private void Awake()
        {
            if (_ps == null) _ps = GetComponent<ParticleSystem>();
        }

        private void OnEnable()
        {
            // Ensure it will stop naturally
            if (_ps != null && _ps.isStopped) _ps.Play(true);
        }

        private void LateUpdate()
        {
            if (_ps == null) return;
            if (!_ps.IsAlive(true))
            {
                // Return to pool
                if (_manager != null && !string.IsNullOrEmpty(_key))
                {
                    _manager.ReturnToPool(_key, _ps);
                }
            }
        }

        // Allows code to explicitly return a looping particle to the pool
        public void ForceReturn()
        {
            if (_ps != null)
            {
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (_manager != null && !string.IsNullOrEmpty(_key) && _ps != null)
            {
                _manager.ReturnToPool(_key, _ps);
            }
        }
    }
}
