using UnityEngine;
using TR.Data;
using TR.UI;
using TR.VFX;
using TR.Audio;

namespace TR.Battle
{
    // Basic 2D enemy runtime: moves along Path2D, tracks health, and exposes TakeDamage.
    public class EnemyBase2D : MonoBehaviour
    {
        // Global registry for fast lookup
        private static readonly System.Collections.Generic.HashSet<EnemyBase2D> s_all = new();
        public static System.Collections.Generic.IReadOnlyCollection<EnemyBase2D> All => s_all;
        [Header("Runtime")]
        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private float currentHealth;
        [SerializeField] private float moveSpeed;
        [SerializeField] private int waypointIndex;
        [SerializeField] private Path2D path;
        [SerializeField] private float reachThreshold = 0.02f;
        // Boss per-spawn scaling (runtime)
        [SerializeField] private float _runtimeMaxHealth; // if > 0 overrides definition.MaxHealth
        [SerializeField] private float _bossDamageMult = 1f;
        [Header("Auto-Config")]
        [SerializeField] private bool autoFindPath = true; // if true, auto-locate Path2D in scene when none is assigned

        [Header("UI (Optional)")]
        [SerializeField] private GameObject healthBarUIPrefab; // prefab with a Slider + EnemyHealthBarUI component
        [SerializeField] private Canvas healthBarCanvas;        // optional: parent canvas (world-space preferred)
        [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 0.6f, 0f);
        private GameObject _healthBarInstance;

        [Header("Boss UI (Screen Space)")]
        [Tooltip("Either assign a BossHealthUI prefab (if BossHealthUI is on the root), or assign a GameObject prefab where BossHealthUI exists on a child.")]
        [SerializeField] private BossHealthUI bossHealthUIPrefab; // if BossHealthUI component is on root
        [SerializeField] private GameObject bossHealthUIPrefabGO; // if BossHealthUI component is in children
        private BossHealthUI _bossUIInstance;
        private ArenaDefinition _arena;

        [Header("VFX")]
        [Tooltip("Key from ParticleManager registry to spawn on death. Leave empty to disable.")]
        [SerializeField] private string deathVfxKey = "";
        [Tooltip("Optional position anchor for death VFX. If null, uses enemy transform position.")]
        [SerializeField] private Transform deathVfxAnchor;
        [Tooltip("Anchor for status VFX (burn/poison); if null uses enemy position")] [SerializeField] private Transform statusVfxAnchor;
        [Tooltip("Particle key to play periodically while burn ticks")] [SerializeField] private string burnTickVfxKey = "";
        [Tooltip("Seconds between burn tick VFX (rate limit)")] [SerializeField] private float burnTickVfxInterval = 0.3f;
        [Tooltip("Particle key to play periodically while poison ticks")] [SerializeField] private string poisonTickVfxKey = "";
        [Tooltip("Seconds between poison tick VFX (rate limit)")] [SerializeField] private float poisonTickVfxInterval = 0.3f;
        [Tooltip("Particle key to play periodically while slowed")] [SerializeField] private string slowTickVfxKey = "";
        [Tooltip("Seconds between slow tick VFX (rate limit)")] [SerializeField] private float slowTickVfxInterval = 0.35f;
        [Tooltip("One-shot particle key to play when this enemy is hit for damage (optional)")]
        [SerializeField] private string hitVfxKey = "";
        [Tooltip("Optional transform anchor for hit VFX; if null uses enemy position")] [SerializeField]
        private Transform hitVfxAnchor;
        [Tooltip("Minimum seconds between repeated hit VFX spawns (prevents spam for beams/DoTs)")]
        [SerializeField] private float hitVfxInterval = 0.12f;
        private float _nextHitVfxTime = 0f;
        [Tooltip("Minimum seconds between hit VFX when damage comes from status ticks (burn/poison/frostbite)")]
        [SerializeField] private float statusHitVfxInterval = 0.45f;
        private float _nextStatusHitVfxTime = 0f;
        [Header("SFX")]
        [Tooltip("SFX key to play on death (SFXManager). Leave empty to disable.")]
        [SerializeField] private string deathSfxKey = "";

        // Regen runtime tracking
        private float _lastDamageTime;
        private float _totalRegenThisLife;

        // Pulse Nuke runtime
        private float _pulseNukeNextTime;      // absolute time when we can start random checks
        private float _pulseNukeCheckTimer;    // accumulator to step random checks
        // Stun Pulse runtime
        private float _stunPulseNextTime;
        private float _stunPulseCheckTimer;

