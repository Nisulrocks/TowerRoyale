using UnityEngine;
using TR.Systems;

namespace TR.Data
{
    [System.Serializable]
    public struct TowerStats
    {
        public int level;
        public float dps;
        public float fireRate;
        public float range;
        public float splashRadius; 
        public int cost;           
    }

    [CreateAssetMenu(fileName = "CardDefinition", menuName = "TR/Data/Card Definition")]
    public class CardDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string cardId;           
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private RarityDefinition rarity;

        [Header("Battle Prefab")]
        [SerializeField] private GameObject towerPrefab;  

        [Header("Unlocking")] 

        [SerializeField] private ArenaDefinition unlockArena; 

        [Header("Stat Formula (value = base + perLevel*(level-1))")]
        [SerializeField] private float dpsBase = 5f;    [SerializeField] private float dpsPerLevel = 2f;
        [SerializeField] private float fireRateBase = 1f; [SerializeField] private float fireRatePerLevel = 0.1f;
        [SerializeField] private float rangeBase = 3f;     [SerializeField] private float rangePerLevel = 0.3f;
        [SerializeField] private float splashBase = 0f;     [SerializeField] private float splashPerLevel = 0f;
        [SerializeField] private int costBase = 50;         [SerializeField] private int costPerLevel = 7;

        [Header("Critical Hits (optional)")]
        [Tooltip("Chance to critically hit (0..1) at level 1 and per level increment")] [SerializeField]
        private float critChanceBase = 0f; [SerializeField] private float critChancePerLevel = 0f;
        [Tooltip("Critical damage multiplier at level 1 and per level increment (e.g., 2 = double damage)")] [SerializeField]
        private float critMultiplierBase = 2f; [SerializeField] private float critMultiplierPerLevel = 0f;
        [Tooltip("Burst text to display when a critical hit occurs (e.g., 'CRIT!', 'HEADSHOT!')")] [SerializeField]
        private string critBurstText = "CRIT!";

        [Header("On-Hit Effects (optional)")]
        [Header("On-Hit Effects Formula (optional)")]
        [SerializeField] private float burnDpsBase = 0f;       [SerializeField] private float burnDpsPerLevel = 0f;
        [SerializeField] private float burnDurBase = 0f;        [SerializeField] private float burnDurPerLevel = 0f;
        [SerializeField] private float poisonDpsBase = 0f;      [SerializeField] private float poisonDpsPerLevel = 0f;
        [SerializeField] private float poisonDurBase = 0f;       [SerializeField] private float poisonDurPerLevel = 0f;
        [Header("Hit Visual Overrides (optional)")]
        [Tooltip("If true, overrides projectile prefab and uses a lightning zap visual on hit (instant).")]
        [SerializeField] private bool useLightningZapOnHit = false;
        [SerializeField] private float zapDuration = 0.12f;
        [SerializeField] private float zapWidth = 0.06f;
        [SerializeField] private float zapJitter = 0.18f;
        [SerializeField] private int zapSegments = 12;
        [SerializeField] private Color zapColor = new Color(0.7f, 0.9f, 1f, 1f);
        [Tooltip("Optional ParticleManager key to spawn at hit point with the zap (one-shot)")]
        [SerializeField] private string zapHitVfxKey = "";

        [SerializeField] private bool forceDefaultZapMaterial = false;
        [Header("Lightning Zap Material (optional)")]
        [Tooltip("Optional custom material for LightningZap line renderer (both override and chain). Leave empty to use default.")]
        [SerializeField] private Material zapMaterial;
        [Header("Lightning Zap Glow (override)")]
        [Tooltip("Enable HDR glow (bloom-friendly) for the Lightning Zap visual override")]
        [SerializeField] private bool zapGlowEnabled = true;

        [SerializeField] private float zapGlowBoost = 2.0f;
        [Header("Chain Ricochet Glow")]
        [Tooltip("Enable HDR glow (bloom-friendly) for chain ricochet zaps")]
        [SerializeField] private bool chainGlowEnabled = true;

