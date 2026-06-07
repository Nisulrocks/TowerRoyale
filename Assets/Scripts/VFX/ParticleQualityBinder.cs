using UnityEngine;

namespace TR.VFX
{
    
    
    
    
    
    [DisallowMultipleComponent]
    public class ParticleQualityBinder : MonoBehaviour
    {
        [Header("Binding Scope")]

        [SerializeField] private bool includeChildren = true;

        [Tooltip("If true, when VFX is enabled this binder will Play() the systems if the object is active.")]
        [SerializeField] private bool autoPlayWhenEnabled = true;

        private ParticleSystem[] _systems;

        private void Awake()
        {
            CacheSystems();
        }

        
        public void Refresh()
        {
            if (_systems == null || _systems.Length == 0) CacheSystems();
            OnQualityChanged(ParticleQuality.Current);
        }

        private void OnEnable()
        {
            if (_systems == null || _systems.Length == 0) CacheSystems();
            ParticleQuality.OnChanged += OnQualityChanged;
            
            OnQualityChanged(ParticleQuality.Current);
        }

        private void OnDisable()
        {
            ParticleQuality.OnChanged -= OnQualityChanged;
        }

        private void CacheSystems()
        {
            if (includeChildren)
                _systems = GetComponentsInChildren<ParticleSystem>(true);
            else
                _systems = GetComponents<ParticleSystem>();
        }

        private void OnQualityChanged(int quality)
        {
            if (_systems == null || _systems.Length == 0) return;
            if (quality <= 0)
            {
                
                for (int i = 0; i < _systems.Length; i++)
                {
                    var ps = _systems[i]; if (ps == null) continue;
                    
                    var em = ps.emission; em.enabled = false;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            else
            {
                
                if (!autoPlayWhenEnabled) return;
                if (!gameObject.activeInHierarchy) return;
                for (int i = 0; i < _systems.Length; i++)
                {
                    var ps = _systems[i]; if (ps == null) continue;
                    
                    var em = ps.emission; em.enabled = true;
                    
                    if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);
                    
                    ps.Play(true);
                }
            }
        }

        private void OnTransformChildrenChanged()
        {
            CacheSystems();
        }

        private void OnValidate()
        {
            
            CacheSystems();
        }
    }
}