        public EnemyDefinition Definition => definition;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => _runtimeMaxHealth > 0f ? _runtimeMaxHealth : (definition != null ? definition.MaxHealth : Mathf.Max(currentHealth, 1f));
        public System.Action<float, float> OnHealthChanged; // (current, max)

        private void Awake()
        {
            // If this prefab is dropped in scene without being initialized by a spawner, 
            // auto-configure from serialized definition and optionally find a Path2D.
            if (path == null && autoFindPath)
            {
                path = FindFirstObjectByType<Path2D>(FindObjectsInactive.Include);
            }
            // Ensure initial apply if definition is present
            if (definition != null && currentHealth <= 0f)
            {
                ApplyDefinition(definition);
                OnHealthChanged?.Invoke(currentHealth, MaxHealth);
            }
        }

        private void TryTickPulseNuke(float dt)
        {
            if (definition == null || !definition.UsePulseNukeAbility) return;
            if (currentHealth <= 0f) return;
            // Wait until initial cooldown passes
            if (Time.time < _pulseNukeNextTime) return;
            // Step check timer
            _pulseNukeCheckTimer += Mathf.Max(0f, dt);
            float interval = definition.PulseNukeRandomCheckInterval;
            if (_pulseNukeCheckTimer < interval) return;
            _pulseNukeCheckTimer = 0f;
            // Random roll
            if (Random.value <= definition.PulseNukeTriggerChance)
            {
                DoPulseNuke();
                // Reset cooldown window
                float min = definition.PulseNukeCooldownMin;
                float max = definition.PulseNukeCooldownMax;
                if (max < min) max = min;
                _pulseNukeNextTime = Time.time + Random.Range(min, max);
            }
        }

        private static readonly System.Collections.Generic.List<TowerBase> s_towerSnapshot = new System.Collections.Generic.List<TowerBase>(64);
        private void DoPulseNuke()
        {
            float radius = Mathf.Max(0f, definition.PulseNukeRadius);
            Vector3 origin = transform.position;
            // VFX: reuse pulse visuals
            string vfxKey = definition.PulseNukeVfxKey;
            if (!string.IsNullOrEmpty(vfxKey))
            {
                TR.VFX.ParticleManager.SpawnOneShot(vfxKey, origin);
            }
            else
            {
                TR.VFX.PulseRipple.Spawn(origin,
                                         radius,
                                         definition.PulseNukeRippleColor,
                                         definition.PulseNukeRippleDuration,
                                         definition.PulseNukeRippleLineWidth,
                                         definition.PulseNukeRippleSegments);
            }
            // SFX
            string sfx = definition.PulseNukeSfxKey;
            if (!string.IsNullOrEmpty(sfx) && TR.Audio.SFXManager.Instance != null)
            {
                TR.Audio.SFXManager.Instance.Play(sfx);
            }

            // Destroy all towers within radius
            s_towerSnapshot.Clear();
            foreach (var t in TowerBase.All) s_towerSnapshot.Add((TowerBase)t);
            for (int i = 0; i < s_towerSnapshot.Count; i++)
            {
                var tb = s_towerSnapshot[i];
                if (tb == null || !tb.gameObject.activeInHierarchy) continue;
                float d = Vector2.Distance((Vector2)origin, (Vector2)tb.transform.position);
                if (d <= radius)
                {
                    // Play the tower's defeat/destroy VFX & SFX before destruction
                    var def = tb.Definition;
                    if (def != null)
                    {
                        string towerVfxKey = def.GetDefeatDestroyVfxKey();
                        if (!string.IsNullOrEmpty(towerVfxKey))
                        {
                            TR.VFX.ParticleManager.SpawnOneShot(towerVfxKey, tb.transform.position);
                        }
                        string towerSfxKey = def.GetDefeatDestroySfxKey();
                        if (!string.IsNullOrEmpty(towerSfxKey) && TR.Audio.SFXManager.Instance != null)
                        {
                            TR.Audio.SFXManager.Instance.Play(towerSfxKey);
                        }
                    }
                    // Instant destroy without refund
                    Destroy(tb.gameObject);
                }
            }
            // Optional debug ring
            #if UNITY_EDITOR
            DebugDrawCircle(origin, radius, new Color(1f, 0.6f, 0.9f, 0.6f), 0.25f);
            #endif
        }