        [SerializeField] private float chainGlowBoost = 2.0f;
        [Header("On-Hit Tornado (optional)")]
[SerializeField]
        private bool tornadoOnHit = false;
[SerializeField]
        private float tornadoRadiusBase = 0f; [SerializeField] private float tornadoRadiusPerLevel = 0f;
        [Tooltip("Tornado pull strength (units per second) at level 1 and per level increment")] [SerializeField]
        private float tornadoStrengthBase = 0f; [SerializeField] private float tornadoStrengthPerLevel = 0f;
[SerializeField]
        private float tornadoDurationBase = 0f; [SerializeField] private float tornadoDurationPerLevel = 0f;
[SerializeField]
        private int tornadoMaxPullTargets = 6;
        [Header("Tornado Allowed Enemy Tiers")] [SerializeField] private bool tornadoAllowEasy = true;
        [SerializeField] private bool tornadoAllowMedium = true; [SerializeField] private bool tornadoAllowHard = true; [SerializeField] private bool tornadoAllowBoss = true;
        [Header("Tornado VFX (optional)")]
        [Tooltip("ParticleManager key for the tornado visual (looping). Leave empty to disable visuals.")]
        [SerializeField] private string tornadoVfxKey = "";
        [Tooltip("Multiplier to make the tornado VFX match the gameplay radius (used when auto-scaling VFX).\nFinal VFX radius = tornadoRadius * tornadoVfxScaleMultiplier")] 
        [SerializeField] private float tornadoVfxScaleMultiplier = 1.0f;
        [Header("Tornado Behavior (optional)")]
        [Tooltip("If true, enemies can stack at the very center (disables inner orbit + reduces separation). If false, they will orbit around a small core.")]
        [SerializeField] private bool tornadoAllowCenterStack = false;

        [SerializeField] private float tornadoFalloffPower = 1.0f;
        [Header("Targeting Behavior (optional)")]
        [Tooltip("If true and this card applies any on-hit effect (burn/poison/slow), the tower will hit a target once then switch to a new target, letting the effect tick.")]
        [SerializeField] private bool moveOnAfterEffect = false;

        [SerializeField] private float moveOnIgnoreSeconds = 0.6f;
        [Header("Targeting Priority (optional)")]

        [SerializeField] private bool focusOnHighestHp = false;
        [Header("On-Hit Slow (optional)")]

        [SerializeField] private bool slowOnHit = false;
        [Tooltip("Slow percent as a fraction (e.g., 0.3 = 30%) base value at level 1")] [SerializeField]
        private float slowPercentBase = 0f; [SerializeField] private float slowPercentPerLevel = 0f;
[SerializeField]
        private float slowDurationBase = 0f; [SerializeField] private float slowDurationPerLevel = 0f;

        [Header("On-Hit Stun (optional)")]
        [Tooltip("If enabled, hits from this card have a chance to stun the enemy (disables movement/attacks)")]
        [SerializeField] private bool stunOnHit = false;
        [Tooltip("Stun chance at level 1 and per level increment (0..1 range per step)")] [SerializeField]
        private float stunChanceBase = 0f; [SerializeField] private float stunChancePerLevel = 0f;
        [Tooltip("Stun duration (seconds) at level 1 and per level increment")] [SerializeField]
        private float stunDurationBase = 0f; [SerializeField] private float stunDurationPerLevel = 0f;

        [Header("On-Hit Frostbite (optional)")]
        [Tooltip("If enabled, hits apply a Frostbite DoT (DPS for a duration). Requires Slow to be enabled on this card.")]
        [SerializeField] private bool frostbiteOnHit = false;
[SerializeField]
        private float frostbiteDpsBase = 0f; [SerializeField] private float frostbiteDpsPerLevel = 0f;
        [Tooltip("Frostbite duration (seconds) at level 1 and per level increment")] [SerializeField]
        private float frostbiteDurBase = 0f; [SerializeField] private float frostbiteDurPerLevel = 0f;

