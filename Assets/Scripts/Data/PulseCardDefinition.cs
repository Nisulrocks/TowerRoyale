using UnityEngine;

namespace TR.Data
{
    // Specialized card for a pulse tower that periodically damages all enemies in a radius
    [CreateAssetMenu(menuName = "TR/Cards/Pulse Card", fileName = "PulseCardDefinition")]
    public class PulseCardDefinition : CardDefinition
    {
        [Header("Pulse Settings")] 
        [Tooltip("Seconds between pulses at level 1 and per level increment")]
        [SerializeField] private float pulseIntervalBase = 1.25f; [SerializeField] private float pulseIntervalPerLevel = 0f;
        [Tooltip("Pulse radius at level 1 and per level increment")]
        [SerializeField] private float pulseRadiusBase = 3.5f; [SerializeField] private float pulseRadiusPerLevel = 0f;
        [Tooltip("Damage dealt per pulse at level 1 and per level increment")] 
        [SerializeField] private float pulseDamageBase = 20f; [SerializeField] private float pulseDamagePerLevel = 0f;
        [Tooltip("If true, on-hit effects (burn/poison/slow/stun) are applied on pulse hits as well")] 
        [SerializeField] private bool pulseAppliesOnHitEffects = true;

        [Header("Pulse VFX/SFX")] 
        [Tooltip("ParticleManager key for an expanding shockwave effect at the tower's position")] 
        [SerializeField] private string shockwaveVfxKey = "";
        [Tooltip("Sound key to play each time a pulse occurs")] 
        [SerializeField] private string sfxPulseKey = "";
        [Tooltip("Fallback ripple duration if no shockwave VFX key is configured")]
        [SerializeField] private float rippleDuration = 0.25f;
        [Tooltip("Fallback ripple line thickness (world units)")]
        [SerializeField] private float rippleLineWidth = 0.05f;
        [Tooltip("Fallback ripple color")]
        [SerializeField] private Color rippleColor = new Color(0.7f, 0.9f, 1f, 0.6f);
        [Tooltip("Fallback ripple circle smoothness (segments)")]
        [SerializeField] private int rippleSegments = 64;

        public float GetPulseInterval(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0.05f, pulseIntervalBase + pulseIntervalPerLevel * (lv - 1));
        }
        public float GetPulseRadius(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, pulseRadiusBase + pulseRadiusPerLevel * (lv - 1));
        }
        public float GetPulseDamage(int level)
        {
            int lv = Mathf.Clamp(level, 1, Rarity != null ? Rarity.MaxLevel : level);
            return Mathf.Max(0f, pulseDamageBase + pulseDamagePerLevel * (lv - 1));
        }
        public bool PulseAppliesOnHitEffects() => pulseAppliesOnHitEffects;
        public string GetShockwaveVfxKey() => shockwaveVfxKey;
        public string GetSfxPulseKey() => sfxPulseKey;
        public float GetRippleDuration() => Mathf.Max(0.01f, rippleDuration);
        public float GetRippleLineWidth() => Mathf.Max(0.001f, rippleLineWidth);
        public Color GetRippleColor() => rippleColor;
        public int GetRippleSegments() => Mathf.Clamp(rippleSegments, 12, 256);
    }
}