        private void TryTickStunPulse(float dt)
        {
            if (definition == null || !definition.UseStunPulseAbility) return;
            if (currentHealth <= 0f) return;
            if (Time.time < _stunPulseNextTime) return;
            _stunPulseCheckTimer += Mathf.Max(0f, dt);
            float interval = definition.StunPulseRandomCheckInterval;
            if (_stunPulseCheckTimer < interval) return;
            _stunPulseCheckTimer = 0f;
            if (Random.value <= definition.StunPulseTriggerChance)
            {
                DoStunPulse();
                float smin = definition.StunPulseCooldownMin;
                float smax = definition.StunPulseCooldownMax;
                if (smax < smin) smax = smin;
                _stunPulseNextTime = Time.time + Random.Range(smin, smax);
            }
        }

        private static readonly System.Collections.Generic.List<TowerBase> s_towerSnapshot2 = new System.Collections.Generic.List<TowerBase>(64);
        private void DoStunPulse()
        {
            float radius = Mathf.Max(0f, definition.StunPulseRadius);
            Vector3 origin = transform.position;
            // VFX (reuse pulse visuals)
            string vfxKey2 = definition.StunPulseVfxKey;
            if (!string.IsNullOrEmpty(vfxKey2))
            {
                TR.VFX.ParticleManager.SpawnOneShot(vfxKey2, origin);
            }
            else
            {
                TR.VFX.PulseRipple.Spawn(origin,
                                         radius,
                                         definition.StunPulseRippleColor,
                                         definition.StunPulseRippleDuration,
                                         definition.StunPulseRippleLineWidth,
                                         definition.StunPulseRippleSegments);
            }
            // SFX
            string sfx2 = definition.StunPulseSfxKey;
            if (!string.IsNullOrEmpty(sfx2) && TR.Audio.SFXManager.Instance != null)
            {
                TR.Audio.SFXManager.Instance.Play(sfx2);
            }
            // Stun all towers within radius
            float dur = Mathf.Max(0f, definition.StunPulseDuration);
            s_towerSnapshot2.Clear();
            foreach (var t in TowerBase.All) s_towerSnapshot2.Add((TowerBase)t);
            for (int i = 0; i < s_towerSnapshot2.Count; i++)
            {
                var tb = s_towerSnapshot2[i];
                if (tb == null || !tb.gameObject.activeInHierarchy) continue;
                float d = Vector2.Distance((Vector2)origin, (Vector2)tb.transform.position);
                if (d <= radius)
                {
                    tb.ApplyTowerStun(dur);
                }
            }
            #if UNITY_EDITOR
            DebugDrawCircle(origin, radius, new Color(0.7f, 0.85f, 1f, 0.6f), 0.25f);
            #endif
        }

        private float GetEffectiveMoveSpeed()
        {
            float baseSpeed = moveSpeed; // moveSpeed is configured from definition
            float mult = 1f - Mathf.Clamp01(_slowPercent);
            return Mathf.Max(0f, baseSpeed * mult);
        }

        // Debug helper to draw a circle in the Scene view
        private void DebugDrawCircle(Vector3 center, float radius, Color color, float duration)
        {
            const int seg = 20;
            Vector3 prev = center + Vector3.right * radius;
            for (int i = 1; i <= seg; i++)
            {
                float ang = i * Mathf.PI * 2f / seg;
                Vector3 next = center + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * radius;
                Debug.DrawLine(prev, next, color, duration);
                prev = next;
            }
        }

        private void OnEnable()
        {
            s_all.Add(this);
            // Note: _arena may not be assigned yet at this point. Defer boss binding until SetArena.
            TrySpawnHealthBarUI();
        }

        private void OnDisable()
        {
            s_all.Remove(this);
            if (_healthBarInstance != null)
            {
                Destroy(_healthBarInstance);
                _healthBarInstance = null;
            }
            // Unbind boss UI if this was the boss
            if (IsBoss())
            {
                if (_bossUIInstance != null)
                {
                    _bossUIInstance.UnbindIfTarget(this);
                    Destroy(_bossUIInstance.gameObject);
                    _bossUIInstance = null;
                }
            }
        }

        public void Initialize(EnemyDefinition def, Path2D followPath)
        {
            definition = def;
            path = followPath != null ? followPath : (autoFindPath ? FindFirstObjectByType<Path2D>(FindObjectsInactive.Include) : null);
            ApplyDefinition(definition);
        }

        public void SetDefinition(EnemyDefinition def)
        {
            definition = def;
            ApplyDefinition(definition);
        }

