using UnityEngine;

namespace TR.Data
{
    
    [CreateAssetMenu(menuName = "TR/Cards/Pulse Card", fileName = "PulseCardDefinition")]
    public class PulseCardDefinition : CardDefinition
    {
        [Header("Pulse Settings")] 

        [SerializeField] private float pulseIntervalBase = 1.25f; [SerializeField] private float pulseIntervalPerLevel = 0f;

        [SerializeField] private float pulseRadiusBase = 3.5f; [SerializeField] private float pulseRadiusPerLevel = 0f;

        [SerializeField] private float pulseDamageBase = 20f; [SerializeField] private float pulseDamagePerLevel = 0f;
        [Tooltip("If true, on-hit effects (burn/poison/slow/stun) are applied on pulse hits as well")] 
        [SerializeField] private bool pulseAppliesOnHitEffects = true;

        [Header("Pulse VFX/SFX")] 

        [SerializeField] private string shockwaveVfxKey = "";

        [SerializeField] private string sfxPulseKey = "";

        [SerializeField] private float rippleDuration = 0.25f;
        [Tooltip("Fallback ripple line thickness (world units)")]
        [SerializeField] private float rippleLineWidth = 0.05f;

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
