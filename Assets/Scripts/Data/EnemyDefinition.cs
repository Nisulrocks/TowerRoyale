using UnityEngine;

namespace TR.Data
{
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "TR/Data/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string enemyId; // unique key
        [SerializeField] private string displayName = "Enemy";

        [Header("Stats")]
        [Min(0f)] [SerializeField] private float movementSpeed = 1.5f; // units per second
        [Min(1f)] [SerializeField] private float maxHealth = 10f;
        [Min(0f)] [SerializeField] private float damagePerHit = 1f; // damage dealt to castle per attack

        [Header("Prefab")]
        [SerializeField] private GameObject prefab; // visual prefab to spawn in battle

        [Header("Abilities - Regeneration (fractions are per MaxHP)")]
        [Tooltip("Enable health regeneration over time with safeguards against being overpowered")] [SerializeField]
        private bool useRegenAbility = false;
        [Tooltip("Base regen per second, as a fraction of MaxHP (e.g., 0.03 = 3% MaxHP/sec)")] [SerializeField] [Min(0f)]
        private float regenPerSecondBase = 0.03f;
        [Tooltip("Additional regen at 100% missing health, scales linearly by missing fraction (0..1). As fraction of MaxHP/sec")] [SerializeField] [Min(0f)]
        private float regenMissingHealthFactor = 0.02f;
        [Tooltip("Seconds after taking any damage before regen can start")] [SerializeField] [Min(0f)]
        private float regenSuppressAfterDamageSeconds = 2.5f;
        [Tooltip("Maximum regen rate per second, as a fraction of MaxHP/sec")] [SerializeField] [Min(0f)]
        private float regenPerSecondCap = 0.05f;
        [Tooltip("Maximum total regen over this enemy's lifetime, as a fraction of MaxHP (0..1)")] [SerializeField] [Range(0f, 1f)]
        private float regenTotalPercentCap = 0.4f;
        [Tooltip("If false, active DoTs (burn/poison/frostbite) fully suppress regen; if true, regen is reduced by DotPenaltyMultiplier")]
        [SerializeField] private bool regenWhileDoT = false;
        [Tooltip("Multiplier applied to regen while DoTs are active (only if RegenWhileDoT = true)")] [SerializeField] [Range(0f, 1f)]
        private float regenDoTPenaltyMultiplier = 0.25f;
        [Tooltip("Optional VFX key played when regen ticks")] [SerializeField]
        private string regenVfxKey = "";

        [Header("Abilities - Pulse Nuke (Destroys Towers)")]
        [Tooltip("If enabled, this enemy will occasionally emit a large pulse that destroys all player towers in a radius.")]
        [SerializeField] private bool usePulseNukeAbility = false;
        [Tooltip("Pulse radius in world units")] [SerializeField] [Min(0f)] private float pulseNukeRadius = 4.5f;
        [Tooltip("Minimum cooldown between pulses (seconds)")] [SerializeField] [Min(0f)] private float pulseNukeCooldownMin = 45f;
        [Tooltip("Maximum cooldown between pulses (seconds)")] [SerializeField] [Min(0f)] private float pulseNukeCooldownMax = 75f;
        [Tooltip("After the cooldown, how often to check (seconds) for a random trigger")] [SerializeField] [Min(0.1f)] private float pulseNukeRandomCheckInterval = 1.0f;
        [Tooltip("Chance to trigger on each check after cooldown (0..1)")] [SerializeField] [Range(0f,1f)] private float pulseNukeTriggerChance = 0.2f;
        [Header("Pulse Nuke VFX/SFX")]
        [Tooltip("Optional VFX key to spawn a shockwave; if empty, uses procedural ripple")] [SerializeField] private string pulseNukeVfxKey = "";
        [SerializeField] private Color pulseNukeRippleColor = new Color(0.9f, 0.85f, 1f, 0.5f);
        [SerializeField] [Min(0.05f)] private float pulseNukeRippleDuration = 0.45f;
        [SerializeField] [Min(0.001f)] private float pulseNukeRippleLineWidth = 0.12f;
        [SerializeField] [Min(8)] private int pulseNukeRippleSegments = 48;
        [Tooltip("SFX key to play when the pulse triggers")] [SerializeField] private string pulseNukeSfxKey = "";