        public void SetArena(ArenaDefinition arena)
        {
            _arena = arena;
            // After arena is assigned we can determine boss tier and reconfigure UI accordingly
            if (IsBoss())
            {
                // Remove any world-space bar that may have been spawned on OnEnable
                if (_healthBarInstance != null)
                {
                    Destroy(_healthBarInstance);
                    _healthBarInstance = null;
                }
                // Spawn and bind boss screen UI from assigned prefab
                if (_bossUIInstance == null && (bossHealthUIPrefab != null || bossHealthUIPrefabGO != null))
                {
                    Transform parent = null;
                    // If assigned prefab has no Canvas in parents, try to parent under overlay canvas
                    bool prefabHasCanvas = false;
                    if (bossHealthUIPrefab != null)
                    {
                        prefabHasCanvas = bossHealthUIPrefab.GetComponentInParent<Canvas>() != null;
                    }
                    else if (bossHealthUIPrefabGO != null)
                    {
                        prefabHasCanvas = bossHealthUIPrefabGO.GetComponentInChildren<Canvas>(true) != null;
                    }
                    if (!prefabHasCanvas)
                    {
                        var overlay = FindOverlayCanvas();
                        parent = overlay != null ? overlay.transform : null;
                    }

                    if (bossHealthUIPrefab != null)
                    {
                        _bossUIInstance = Instantiate(bossHealthUIPrefab, parent);
                    }
                    else
                    {
                        var go = Instantiate(bossHealthUIPrefabGO, parent);
                        _bossUIInstance = go.GetComponentInChildren<BossHealthUI>(true);
                        if (_bossUIInstance == null)
                        {
                            Debug.LogError("[EnemyBase2D] Boss UI prefab (GameObject) is missing BossHealthUI component in children.");
                        }
                    }
                }
                if (_bossUIInstance != null)
                {
                    string name = definition != null ? (string.IsNullOrEmpty(definition.DisplayName) ? definition.name : definition.DisplayName) : "Boss";
                    _bossUIInstance.Bind(this, name);
                }
            }
        }