        [Header("SFX (Keys)")]
        [Tooltip("Sound key when the tower fires (muzzle)")] [SerializeField] private string sfxFireKey = "";
[SerializeField] private string sfxHitKey = "";
[SerializeField] private string sfxSplashKey = "";
[SerializeField] private string sfxChainJumpKey = "";
[SerializeField] private string sfxZapFireKey = "";
[SerializeField] private string sfxZapHitKey = "";
        [Tooltip("Looping beam sound key for Inferno (will fade in/out)")] [SerializeField] private string sfxBeamKey = "";
        [Tooltip("Looping tornado sound key (will fade in/out)")] [SerializeField] private string sfxTornadoKey = "";
[SerializeField] private string sfxBurnApplyKey = "";
[SerializeField] private string sfxPoisonApplyKey = "";
[SerializeField] private string sfxSlowApplyKey = "";
[SerializeField] private string sfxStunApplyKey = "";
[SerializeField] private string sfxCritKey = "";

        [Header("Aiming (Regular Towers)")]
        [Tooltip("If true, regular towers rotate to face their current target (Economy/Inferno ignore this).")]
        [SerializeField] private bool rotateToTarget = true;

        [SerializeField] private float rotateSpeedDegPerSec = 360f;

        [Header("Projectiles (Regular Towers)")]

        [SerializeField] private GameObject projectilePrefab;

        [SerializeField] private float projectileSpeed = 8f;
        [Tooltip("Optional particle VFX key to spawn at projectile impact position (one-shot)")]
        [SerializeField] private string projectileImpactVfxKey = "";
        [Header("Idle VFX (optional)")]
        [Tooltip("ParticleManager key for a looping idle effect (e.g., muzzle glow). Leave empty to disable.")]
        [SerializeField] private string idleVfxKey = "";
        [Header("Defeat Cleanup (optional)")]

        [SerializeField] private string defeatDestroyVfxKey = "";

        [SerializeField] private string defeatDestroySfxKey = "";
        [Header("On-Hit Chain (Ricochet)")]

        [SerializeField] private bool chainOnHit = false;
        [Tooltip("Max additional enemies (not counting the main target) at level 1 and per level increment")]
        [SerializeField] private int chainMaxJumpsBase = 0; [SerializeField] private int chainMaxJumpsPerLevel = 0;
        [Tooltip("Damage multiplier applied per jump (e.g., 0.7 means 30% less per jump) at level 1 and per level increment")]
        [Range(0f, 1f)] [SerializeField] private float chainFalloffPerJumpBase = 0.7f; [SerializeField] private float chainFalloffPerJumpPerLevel = 0f;

        [SerializeField] private Color chainZapColor = new Color(0.6f, 0.85f, 1f, 1f);
        [Tooltip("Chain zap duration (seconds). If 0, falls back to main zap duration.")]
        [SerializeField] private float chainZapDuration = 0.08f;

        [SerializeField] private float chainZapWidth = 0.04f;

        [SerializeField] private float chainZapJitter = 0.15f;

        [SerializeField] private int chainZapSegments = 10;
        [Tooltip("If true, on-hit effects (burn/poison/slow/stun) are also applied to chained targets.")]
        [SerializeField] private bool chainTransfersOnHitEffects = false;

        
        public string CardId => cardId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public RarityDefinition Rarity => rarity;
        public GameObject TowerPrefab => towerPrefab;
        public ArenaDefinition UnlockArena => unlockArena;
        public int RequiredTrophies => unlockArena != null ? Mathf.Max(0, unlockArena.TrophyRequirement) : 0;

        
        public bool IsUnlockedForTrophies(int trophies)
        {
            if (unlockArena == null) return true;
            return Mathf.Max(0, trophies) >= Mathf.Max(0, unlockArena.TrophyRequirement);
        }

        
        public virtual float GetCritChance(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Clamp01(critChanceBase + critChancePerLevel * (lv - 1));
        }
        public virtual float GetCritMultiplier(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Max(1f, critMultiplierBase + critMultiplierPerLevel * (lv - 1));
        }
        public virtual string GetCritBurstText() => string.IsNullOrEmpty(critBurstText) ? "CRIT!" : critBurstText;
        public bool IsUnlockedForPlayer()
        {
            return IsUnlockedForTrophies(PlayerProfile.GetTrophies());
        }
        public virtual bool ShouldRotateToTarget() => rotateToTarget;
        public virtual float GetRotateSpeedDegPerSec() => Mathf.Max(0f, rotateSpeedDegPerSec);
        public virtual GameObject GetProjectilePrefab() => projectilePrefab;
        public virtual float GetProjectileSpeed() => Mathf.Max(0.1f, projectileSpeed);
        public virtual string GetProjectileImpactVfxKey() => projectileImpactVfxKey;
        public virtual string GetIdleVfxKey() => idleVfxKey;
        public virtual string GetDefeatDestroyVfxKey() => defeatDestroyVfxKey;
        public virtual string GetDefeatDestroySfxKey() => defeatDestroySfxKey;
        