        [Header("Abilities - Stun Pulse (Stuns Towers)")]
        [Tooltip("If enabled, this enemy will occasionally emit a pulse that stuns player towers in a radius.")]
        [SerializeField] private bool useStunPulseAbility = false;
        [Tooltip("Stun pulse radius in world units")] [SerializeField] [Min(0f)] private float stunPulseRadius = 4.5f;
        [Tooltip("Minimum cooldown between stun pulses (seconds)")] [SerializeField] [Min(0f)] private float stunPulseCooldownMin = 35f;
        [Tooltip("Maximum cooldown between stun pulses (seconds)")] [SerializeField] [Min(0f)] private float stunPulseCooldownMax = 60f;
        [Tooltip("After the cooldown, how often to check (seconds) for a random trigger")] [SerializeField] [Min(0.1f)] private float stunPulseRandomCheckInterval = 1.0f;
        [Tooltip("Chance to trigger on each check after cooldown (0..1)")] [SerializeField] [Range(0f,1f)] private float stunPulseTriggerChance = 0.25f;
        [Tooltip("How long towers are stunned (seconds)")] [SerializeField] [Min(0f)] private float stunPulseDuration = 2.5f;
        [Header("Stun Pulse VFX/SFX")]
        [Tooltip("Optional VFX key to spawn a shockwave; if empty, uses procedural ripple")] [SerializeField] private string stunPulseVfxKey = "";
        [SerializeField] private Color stunPulseRippleColor = new Color(0.8f, 0.9f, 1f, 0.55f);
        [SerializeField] [Min(0.05f)] private float stunPulseRippleDuration = 0.45f;
        [SerializeField] [Min(0.001f)] private float stunPulseRippleLineWidth = 0.12f;
        [SerializeField] [Min(8)] private int stunPulseRippleSegments = 48;
        [Tooltip("SFX key to play when the stun pulse triggers")] [SerializeField] private string stunPulseSfxKey = "";

        public string EnemyId => enemyId;
        public string DisplayName => displayName;
        public float MovementSpeed => Mathf.Max(0f, movementSpeed);
        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public float DamagePerHit => Mathf.Max(0f, damagePerHit);
        public GameObject Prefab => prefab;

        // Regen ability accessors
        public bool UseRegenAbility => useRegenAbility;
        public float RegenPerSecondBase => Mathf.Max(0f, regenPerSecondBase);
        public float RegenMissingHealthFactor => Mathf.Max(0f, regenMissingHealthFactor);
        public float RegenSuppressAfterDamageSeconds => Mathf.Max(0f, regenSuppressAfterDamageSeconds);
        public float RegenPerSecondCap => Mathf.Max(0f, regenPerSecondCap);
        public float RegenTotalPercentCap => Mathf.Clamp01(regenTotalPercentCap);
        public bool RegenWhileDoT => regenWhileDoT;
        public float RegenDoTPenaltyMultiplier => Mathf.Clamp01(regenDoTPenaltyMultiplier);
        public string RegenVfxKey => regenVfxKey;

        // Pulse nuke accessors
        public bool UsePulseNukeAbility => usePulseNukeAbility;
        public float PulseNukeRadius => Mathf.Max(0f, pulseNukeRadius);
        public float PulseNukeCooldownMin => Mathf.Max(0f, pulseNukeCooldownMin);
        public float PulseNukeCooldownMax => Mathf.Max(pulseNukeCooldownMin, pulseNukeCooldownMax);
        public float PulseNukeRandomCheckInterval => Mathf.Max(0.1f, pulseNukeRandomCheckInterval);
        public float PulseNukeTriggerChance => Mathf.Clamp01(pulseNukeTriggerChance);
        public string PulseNukeVfxKey => pulseNukeVfxKey;
        public Color PulseNukeRippleColor => pulseNukeRippleColor;
        public float PulseNukeRippleDuration => Mathf.Max(0.01f, pulseNukeRippleDuration);
        public float PulseNukeRippleLineWidth => Mathf.Max(0.001f, pulseNukeRippleLineWidth);
        public int PulseNukeRippleSegments => Mathf.Max(8, pulseNukeRippleSegments);
        public string PulseNukeSfxKey => pulseNukeSfxKey;

        // Stun pulse accessors
        public bool UseStunPulseAbility => useStunPulseAbility;
        public float StunPulseRadius => Mathf.Max(0f, stunPulseRadius);
        public float StunPulseCooldownMin => Mathf.Max(0f, stunPulseCooldownMin);
        public float StunPulseCooldownMax => Mathf.Max(stunPulseCooldownMin, stunPulseCooldownMax);
        public float StunPulseRandomCheckInterval => Mathf.Max(0.1f, stunPulseRandomCheckInterval);
        public float StunPulseTriggerChance => Mathf.Clamp01(stunPulseTriggerChance);
        public float StunPulseDuration => Mathf.Max(0f, stunPulseDuration);
        public string StunPulseVfxKey => stunPulseVfxKey;
        public Color StunPulseRippleColor => stunPulseRippleColor;
        public float StunPulseRippleDuration => Mathf.Max(0.01f, stunPulseRippleDuration);
        public float StunPulseRippleLineWidth => Mathf.Max(0.001f, stunPulseRippleLineWidth);
        public int StunPulseRippleSegments => Mathf.Max(8, stunPulseRippleSegments);
        public string StunPulseSfxKey => stunPulseSfxKey;
    }
}