        private void ApplyDefinition(EnemyDefinition def)
        {
            _runtimeMaxHealth = def != null ? Mathf.Max(1f, def.MaxHealth) : 10f;
            currentHealth = _runtimeMaxHealth;
            moveSpeed = Mathf.Max(0f, def != null ? def.MovementSpeed : 1.5f);
            waypointIndex = 0;
            _bossDamageMult = 1f;
            // Reset regen tracking when definition reapplies
            _lastDamageTime = Time.time;
            _totalRegenThisLife = 0f;
            // Setup pulse nuke initial cooldown window
            if (def != null && def.UsePulseNukeAbility)
            {
                float min = def.PulseNukeCooldownMin;
                float max = def.PulseNukeCooldownMax;
                if (max < min) max = min;
                _pulseNukeNextTime = Time.time + Random.Range(min, max);
            }
            else
            {
                _pulseNukeNextTime = float.PositiveInfinity;
            }
            _pulseNukeCheckTimer = 0f;
            // Setup stun pulse initial cooldown
            if (def != null && def.UseStunPulseAbility)
            {
                float smin = def.StunPulseCooldownMin;
                float smax = def.StunPulseCooldownMax;
                if (smax < smin) smax = smin;
                _stunPulseNextTime = Time.time + Random.Range(smin, smax);
            }
            else
            {
                _stunPulseNextTime = float.PositiveInfinity;
            }
            _stunPulseCheckTimer = 0f;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        [Header("Attack")]
        [SerializeField] private float attackInterval = 1.0f; // seconds between hits on the castle
        private float _attackTimer;

        // === Status Effects (simple) ===
        private float _burnDps;      // accumulated DPS from burns
        private float _burnTime;     // remaining burn duration
        private float _poisonDps;    // accumulated DPS from poison
        private float _poisonTime;   // remaining poison duration
        // Frostbite (DoT gated by slow threshold)
        private float _frostbiteDps;   // accumulated DPS from frostbite
        private float _frostbiteTime;  // remaining frostbite duration
        private float _burnVfxTimer; // time until next burn tick VFX
        private float _poisonVfxTimer; // time until next poison tick VFX
        private float _frostbiteVfxTimer; // time until next frostbite tick VFX
        private float _slowVfxTimer;   // time until next slow tick VFX
        // Slow (movement debuff)
        private float _slowPercent;   // 0..0.95 fraction
        private float _slowTime;      // remaining slow duration
        // Stun (hard CC): disables movement and attacking
        private float _stunTime;      // remaining stun duration
        [Tooltip("Particle key to play periodically while stunned")] [SerializeField] private string stunTickVfxKey = "";
        [Tooltip("Seconds between stun tick VFX (rate limit)")] [SerializeField] private float stunTickVfxInterval = 0.35f;
        private float _stunVfxTimer;

        private void Update()
        {
            TickStatusEffects();
            // Boss abilities tick (e.g., pulse nuke) even if stunned
            TryTickPulseNuke(Time.deltaTime);
            TryTickStunPulse(Time.deltaTime);
            // If stunned, do not move or attack
            if (_stunTime > 0f)
            {
                return;
            }
            if (path == null || path.Waypoints == null || path.Waypoints.Length == 0)
            {
                // No path: just idle or move right as fallback
                transform.position += Vector3.right * GetEffectiveMoveSpeed() * Time.deltaTime;
                return;
            }

            var wps = path.Waypoints;
            if (waypointIndex >= wps.Length)
            {
                // At goal: attack the base over time
                TickAttackBase();
                return;
            }

            var target = wps[waypointIndex];
            if (target == null)
            {
                waypointIndex++;
                return;
            }

            Vector3 pos = transform.position;
            Vector3 to = (target.position - pos);
            float dist = to.magnitude;
            if (dist <= reachThreshold)
            {
                waypointIndex++;
            }
            else
            {
                Vector3 dir = to / (dist > 1e-5f ? dist : 1f);
                transform.position = pos + dir * GetEffectiveMoveSpeed() * Time.deltaTime;
            }
        }

        // === Status effect ticking and application ===
        private void TickStatusEffects()
        {
            float dt = Time.deltaTime;
            if (_burnVfxTimer > 0f) _burnVfxTimer -= dt;
            if (_poisonVfxTimer > 0f) _poisonVfxTimer -= dt;
            if (_frostbiteVfxTimer > 0f) _frostbiteVfxTimer -= dt;
            if (_slowVfxTimer > 0f) _slowVfxTimer -= dt;
            if (_stunVfxTimer > 0f) _stunVfxTimer -= dt;
            // Stun decay
            if (_stunTime > 0f)
            {
                _stunTime -= dt;
                if (_stunTime < 0f) _stunTime = 0f;
                else
                {
                    if (!string.IsNullOrEmpty(stunTickVfxKey) && _stunVfxTimer <= 0f)
                    {
                        var pos = statusVfxAnchor != null ? statusVfxAnchor.position : transform.position;
                        TR.VFX.ParticleManager.SpawnOneShot(stunTickVfxKey, pos);
                        _stunVfxTimer = Mathf.Max(0.05f, stunTickVfxInterval);
                    }
                }
            }
            // Slow decay
            if (_slowTime > 0f)
            {
                _slowTime -= dt;
                if (_slowTime <= 0f)
                {
                    _slowTime = 0f;
                    _slowPercent = 0f;
                }
                else
                {
                    // Optional slow tick VFX (rate-limited)
                    if (!string.IsNullOrEmpty(slowTickVfxKey) && _slowVfxTimer <= 0f)
                    {
                        var pos = statusVfxAnchor != null ? statusVfxAnchor.position : transform.position;
                        TR.VFX.ParticleManager.SpawnOneShot(slowTickVfxKey, pos);
                        _slowVfxTimer = Mathf.Max(0.05f, slowTickVfxInterval);
                    }
                }
            }
            // Apply burn first
            if (_burnTime > 0f && _burnDps > 0f)
            {
                float burnTick = _burnDps * dt;
                _burnTime -= dt;
                if (_burnTime <= 0f) { _burnTime = 0f; _burnDps = 0f; }
                if (burnTick > 0f) TakeDamageFromStatus(burnTick, DamageType.Inferno);
                // Optional burn tick VFX (rate-limited)
                if (!string.IsNullOrEmpty(burnTickVfxKey) && _burnVfxTimer <= 0f)
                {
                    var pos = statusVfxAnchor != null ? statusVfxAnchor.position : transform.position;
                    TR.VFX.ParticleManager.SpawnOneShot(burnTickVfxKey, pos);
                    _burnVfxTimer = Mathf.Max(0.05f, burnTickVfxInterval);
                }
            }
            // Then poison
            if (_poisonTime > 0f && _poisonDps > 0f)
            {
                float poisonTick = _poisonDps * dt;
                _poisonTime -= dt;
                if (_poisonTime <= 0f) { _poisonTime = 0f; _poisonDps = 0f; }
                if (poisonTick > 0f) TakeDamageFromStatus(poisonTick, DamageType.Elemental);
                // Optional poison tick VFX (rate-limited)
                if (!string.IsNullOrEmpty(poisonTickVfxKey) && _poisonVfxTimer <= 0f)
                {
                    var pos = statusVfxAnchor != null ? statusVfxAnchor.position : transform.position;
                    TR.VFX.ParticleManager.SpawnOneShot(poisonTickVfxKey, pos);
                    _poisonVfxTimer = Mathf.Max(0.05f, poisonTickVfxInterval);
                }
            }
            // Finally frostbite
            if (_frostbiteTime > 0f && _frostbiteDps > 0f)
            {
                float frostTick = _frostbiteDps * dt;
                _frostbiteTime -= dt;
                if (_frostbiteTime <= 0f) { _frostbiteTime = 0f; _frostbiteDps = 0f; }
                if (frostTick > 0f) TakeDamageFromStatus(frostTick, DamageType.Elemental);
                // Optional: reuse slow VFX key if a dedicated frostbite key isn't defined
                if (!string.IsNullOrEmpty(slowTickVfxKey) && _frostbiteVfxTimer <= 0f)
                {
                    var pos = statusVfxAnchor != null ? statusVfxAnchor.position : transform.position;
                    TR.VFX.ParticleManager.SpawnOneShot(slowTickVfxKey, pos);
                    _frostbiteVfxTimer = Mathf.Max(0.05f, slowTickVfxInterval);
                }
            }

            // After processing DoTs, attempt regeneration if ability is enabled
            TryTickRegeneration(dt);
        }

        private void TryTickRegeneration(float dt)
        {
            if (definition == null || !definition.UseRegenAbility) return;
            if (currentHealth <= 0f) return;
            // Suppress shortly after taking any damage
            float suppress = definition.RegenSuppressAfterDamageSeconds;
            if (Time.time - _lastDamageTime < suppress) return;

            // Lifetime cap
            float lifeCap = Mathf.Clamp01(definition.RegenTotalPercentCap) * MaxHealth;
            if (_totalRegenThisLife >= lifeCap - 1e-4f) return;

            // DoT interaction
            bool anyDot = (_burnTime > 0f && _burnDps > 0f) || (_poisonTime > 0f && _poisonDps > 0f) || (_frostbiteTime > 0f && _frostbiteDps > 0f);
            float regenMult = 1f;
            if (anyDot)
            {
                if (!definition.RegenWhileDoT) return; // fully suppressed by DoTs
                regenMult *= Mathf.Clamp01(definition.RegenDoTPenaltyMultiplier);
            }

            // Compute regen rate (per second) in absolute HP using caps
            float baseRate = Mathf.Max(0f, definition.RegenPerSecondBase) * MaxHealth;
            float missingFrac = Mathf.Clamp01((MaxHealth - currentHealth) / Mathf.Max(1f, MaxHealth));
            float missingBonus = Mathf.Max(0f, definition.RegenMissingHealthFactor) * missingFrac * MaxHealth;
            float perSec = (baseRate + missingBonus) * regenMult;
            float perSecCap = Mathf.Max(0f, definition.RegenPerSecondCap) * MaxHealth;
            if (perSecCap > 0f) perSec = Mathf.Min(perSec, perSecCap);
            if (perSec <= 1e-5f) return;

            float delta = perSec * dt;
            // Respect lifetime total cap
            float remainingCap = Mathf.Max(0f, lifeCap - _totalRegenThisLife);
            if (delta > remainingCap) delta = remainingCap;
            if (delta <= 0f) return;

            float newHp = Mathf.Min(MaxHealth, currentHealth + delta);
            float actual = newHp - currentHealth;
            if (actual <= 0f) return;
            currentHealth = newHp;
            _totalRegenThisLife += actual;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
            // Optional VFX ping for regen
            string key = definition.RegenVfxKey;
            if (!string.IsNullOrEmpty(key))
            {
                var pos = statusVfxAnchor != null ? statusVfxAnchor.position : transform.position;
                TR.VFX.ParticleManager.SpawnOneShot(key, pos);
            }
        }

        public void ApplyBurn(float dps, float duration)
        {
            if (dps <= 0f || duration <= 0f) return;
            _burnDps += Mathf.Max(0f, dps);
            _burnTime = Mathf.Max(_burnTime, duration);
        }

        public void ApplyPoison(float dps, float duration)
        {
            if (dps <= 0f || duration <= 0f) return;
            _poisonDps += Mathf.Max(0f, dps);
            _poisonTime = Mathf.Max(_poisonTime, duration);
        }

        public void ApplySlow(float percent, float duration)
        {
            percent = Mathf.Clamp(percent, 0f, 0.95f);
            if (percent <= 0f || duration <= 0f) return;
            _slowPercent = Mathf.Max(_slowPercent, percent);
            // Duration stacks additively; strength does not (keep max percent)
            _slowTime += duration;
            // Immediate slow VFX on application (optional)
            if (!string.IsNullOrEmpty(slowTickVfxKey))
            {
                var pos = statusVfxAnchor != null ? statusVfxAnchor.position : transform.position;
                TR.VFX.ParticleManager.SpawnOneShot(slowTickVfxKey, pos);
                _slowVfxTimer = Mathf.Max(0.05f, slowTickVfxInterval);
            }
        }

        public void TakeDamage(float amount)
        {
            amount = Mathf.Max(0f, amount);
            // Default to Normal damage if no type specified
            TakeDamage(amount, DamageType.Normal);
        }

        public void TakeDamage(float amount, DamageType type)
        {
            amount = Mathf.Max(0f, amount);
            // Clamp the EFFECTIVE damage to remaining health so the visual number doesn't exceed target HP
            float effective = Mathf.Min(amount, Mathf.Max(0f, currentHealth));
            currentHealth -= effective;
            OnHealthChanged?.Invoke(Mathf.Max(0f, currentHealth), MaxHealth);
            // Mark last time damaged to suppress regen
            if (effective > 0f) _lastDamageTime = Time.time;
            // Floating damage numbers (stacking handled by manager)
            float display = TR.UI.DamageNumbers.ClampDisplayed ? effective : amount;
            if (display > 0)
            {
                DamageNumbers.ShowFloat(transform, display, type);
            }
            // Hit VFX (optional) on any non-zero effective damage, rate-limited
            if (effective > 0f && !string.IsNullOrEmpty(hitVfxKey))
            {
                if (Time.time >= _nextHitVfxTime)
                {
                    var pos = hitVfxAnchor != null ? hitVfxAnchor.position : transform.position;
                    ParticleManager.SpawnOneShot(hitVfxKey, pos);
                    _nextHitVfxTime = Time.time + Mathf.Max(0.01f, hitVfxInterval);
                }
            }
            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        // Status damage uses a separate VFX throttle to avoid spamming the regular hit VFX
        public void TakeDamageFromStatus(float amount, DamageType type)
        {
            amount = Mathf.Max(0f, amount);
            float effective = Mathf.Min(amount, Mathf.Max(0f, currentHealth));
            currentHealth -= effective;
            OnHealthChanged?.Invoke(Mathf.Max(0f, currentHealth), MaxHealth);
            if (effective > 0f) _lastDamageTime = Time.time;
            float display = TR.UI.DamageNumbers.ClampDisplayed ? effective : amount;
            if (display > 0)
            {
                DamageNumbers.ShowFloat(transform, display, type);
            }
            if (effective > 0f && !string.IsNullOrEmpty(hitVfxKey))
            {
                if (Time.time >= _nextStatusHitVfxTime)
                {
                    var pos = hitVfxAnchor != null ? hitVfxAnchor.position : transform.position;
                    ParticleManager.SpawnOneShot(hitVfxKey, pos);
                    _nextStatusHitVfxTime = Time.time + Mathf.Max(0.05f, statusHitVfxInterval);
                }
            }
            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            // Award match money on kill based on arena tier
            TryAwardKillMoney();
            // Spawn death VFX if configured
            if (!string.IsNullOrEmpty(deathVfxKey))
            {
                var pos = deathVfxAnchor != null ? deathVfxAnchor.position : transform.position;
                ParticleManager.SpawnOneShot(deathVfxKey, pos);
            }
            // Death SFX
            if (!string.IsNullOrEmpty(deathSfxKey) && SFXManager.Instance != null)
            {
                SFXManager.Instance.Play(deathSfxKey);
            }
            Destroy(gameObject);
        }

        private void TryAwardKillMoney()
        {
            if (_arena == null || definition == null) return;
            var tier = _arena.GetTier(definition);
            int min = 0, max = 0;
            switch (tier)
            {
                case ArenaDefinition.EnemyTier.Easy:
                    min = _arena.EasyKillMin; max = _arena.EasyKillMax; break;
                case ArenaDefinition.EnemyTier.Medium:
                    min = _arena.MediumKillMin; max = _arena.MediumKillMax; break;
                case ArenaDefinition.EnemyTier.Hard:
                    min = _arena.HardKillMin; max = _arena.HardKillMax; break;
                case ArenaDefinition.EnemyTier.Boss:
                    min = _arena.BossKillMin; max = _arena.BossKillMax; break;
                default:
                    return;
            }
            if (max < min) { var t = min; min = max; max = t; }
            int amount = Random.Range(min, max + 1);
            if (amount <= 0) return;
            var econ = FindFirstObjectByType<MatchEconomy>(FindObjectsInactive.Include);
            if (econ != null)
            {
                econ.Earn(amount);
            }
        }

        // Public helper: return this enemy's tier in the current arena
        public ArenaDefinition.EnemyTier GetTier()
        {
            if (_arena != null && definition != null) return _arena.GetTier(definition);
            return ArenaDefinition.EnemyTier.Medium;
        }

        private void TickAttackBase()
        {
            if (_stunTime > 0f) return; // stunned enemies cannot attack the base
            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f) return;
            _attackTimer = Mathf.Max(0.05f, attackInterval);
            float baseDmg = definition != null ? definition.DamagePerHit : 1f;
            float dmg = Mathf.Max(0f, baseDmg * Mathf.Max(0.01f, _bossDamageMult));
            var baseObj = FindFirstObjectByType<BaseCastle>(FindObjectsInactive.Include);
            if (baseObj != null)
            {
                baseObj.TakeDamage(Mathf.CeilToInt(dmg));
            }
        }

        // Apply per-spawn boss scaling (only meaningful for bosses)
        public void ApplyBossScaling(float healthMult, float damageMult, float speedMult)
        {
            // Clamp inputs
            float h = Mathf.Max(0.01f, healthMult);
            float d = Mathf.Max(0.01f, damageMult);
            float s = Mathf.Max(0.01f, speedMult);
            // Scale runtime max HP and reset current health to full
            float baseMax = definition != null ? Mathf.Max(1f, definition.MaxHealth) : Mathf.Max(1f, _runtimeMaxHealth);
            _runtimeMaxHealth = Mathf.Max(1f, baseMax * h);
            currentHealth = _runtimeMaxHealth;
            // Scale move speed multiplicatively
            float baseSpeed = definition != null ? Mathf.Max(0f, definition.MovementSpeed) : Mathf.Max(0f, moveSpeed);
            moveSpeed = Mathf.Max(0f, baseSpeed * s);
            // Damage multiplier applied at attack time
            _bossDamageMult = d;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        private void TrySpawnHealthBarUI()
        {
            if (healthBarUIPrefab == null) return;
            var parent = healthBarCanvas != null ? healthBarCanvas.transform : null;
            _healthBarInstance = Instantiate(healthBarUIPrefab, parent);
            var ui = _healthBarInstance.GetComponent<EnemyHealthBarUI>();
            if (ui != null)
            {
                ui.Bind(this, healthBarOffset);
            }
            else
            {
                Debug.LogWarning("[EnemyBase2D] HealthBar UI prefab missing EnemyHealthBarUI component.");
            }
        }

        private bool IsBoss()
        {
            return _arena != null && definition != null && _arena.GetTier(definition) == ArenaDefinition.EnemyTier.Boss;
        }

        private Canvas FindOverlayCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
            }
            return null;
        }

