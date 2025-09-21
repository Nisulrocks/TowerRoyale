using UnityEngine;

namespace TR.VFX
{
    // Attach this to any GameObject that has ParticleSystem(s) (including child hierarchies)
    // to make them respond to Settings -> VFX Enable/Quality changes at runtime.
    // - When VFX is turned Off (quality = 0): stops and clears all bound particle systems
    // - When VFX is turned On (>0): starts them again if the object is active in hierarchy
    // Works for idle/looping effects placed before or after the toggle changes.
    [DisallowMultipleComponent]
    public class ParticleQualityBinder : MonoBehaviour
    {
        [Header("Binding Scope")]
        [Tooltip("If true, searches for ParticleSystem components in this object AND all children.")]
        [SerializeField] private bool includeChildren = true;

        [Tooltip("If true, when VFX is enabled this binder will Play() the systems if the object is active.")]
        [SerializeField] private bool autoPlayWhenEnabled = true;

        private ParticleSystem[] _systems;

        private void Awake()
        {
            CacheSystems();
        }

        // Public API for external callers (e.g., ParticleManager) to force re-applying current quality state
        public void Refresh()
        {
            if (_systems == null || _systems.Length == 0) CacheSystems();
            OnQualityChanged(ParticleQuality.Current);
        }

        private void OnEnable()
        {
            if (_systems == null || _systems.Length == 0) CacheSystems();
            ParticleQuality.OnChanged += OnQualityChanged;
            // Apply immediately to reflect current setting
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
                // Stop and clear all bound systems
                for (int i = 0; i < _systems.Length; i++)
                {
                    var ps = _systems[i]; if (ps == null) continue;
                    // Disable emission to ensure no stray spawn
                    var em = ps.emission; em.enabled = false;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            else
            {
                // Optionally play when enabled (covers systems created while VFX was off)
                if (!autoPlayWhenEnabled) return;
                if (!gameObject.activeInHierarchy) return;
                for (int i = 0; i < _systems.Length; i++)
                {
                    var ps = _systems[i]; if (ps == null) continue;
                    // Ensure emission is enabled
                    var em = ps.emission; em.enabled = true;
                    // Re-enable GameObject if authoring disabled it for perf
                    if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);
                    // Play regardless; calling Play on already playing systems is safe
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
            // Refresh cached list in editor when toggles change
            CacheSystems();
        }
    }
}