        public bool HasChainOnHit() => chainOnHit;
        public int GetChainMaxJumps(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            int v = chainMaxJumpsBase + chainMaxJumpsPerLevel * (lv - 1);
            return Mathf.Max(0, v);
        }
        public float GetChainFalloffPerJump(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            float v = chainFalloffPerJumpBase + chainFalloffPerJumpPerLevel * (lv - 1);
            return Mathf.Clamp01(v);
        }
        public Color GetChainZapColor() => chainZapColor;
        public float GetChainZapDurationOrFallback() => chainZapDuration > 0f ? chainZapDuration : GetZapDuration();
        public float GetChainZapWidthOrFallback() => chainZapWidth > 0f ? chainZapWidth : GetZapWidth();
        public float GetChainZapJitterOrFallback() => chainZapJitter > 0f ? chainZapJitter : GetZapJitter();
        public int GetChainZapSegmentsOrFallback()
        {
            int seg = chainZapSegments;
            if (seg <= 1) seg = GetZapSegments();
            return Mathf.Clamp(seg, 2, 64);
        }
        public bool GetChainTransfersOnHitEffects() => chainTransfersOnHitEffects;

        
        public virtual TowerStats GetStatsForLevel(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);

            float dpsVal = Mathf.Clamp(dpsBase + dpsPerLevel * (lv - 1), 0f, float.MaxValue);
            float fireRateVal = Mathf.Clamp(fireRateBase + fireRatePerLevel * (lv - 1), 0.01f, float.MaxValue);
            float rangeVal = Mathf.Clamp(rangeBase + rangePerLevel * (lv - 1), 0f, float.MaxValue);
            float splashVal = Mathf.Clamp(splashBase + splashPerLevel * (lv - 1), 0f, float.MaxValue);
            int costVal = Mathf.Clamp(costBase + costPerLevel * (lv - 1), 0, int.MaxValue);