        // === Public Status API ===
        public bool IsStunned => _stunTime > 0f;
        public float GetCurrentSlowPercent() => Mathf.Clamp01(_slowPercent);
        public void ExtendSlow(float extraSeconds)
        {
            extraSeconds = Mathf.Max(0f, extraSeconds);
            if (extraSeconds <= 0f) return;
            _slowTime += extraSeconds;
        }
        public void ApplyFrostbite(float dps, float duration)
        {
            dps = Mathf.Max(0f, dps);
            duration = Mathf.Max(0f, duration);
            if (dps <= 0f || duration <= 0f) return;
            _frostbiteDps += dps;
            _frostbiteTime = Mathf.Max(_frostbiteTime, duration);
        }
        public void ApplyStun(float duration)
        {
            duration = Mathf.Max(0f, duration);
            if (duration <= 0f) return;
            // Stuns stack by extending time
            _stunTime = Mathf.Max(_stunTime, duration);
            // Optional immediate VFX ping
            if (!string.IsNullOrEmpty(stunTickVfxKey))
            {
                var pos = statusVfxAnchor != null ? statusVfxAnchor.position : transform.position;
                TR.VFX.ParticleManager.SpawnOneShot(stunTickVfxKey, pos);
                _stunVfxTimer = Mathf.Max(0.05f, stunTickVfxInterval);
            }
        }
    }
}