            return new TowerStats
            {
                level = lv,
                dps = dpsVal,
                fireRate = fireRateVal,
                range = rangeVal,
                splashRadius = splashVal,
                cost = costVal
            };
        }

        
        public virtual float GetBurnDps(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Clamp(burnDpsBase + burnDpsPerLevel * (lv - 1), 0f, float.MaxValue);
        }
        public virtual float GetBurnDuration(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Clamp(burnDurBase + burnDurPerLevel * (lv - 1), 0f, float.MaxValue);
        }
        public virtual float GetPoisonDps(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Clamp(poisonDpsBase + poisonDpsPerLevel * (lv - 1), 0f, float.MaxValue);
        }
        public virtual float GetPoisonDuration(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Clamp(poisonDurBase + poisonDurPerLevel * (lv - 1), 0f, float.MaxValue);
        }

        
        public bool HasSlowOnHit() => slowOnHit;
        public virtual float GetSlowPercent(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Clamp(slowPercentBase + slowPercentPerLevel * (lv - 1), 0f, 0.95f); 
        }
        public virtual float GetSlowDuration(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Clamp(slowDurationBase + slowDurationPerLevel * (lv - 1), 0f, float.MaxValue);
        }

        
        public bool HasStunOnHit() => stunOnHit;
        public virtual float GetStunChance(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Clamp01(stunChanceBase + stunChancePerLevel * (lv - 1));
        }
        public virtual float GetStunDuration(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Max(0f, stunDurationBase + stunDurationPerLevel * (lv - 1));
        }

        
        public bool HasFrostbiteOnHit() => frostbiteOnHit;
        public virtual float GetFrostbiteDps(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Max(0f, frostbiteDpsBase + frostbiteDpsPerLevel * (lv - 1));
        }
        public virtual float GetFrostbiteDuration(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Max(0f, frostbiteDurBase + frostbiteDurPerLevel * (lv - 1));
        }

        
        public string GetSfxFireKey() => sfxFireKey;
        public string GetSfxHitKey() => sfxHitKey;
        public string GetSfxSplashKey() => sfxSplashKey;
        public string GetSfxChainJumpKey() => sfxChainJumpKey;
        public string GetSfxZapFireKey() => sfxZapFireKey;
        public string GetSfxZapHitKey() => sfxZapHitKey;
        public string GetSfxBeamKey() => sfxBeamKey;
        public string GetSfxTornadoKey() => sfxTornadoKey;
        public string GetSfxBurnApplyKey() => sfxBurnApplyKey;
        public string GetSfxPoisonApplyKey() => sfxPoisonApplyKey;
        public string GetSfxSlowApplyKey() => sfxSlowApplyKey;
        public string GetSfxStunApplyKey() => sfxStunApplyKey;
        public string GetSfxCritKey() => sfxCritKey;

        
        public bool HasTornadoOnHit() => tornadoOnHit;
        public float GetTornadoRadius(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Max(0f, tornadoRadiusBase + tornadoRadiusPerLevel * (lv - 1));
        }
        public float GetTornadoStrength(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Max(0f, tornadoStrengthBase + tornadoStrengthPerLevel * (lv - 1));
        }
        public float GetTornadoDuration(int level)
        {
            int lv = Mathf.Clamp(level, 1, rarity != null ? rarity.MaxLevel : level);
            return Mathf.Max(0f, tornadoDurationBase + tornadoDurationPerLevel * (lv - 1));
        }
        public int GetTornadoMaxPullTargets() => Mathf.Max(0, tornadoMaxPullTargets);
        public bool TornadoAllowsTier(ArenaDefinition.EnemyTier tier)
        {
            switch (tier)
            {
                case ArenaDefinition.EnemyTier.Easy: return tornadoAllowEasy;
                case ArenaDefinition.EnemyTier.Medium: return tornadoAllowMedium;
                case ArenaDefinition.EnemyTier.Hard: return tornadoAllowHard;
                case ArenaDefinition.EnemyTier.Boss: return tornadoAllowBoss;
                default: return true;
            }
        }
        public string GetTornadoVfxKey() => tornadoVfxKey;
        public float GetTornadoVfxScaleMultiplier() => Mathf.Max(0f, tornadoVfxScaleMultiplier);
        public bool GetTornadoAllowCenterStack() => tornadoAllowCenterStack;
        public float GetTornadoFalloffPower() => Mathf.Clamp(tornadoFalloffPower, 0.1f, 5f);

        
        public bool UseLightningZapOnHit() => useLightningZapOnHit;
        public float GetZapDuration() => Mathf.Max(0.02f, zapDuration);
        public float GetZapWidth() => Mathf.Max(0.001f, zapWidth);
        public float GetZapJitter() => Mathf.Max(0f, zapJitter);
        public int GetZapSegments() => Mathf.Clamp(zapSegments, 2, 64);
        public Color GetZapColor() => zapColor;
        public string GetZapHitVfxKey() => zapHitVfxKey;
        public bool GetForceDefaultZapMaterial() => forceDefaultZapMaterial;
        public Material GetZapMaterial() => zapMaterial;
        public bool GetZapGlowEnabled() => zapGlowEnabled;
        public float GetZapGlowBoost() => Mathf.Max(0f, zapGlowBoost);
        public bool GetChainGlowEnabled() => chainGlowEnabled;
        public float GetChainGlowBoost() => Mathf.Max(0f, chainGlowBoost);

        
        public bool MoveOnAfterEffect => moveOnAfterEffect;
        public float GetMoveOnIgnoreSeconds() => Mathf.Max(0f, moveOnIgnoreSeconds);
        public bool FocusOnHighestHp => focusOnHighestHp;
    }
}
