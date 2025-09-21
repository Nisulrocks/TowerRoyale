using UnityEngine;
using System.Collections.Generic;
using TR.Data;
using TR.VFX;
using TR.Audio;

namespace TR.Battle
{
    // Tower runtime logic: reads stats from CardDefinition to attack enemies.
    public class TowerBase : MonoBehaviour
    {
        // Global registry for selection
        private static readonly System.Collections.Generic.HashSet<TowerBase> s_all = new();
        public static System.Collections.Generic.IReadOnlyCollection<TowerBase> All => s_all;
        [SerializeField] private CardDefinition definition;
        [SerializeField] private int level = 1;

        private TR.Data.TowerStats _stats;
        private float _fireCooldown;
        private EnemyBase2D _lastTarget;
        private RangeRing _rangeRing;
        // Aggregated buff multipliers (defaults 1)
        private float _dpsMul = 1f, _fireRateMul = 1f, _rangeMul = 1f, _splashMul = 1f;
        private float _burnDpsMul = 1f, _burnDurMul = 1f, _poisonDpsMul = 1f, _poisonDurMul = 1f, _slowPctMul = 1f, _slowDurMul = 1f;
        private float _stunChanceMul = 1f, _stunDurMul = 1f;
        private static readonly List<EnemyBase2D> _enemySnapshot = new List<EnemyBase2D>(64);
        [SerializeField] private bool disableCombat = false; // when true, Update() will not attack (used by specialized towers)
        // Target ignore timers for move-on-after-effect behavior
        private readonly System.Collections.Generic.Dictionary<EnemyBase2D, float> _ignoreTimers = new System.Collections.Generic.Dictionary<EnemyBase2D, float>();
        // Runtime buff multipliers (aggregated from BuffTower auras)
        private class BuffEntry
        {
            public float dps = 1f, fireRate = 1f, range = 1f, splash = 1f;
            public float burnDps = 1f, burnDur = 1f, poisonDps = 1f, poisonDur = 1f, slowPct = 1f, slowDur = 1f;
            public float stunChance = 1f, stunDur = 1f;
        }

        private bool _lastCrit;
        private float ApplyCrit(float baseDamage)
        {
            if (definition == null) return baseDamage;
            float chance = Mathf.Clamp01(definition.GetCritChance(level));
            float mult = Mathf.Max(1f, definition.GetCritMultiplier(level));
            if (chance <= 0f || mult <= 1f) return baseDamage;
            bool crit = Random.value <= chance;
            _lastCrit = crit;
            return crit ? baseDamage * mult : baseDamage;
        }
        private readonly System.Collections.Generic.Dictionary<UnityEngine.Object, BuffEntry> _buffs = new();
        public System.Action OnBuffsChanged;
        [Header("VFX")]
        [Tooltip("ParticleManager key for a looping idle effect (e.g., Inferno tower flame). Leave empty to disable.")]
        [SerializeField] private string idleVfxKey = "";
        [Tooltip("Optional anchor for idle VFX. If null, uses tower transform.")]
        [SerializeField] private Transform idleVfxAnchor;
        private ParticleSystem _idleVfx;
        [Tooltip("ParticleManager key for a muzzle flash when firing. Leave empty to disable.")]
        [SerializeField] private string muzzleFlashVfxKey = "";
        [Tooltip("Optional anchor for muzzle flash (fire point). If null, uses tower transform.")]
        [SerializeField] private Transform muzzleFlashAnchor;
        [Tooltip("Optional ParticleManager key to spawn at projectile impact")]
        [SerializeField] private string projectileImpactVfxKey = "";

        // ===== Visual Buff Glow (no particles) =====
        [Header("Buff Glow (Optional)")]
        [Tooltip("If enabled by a BuffTower, the tower's sprite will be tinted towards this color.")]
        [SerializeField] private Color buffGlowColor = new Color(0.2f, 1f, 0.6f, 1f);
        [Range(0f, 1f)] [SerializeField] private float buffGlowIntensity = 0.35f; // how strong to lerp towards glow color
        [Tooltip("Speed of the glow pulse animation (cycles per second)")]
        [SerializeField] private float buffGlowPulseSpeed = 2.5f;
        [Range(0f, 1f)] [SerializeField] private float buffGlowPulseAmplitude = 0.25f; // extra intensity added/removed by pulse
        private readonly HashSet<object> _glowSources = new HashSet<object>();
        private SpriteRenderer[] _cachedRenderers;
        private System.Collections.Generic.Dictionary<SpriteRenderer, Color> _origColors;
        private bool _glowActive;
        // Enemy-applied stun: disables attacking while active
        private float _stunTimeFromEnemy;

        // Towers can override this to opt-out of visual buff glow entirely
        protected virtual bool SupportsBuffGlow() => true;

        public CardDefinition Definition => definition;
        public int Level => level;
        public TR.Data.TowerStats Stats => _stats;
        private int _placementCost;

        public void Initialize(CardDefinition def, int lv)
        {
            definition = def;
            level = Mathf.Max(1, lv);
            _stats = definition.GetStatsForLevel(level);
            _placementCost = _stats.cost;
            // If OnEnable ran before definition was set, we may have skipped spawning idle VFX; try now
            TrySpawnIdleVfx();
        }

        public void SetCombatEnabled(bool enabled)
        {
            disableCombat = !enabled;
        }

        public bool ApplyOnHitEffects(EnemyBase2D enemy)
        {
            if (definition == null || enemy == null) return false;
            // Read effect values from CardDefinition optional curves
            float burnDps = definition.GetBurnDps(level) * _burnDpsMul;
            float burnDur = definition.GetBurnDuration(level) * _burnDurMul;
            bool appliedAny = false;
            if (burnDps > 0f && burnDur > 0f)
            {
                enemy.ApplyBurn(burnDps, burnDur);
                appliedAny = true;
                var k = definition.GetSfxBurnApplyKey(); if (!string.IsNullOrEmpty(k)) SFXManager.Instance?.Play(k);
            }
            float poisonDps = definition.GetPoisonDps(level) * _poisonDpsMul;
            float poisonDur = definition.GetPoisonDuration(level) * _poisonDurMul;
            if (poisonDps > 0f && poisonDur > 0f)
            {
                enemy.ApplyPoison(poisonDps, poisonDur);
                appliedAny = true;
                var k = definition.GetSfxPoisonApplyKey(); if (!string.IsNullOrEmpty(k)) SFXManager.Instance?.Play(k);
            }
            // Universal slow-on-hit (optional)
            if (definition.HasSlowOnHit())
            {
                float sp = definition.GetSlowPercent(level) * _slowPctMul;
                float sd = definition.GetSlowDuration(level) * _slowDurMul;
                if (sp > 0f && sd > 0f)
                {
                    enemy.ApplySlow(sp, sd);
                    appliedAny = true;
                    var k = definition.GetSfxSlowApplyKey(); if (!string.IsNullOrEmpty(k)) SFXManager.Instance?.Play(k);
                }
            }
            // Frostbite DoT: apply if card has both Slow and Frostbite toggles (no threshold)
            if (definition.HasSlowOnHit() && definition.HasFrostbiteOnHit())
            {
                float fbDps = Mathf.Max(0f, definition.GetFrostbiteDps(level));
                float fbDur = Mathf.Max(0f, definition.GetFrostbiteDuration(level));
                if (fbDps > 0f && fbDur > 0f)
                {
                    enemy.ApplyFrostbite(fbDps, fbDur);
                    appliedAny = true;
                }
            }
            // Stun-on-hit (optional, affected by buffs via BuffTower)
            bool stunApplied = false;
            if (definition.HasStunOnHit())
            {
                float chance = Mathf.Clamp01(definition.GetStunChance(level) * Mathf.Max(0f, _stunChanceMul));
                float dur = Mathf.Max(0f, definition.GetStunDuration(level) * Mathf.Max(0f, _stunDurMul));
                if (chance > 0f && dur > 0f && Random.value <= chance)
                {
                    enemy.ApplyStun(dur);
                    stunApplied = true;
                    var k = definition.GetSfxStunApplyKey(); if (!string.IsNullOrEmpty(k)) SFXManager.Instance?.Play(k);
                }
            }
            return appliedAny || stunApplied;
        }

        // Ricochet chain helper: chains damage to N enemies behind the main hit, with per-jump falloff
        public void TryDoChainRicochet(EnemyBase2D first, Vector3 sourcePos, float baseDamage)
        {
            if (definition == null || first == null) return;
            if (!definition.HasChainOnHit()) return;
            int maxJumps = definition.GetChainMaxJumps(level);
            float falloff = definition.GetChainFalloffPerJump(level);
            if (maxJumps <= 0 || falloff < 0f) return;

            // Nearest chaining: no directional filter, just chain to the nearest valid enemy each jump

            var visited = new System.Collections.Generic.HashSet<EnemyBase2D>();
            visited.Add(first);
            var current = first;
            float damage = ApplyCrit(baseDamage); // roll once for the initial ricochet damage
            Color zapCol = definition.GetChainZapColor();

            for (int j = 0; j < maxJumps; j++)
            {
                EnemyBase2D best = null;
                float bestDist = float.MaxValue;
                Vector3 curPos = current.transform.position;
                foreach (var e in EnemyBase2D.All)
                {
                    if (e == null || e == current || visited.Contains(e)) continue;
                    if (!e.gameObject.activeInHierarchy || e.CurrentHealth <= 0f) continue;
                    Vector3 to = e.transform.position - curPos;
                    float d = to.magnitude;
                    if (d <= 0.0001f) continue;
                    if (d < bestDist)
                    {
                        bestDist = d; best = e;
                    }
                }
                if (best == null) break;

                // Apply damage and (optionally) on-hit effects to chained target
                best.TakeDamage(damage);
                if (definition.GetChainTransfersOnHitEffects())
                {
                    ApplyOnHitEffects(best);
                }
                var chainKey = definition.GetSfxChainJumpKey(); if (!string.IsNullOrEmpty(chainKey)) TR.Audio.SFXManager.Instance?.Play(chainKey);
                {
                    var mat = definition.GetForceDefaultZapMaterial() ? null : definition.GetZapMaterial();
                    bool glowOn = definition.GetChainGlowEnabled();
                    float glow = definition.GetChainGlowBoost();
                    if (mat != null)
                    {
                        TR.Battle.LightningZap.Spawn(curPos,
                                                     best.transform.position,
                                                     definition.GetChainZapDurationOrFallback(),
                                                     definition.GetChainZapWidthOrFallback(),
                                                     definition.GetChainZapJitterOrFallback(),
                                                     definition.GetChainZapSegmentsOrFallback(),
                                                     zapCol,
                                                     mat,
                                                     glowOn,
                                                     glow);
                    }
                    else
                    {
                        TR.Battle.LightningZap.Spawn(curPos,
                                                     best.transform.position,
                                                     definition.GetChainZapDurationOrFallback(),
                                                     definition.GetChainZapWidthOrFallback(),
                                                     definition.GetChainZapJitterOrFallback(),
                                                     definition.GetChainZapSegmentsOrFallback(),
                                                     zapCol,
                                                     glowOn,
                                                     glow);
                    }
                }
                var zapHitKey = definition.GetSfxZapHitKey(); if (!string.IsNullOrEmpty(zapHitKey)) TR.Audio.SFXManager.Instance?.Play(zapHitKey);
                visited.Add(best);
                current = best;
                damage *= falloff;
                if (damage <= 0.01f) break;
            }
        }

        private void OnEnable()
        {
            s_all.Add(this);
            // Note: UI is handled elsewhere; no health bar spawn here for towers.
            // Ensure selection component exists on all towers at runtime
            if (GetComponent<TowerSelectable>() == null)
            {
                gameObject.AddComponent<TowerSelectable>();
            }
            // Spawn idle VFX if configured
            TrySpawnIdleVfx();
            // Cache sprite renderers for glow
            if (_cachedRenderers == null)
            {
                _cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }
        }

        private void OnDisable()
        {
            s_all.Remove(this);
            // Cleanup idle VFX back to pool
            TryReleaseIdleVfx();
            // Ensure glow is cleared
            if (_glowSources.Count > 0)
            {
                _glowSources.Clear();
                ApplyGlow(false);
            }
            _stunTimeFromEnemy = 0f;
        }

        private void Update()
        {
            // Always animate glow if active, regardless of combat state
            if (_glowActive)
            {
                UpdateGlowPulse();
            }
            // Tick enemy-applied stun timer
            if (_stunTimeFromEnemy > 0f)
            {
                _stunTimeFromEnemy -= Time.deltaTime;
                if (_stunTimeFromEnemy < 0f) _stunTimeFromEnemy = 0f;
            }
            if (definition == null) return;
            if (disableCombat) return;
            // If stunned by enemy pulse, block combat actions
            if (_stunTimeFromEnemy > 0f) return;

            // Tick ignore timers
            if (_ignoreTimers.Count > 0)
            {
                var keys = new System.Collections.Generic.List<EnemyBase2D>(_ignoreTimers.Keys);
                float dt = Time.deltaTime;
                for (int i = 0; i < keys.Count; i++)
                {
                    var k = keys[i];
                    if (k == null || !k.gameObject.activeInHierarchy || k.CurrentHealth <= 0f)
                    {
                        _ignoreTimers.Remove(k);
                        continue;
                    }
                    float t = _ignoreTimers[k] - dt;
                    if (t <= 0f) _ignoreTimers.Remove(k); else _ignoreTimers[k] = t;
                }
            }

            // Acquire target for rotation every frame so rotation is smooth even during cooldown
            EnemyBase2D targetForAim = null;
            if (definition.ShouldRotateToTarget())
            {
                targetForAim = AcquireTarget();
                if (targetForAim != null)
                {
                    Vector3 to = (Vector3)targetForAim.transform.position - transform.position;
                    if (to.sqrMagnitude > 1e-6f)
                    {
                        // Front is the bottom of the sprite => use Vector3.down as the forward reference
                        Quaternion desired = Quaternion.FromToRotation(Vector3.down, to.normalized);
                        float speed = definition.GetRotateSpeedDegPerSec();
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, speed * Time.deltaTime);
                    }
                }
            }

            // Cooldown ticking
            if (_fireCooldown > 0f) _fireCooldown -= Time.deltaTime;
            if (_fireCooldown > 0f) return;

            // If this tower has no DPS (e.g., economy/support towers), do not attempt to attack
            if (_stats.dps <= 1e-4f)
            {
                return;
            }

            // Reuse the aimed target if available, otherwise acquire now
            var target = targetForAim != null ? targetForAim : AcquireTarget();
            if (target == null) return;

            // If rotation-to-target is enabled, only fire when within tolerance
            if (definition.ShouldRotateToTarget())
            {
                Vector3 toNow = (Vector3)target.transform.position - transform.position;
                if (toNow.sqrMagnitude > 1e-6f)
                {
                    // Compare with the tower's CURRENT facing; front is its local down axis
                    Vector3 currentForward = -transform.up; // equals transform.down
                    float ang = Vector3.Angle(currentForward, toNow.normalized);
                    const float aimToleranceDeg = 15f; // slightly looser tolerance
                    if (ang > aimToleranceDeg)
                    {
                        return; // wait until we're roughly facing the target
                    }
                }
            }

            FireAt(target);
            float effectiveFireRate = Mathf.Max(0.01f, _stats.fireRate * _fireRateMul);
            _fireCooldown = Mathf.Max(0.01f, 1f / effectiveFireRate);

            // (glow pulse already updated at frame start)
        }

        public int GetCost() => definition != null ? definition.GetStatsForLevel(level).cost : 0;
        public int GetPlacementCost() => _placementCost;

        // === Enemy interaction API ===
        public void ApplyTowerStun(float duration)
        {
            duration = Mathf.Max(0f, duration);
            if (duration <= 0f) return;
            _stunTimeFromEnemy = Mathf.Max(_stunTimeFromEnemy, duration);
        }

        // External read-only access
        public bool IsStunnedByEnemy => _stunTimeFromEnemy > 0f;

        // Refunds a percentage of the original placement cost and destroys the tower
        public void DestroyForRefund(float refundPercent)
        {
            refundPercent = Mathf.Clamp01(refundPercent);
            int refund = Mathf.RoundToInt(_placementCost * refundPercent);
            var econ = FindFirstObjectByType<MatchEconomy>(FindObjectsInactive.Include);
            if (econ != null && refund > 0)
            {
                econ.Earn(refund);
            }
            // Play defeat/destroy VFX & SFX if configured on the card definition
            if (definition != null)
            {
                string vfxKey = definition.GetDefeatDestroyVfxKey();
                if (!string.IsNullOrEmpty(vfxKey))
                {
                    ParticleManager.SpawnOneShot(vfxKey, transform.position);
                }
                string sfxKey = definition.GetDefeatDestroySfxKey();
                if (!string.IsNullOrEmpty(sfxKey) && SFXManager.Instance != null)
                {
                    SFXManager.Instance.Play(sfxKey);
                }
            }
            Destroy(gameObject);
        }

        public void ShowRangeRing(bool show)
        {
            if (!show)
            {
                if (_rangeRing != null) _rangeRing.gameObject.SetActive(false);
                return;
            }
            if (_rangeRing == null)
            {
                var go = new GameObject("RangeRing");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                _rangeRing = go.AddComponent<RangeRing>();
                _rangeRing.Segments = 48;
                _rangeRing.Thickness = 0.05f;
                _rangeRing.Color = new Color(0.2f, 0.8f, 1f, 0.6f);
            }
            float ringRadius = GetEffectiveRange();
            _rangeRing.Radius = ringRadius;
            _rangeRing.gameObject.SetActive(true);
        }

        

        private EnemyBase2D AcquireTarget()
        {
            // Prefer last target if still in range and alive
            if (_lastTarget != null)
            {
                float dist = Vector2.Distance((Vector2)transform.position, (Vector2)_lastTarget.transform.position);
                bool ignored = _ignoreTimers.TryGetValue(_lastTarget, out var tleft) && tleft > 0f;
                if (!ignored && _lastTarget.gameObject.activeInHierarchy && _lastTarget.CurrentHealth > 0f && dist <= _stats.range * _rangeMul)
                    return _lastTarget;
            }
            EnemyBase2D best = null;
            float maxRange = _stats.range * _rangeMul;
            // Take a stable snapshot to avoid collection-modified exceptions
            _enemySnapshot.Clear();
            foreach (var e in EnemyBase2D.All) _enemySnapshot.Add(e);

            if (definition != null && definition.FocusOnHighestHp)
            {
                // Select the enemy with the highest current HP within range.
                float bestHp = -1f;
                float bestTieDist = float.MaxValue;
                for (int i = 0; i < _enemySnapshot.Count; i++)
                {
                    var e = _enemySnapshot[i];
                    if (e == null || !e.gameObject.activeInHierarchy || e.CurrentHealth <= 0f) continue;
                    if (_ignoreTimers.TryGetValue(e, out var tleft) && tleft > 0f) continue;
                    float d = Vector2.Distance((Vector2)transform.position, (Vector2)e.transform.position);
                    if (d > maxRange) continue;
                    float hp = e.CurrentHealth;
                    if (hp > bestHp || (Mathf.Approximately(hp, bestHp) && d < bestTieDist))
                    {
                        bestHp = hp;
                        bestTieDist = d;
                        best = e;
                    }
                }
            }
            else
            {
                // Default behavior: nearest within range
                float bestDist = maxRange;
                for (int i = 0; i < _enemySnapshot.Count; i++)
                {
                    var e = _enemySnapshot[i];
                    if (e == null || !e.gameObject.activeInHierarchy || e.CurrentHealth <= 0f) continue;
                    if (_ignoreTimers.TryGetValue(e, out var tleft) && tleft > 0f) continue;
                    float d = Vector2.Distance((Vector2)transform.position, (Vector2)e.transform.position);
                    if (d <= bestDist)
                    {
                        bestDist = d;
                        best = e;
                    }
                }
            }
            _lastTarget = best;
            return best;
        }

        private void FireAt(EnemyBase2D target)
        {
            if (target == null) return;
            // Compute damage per shot from DPS and fireRate
            float effectiveDps = Mathf.Max(0f, _stats.dps * _dpsMul);
            float effectiveFireRate = Mathf.Max(0.01f, _stats.fireRate * _fireRateMul);
            float damagePerShot = effectiveDps / effectiveFireRate;
            // Roll crit once per shot
            _lastCrit = false;
            float shotDamage = ApplyCrit(damagePerShot);
            Vector3 hitPos = target.transform.position;
            bool useZap = definition.UseLightningZapOnHit();
            // Projectile path (if prefab assigned on CardDefinition) and zap override is OFF
            var projPrefab = definition != null ? definition.GetProjectilePrefab() : null;
            float projSpeed = definition != null ? definition.GetProjectileSpeed() : 0f;
            if (!useZap && projPrefab != null)
            {
                TryMuzzleFlash();
                var fireKey = definition.GetSfxFireKey(); if (!string.IsNullOrEmpty(fireKey)) SFXManager.Instance?.Play(fireKey);
                var spawnPos = muzzleFlashAnchor != null ? muzzleFlashAnchor.position : transform.position;
                var go = Instantiate(projPrefab, spawnPos, Quaternion.identity);
                var proj = go.GetComponent<ProjectileSimple>();
                if (proj == null) proj = go.AddComponent<ProjectileSimple>();
                // Allow per-tower override via serialized field; otherwise use definition's key
                string impactKey = !string.IsNullOrEmpty(projectileImpactVfxKey) ? projectileImpactVfxKey : definition.GetProjectileImpactVfxKey();
                proj.Init(target, projSpeed, shotDamage, _stats.splashRadius * _splashMul,
                          this, definition, level, impactKey, _lastCrit);
                // Move-on-after-effect will be handled on impact by the projectile
            }
            else
            {
                TryMuzzleFlash();
                var fireKey = definition.GetSfxFireKey(); if (!string.IsNullOrEmpty(fireKey)) SFXManager.Instance?.Play(fireKey);
                float effectiveSplash = _stats.splashRadius * _splashMul;
                if (effectiveSplash > 0.01f)
                {
                    // Splash damage to all enemies within radius (use same crit roll for the whole shot)
                    float r = effectiveSplash;
                    _enemySnapshot.Clear();
                    foreach (var e in EnemyBase2D.All) _enemySnapshot.Add(e);
                    // Apply on-hit effects to the primary target first so we can detect stun for move-on logic
                    bool stunPrimary = ApplyOnHitEffects(target);
                    // If crit, show burst once at the primary target and play crit SFX
                    if (_lastCrit)
                    {
                        TR.UI.DamageNumbers.ShowCrit(target.transform, definition.GetCritBurstText());
                        var ck = definition.GetSfxCritKey(); if (!string.IsNullOrEmpty(ck)) SFXManager.Instance?.Play(ck);
                    }
                    for (int i = 0; i < _enemySnapshot.Count; i++)
                    {
                        var e = _enemySnapshot[i];
                        if (e == null || !e.gameObject.activeInHierarchy || e.CurrentHealth <= 0f) continue;
                        float d = Vector2.Distance((Vector2)hitPos, (Vector2)e.transform.position);
                        if (d <= r)
                        {
                            e.TakeDamage(shotDamage);
                            if (e != target) ApplyOnHitEffects(e);
                        }
                    }
                    var splashKey = definition.GetSfxSplashKey(); if (!string.IsNullOrEmpty(splashKey)) SFXManager.Instance?.Play(splashKey);
                    // Optional lightning zap override visual for splash: strike center then branch to all enemies hit
                    if (useZap)
                    {
                        // Main strike from tower -> splash center (hitPos)
                        TR.Battle.LightningZap.Spawn(transform.position,
                                                     hitPos,
                                                     definition.GetZapDuration(),
                                                     definition.GetZapWidth(),
                                                     definition.GetZapJitter(),
                                                     definition.GetZapSegments(),
                                                     definition.GetZapColor());
                        var zapFireKey = definition.GetSfxZapFireKey(); if (!string.IsNullOrEmpty(zapFireKey)) TR.Audio.SFXManager.Instance?.Play(zapFireKey);
                        // Branch spokes from center -> each affected enemy
                        float spokeDur = Mathf.Max(0.02f, definition.GetZapDuration() * 0.7f);
                        float spokeWidth = Mathf.Max(0.001f, definition.GetZapWidth() * 0.75f);
                        for (int i = 0; i < _enemySnapshot.Count; i++)
                        {
                            var e = _enemySnapshot[i];
                            if (e == null || !e.gameObject.activeInHierarchy || e.CurrentHealth <= 0f) continue;
                            float d = Vector2.Distance((Vector2)hitPos, (Vector2)e.transform.position);
                            if (d <= r)
                            {
                                TR.Battle.LightningZap.Spawn(hitPos,
                                                             e.transform.position,
                                                             spokeDur,
                                                             spokeWidth,
                                                             definition.GetZapJitter(),
                                                             definition.GetZapSegments(),
                                                             definition.GetZapColor());
                            }
                        }
                        string zapHitVfxKey = definition.GetZapHitVfxKey(); if (!string.IsNullOrEmpty(zapHitVfxKey)) TR.VFX.ParticleManager.SpawnOneShot(zapHitVfxKey, hitPos);
                        var zapHitKey = definition.GetSfxZapHitKey(); if (!string.IsNullOrEmpty(zapHitKey)) TR.Audio.SFXManager.Instance?.Play(zapHitKey);
                    }
                    // Debug ring
                    DebugDrawCircle(hitPos, r, Color.red, 0.15f);
                    // Move-on-after-effect ONLY if the primary target was stunned
                    TryScheduleMoveOnAfterEffect(target, stunPrimary);
                }
                else
                {
                    // Single target
                    target.TakeDamage(shotDamage);
                    if (_lastCrit) TR.UI.DamageNumbers.ShowCrit(target.transform, definition.GetCritBurstText());
                    bool stunned = ApplyOnHitEffects(target);
                    var hitKey = definition.GetSfxHitKey(); if (!string.IsNullOrEmpty(hitKey)) SFXManager.Instance?.Play(hitKey);
                    // Chain ricochet (single-target only)
                    TryDoChainRicochet(target, transform.position, shotDamage);
                    // Optional lightning zap override visual
                    if (useZap)
                    {
                        // Visual only: draw zap from tower to target
                        var mat = definition.GetForceDefaultZapMaterial() ? null : definition.GetZapMaterial();
                        bool glowOn = definition.GetZapGlowEnabled();
                        float glow = definition.GetZapGlowBoost();
                        if (mat != null)
                        {
                            TR.Battle.LightningZap.Spawn(transform.position,
                                                         target.transform.position,
                                                         definition.GetZapDuration(),
                                                         definition.GetZapWidth(),
                                                         definition.GetZapJitter(),
                                                         definition.GetZapSegments(),
                                                         definition.GetZapColor(),
                                                         mat,
                                                         glowOn,
                                                         glow);
                        }
                        else
                        {
                            TR.Battle.LightningZap.Spawn(transform.position,
                                                         target.transform.position,
                                                         definition.GetZapDuration(),
                                                         definition.GetZapWidth(),
                                                         definition.GetZapJitter(),
                                                         definition.GetZapSegments(),
                                                         definition.GetZapColor(),
                                                         glowOn,
                                                         glow);
                        }
                        var zapFireKey = definition.GetSfxZapFireKey(); if (!string.IsNullOrEmpty(zapFireKey)) SFXManager.Instance?.Play(zapFireKey);
                        string zapHitVfxKey = definition.GetZapHitVfxKey();
                        if (!string.IsNullOrEmpty(zapHitVfxKey))
                        {
                            TR.VFX.ParticleManager.SpawnOneShot(zapHitVfxKey, target.transform.position);
                        }
                        var zapHitKey = definition.GetSfxZapHitKey(); if (!string.IsNullOrEmpty(zapHitKey)) SFXManager.Instance?.Play(zapHitKey);
                    }
                    if (definition.HasTornadoOnHit())
                    {
                        float tRad = definition.GetTornadoRadius(level);
                        float tStr = definition.GetTornadoStrength(level);
                        float tDur = definition.GetTornadoDuration(level);
                        if (tRad > 0f && tStr > 0f && tDur > 0f)
                        {
                            int maxTargets = definition.GetTornadoMaxPullTargets();
                            bool allowEasy = definition.TornadoAllowsTier(TR.Data.ArenaDefinition.EnemyTier.Easy);
                            bool allowMedium = definition.TornadoAllowsTier(TR.Data.ArenaDefinition.EnemyTier.Medium);
                            bool allowHard = definition.TornadoAllowsTier(TR.Data.ArenaDefinition.EnemyTier.Hard);
                            bool allowBoss = definition.TornadoAllowsTier(TR.Data.ArenaDefinition.EnemyTier.Boss);
                            string vfxKey = definition.GetTornadoVfxKey();
                            float vfxMul = definition.GetTornadoVfxScaleMultiplier();
                            bool allowCenterStack = definition.GetTornadoAllowCenterStack();
                            float falloffPower = definition.GetTornadoFalloffPower();
                            var tf = TornadoField.Spawn(target.transform.position, tRad, tStr, tDur,
                                               maxTargets, allowEasy, allowMedium, allowHard, allowBoss,
                                               vfxKey, vfxMul,
                                               allowCenterStack, falloffPower);
                            // SFX loop for tornado
                            var tornadoKey = definition.GetSfxTornadoKey();
                            if (tf != null && !string.IsNullOrEmpty(tornadoKey))
                            {
                                tf.SetSfxKey(tornadoKey);
                            }
                        }
                    }
                    TryScheduleMoveOnAfterEffect(target, stunned);
                }

                // Optional debug tracer
                Debug.DrawLine(transform.position, hitPos, Color.yellow, 0.1f);
            }
        }

        public void TryScheduleMoveOnAfterEffect(EnemyBase2D hitTarget, bool stunApplied)
        {
            if (hitTarget == null) return;
            // Only for regular towers; inferno uses its own logic and TowerBase.combat may be disabled
            // Focus-on-highest-HP overrides move-on targeting; keep lock on the highest-HP rather than switching.
            if (definition != null && definition.FocusOnHighestHp) return;
            if (!definition.MoveOnAfterEffect) return;
            // New rule: move on ONLY if a stun was actually applied this hit
            if (!stunApplied) return;
            float ignoreFor = definition.GetMoveOnIgnoreSeconds();
            if (ignoreFor <= 0f) return;
            _ignoreTimers[hitTarget] = Mathf.Max(_ignoreTimers.TryGetValue(hitTarget, out var t) ? t : 0f, ignoreFor);
            // Force drop last target so AcquireTarget finds a new one next frame
            if (_lastTarget == hitTarget) _lastTarget = null;
        }

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

        private void OnDrawGizmosSelected()
        {
            if (definition == null) return;
            var stats = _stats.level > 0 ? _stats : definition.GetStatsForLevel(Mathf.Max(1, level));
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, stats.range * _rangeMul);
        }

        // === Buff API (called by BuffTower) ===
        public void AddOrUpdateBuff(UnityEngine.Object source, float dpsMultiplier, float fireRateMultiplier, float rangeMultiplier, float splashMultiplier)
        {
            if (source == null) return;
            AddOrUpdateBuffExtended(source, dpsMultiplier, fireRateMultiplier, rangeMultiplier, splashMultiplier,
                             1f, 1f, 1f, 1f, 1f, 1f,
                             1f, 1f);
        }

        public void AddOrUpdateBuffExtended(UnityEngine.Object source,
                                    float dpsMultiplier, float fireRateMultiplier, float rangeMultiplier, float splashMultiplier,
                                    float burnDpsMultiplier, float burnDurMultiplier,
                                    float poisonDpsMultiplier, float poisonDurMultiplier,
                                    float slowPercentMultiplier, float slowDurMultiplier,
                                    float stunChanceMultiplier, float stunDurMultiplier)
        {
            if (source == null) return;
            if (!_buffs.TryGetValue(source, out var entry)) entry = new BuffEntry();
            entry.dps = Mathf.Max(0f, dpsMultiplier);
            entry.fireRate = Mathf.Max(0f, fireRateMultiplier);
            entry.range = Mathf.Max(0f, rangeMultiplier);
            entry.splash = Mathf.Max(0f, splashMultiplier);
            entry.burnDps = Mathf.Max(0f, burnDpsMultiplier);
            entry.burnDur = Mathf.Max(0f, burnDurMultiplier);
            entry.poisonDps = Mathf.Max(0f, poisonDpsMultiplier);
            entry.poisonDur = Mathf.Max(0f, poisonDurMultiplier);
            entry.slowPct = Mathf.Max(0f, slowPercentMultiplier);
            entry.slowDur = Mathf.Max(0f, slowDurMultiplier);
            entry.stunChance = Mathf.Max(0f, stunChanceMultiplier);
            entry.stunDur = Mathf.Max(0f, stunDurMultiplier);
            _buffs[source] = entry;
            RecomputeBuffs();
            // Update range ring live
            if (_rangeRing != null && _rangeRing.gameObject.activeSelf)
            {
                _rangeRing.Radius = GetEffectiveRange();
            }
        }

        public void RemoveBuff(UnityEngine.Object source)
        {
            if (source == null) return;
            if (_buffs.Remove(source))
            {
                RecomputeBuffs();
                if (_rangeRing != null && _rangeRing.gameObject.activeSelf)
                {
                    _rangeRing.Radius = GetEffectiveRange();
                }
            }
        }

        private void RecomputeBuffs()
        {
            float dps = 1f, fr = 1f, rg = 1f, sp = 1f;
            float burnDps = 1f, burnDur = 1f, poisonDps = 1f, poisonDur = 1f, slowPct = 1f, slowDur = 1f;
            float stunChance = 1f, stunDur = 1f;
            foreach (var kv in _buffs)
            {
                var e = kv.Value;
                dps *= Mathf.Max(0.01f, e.dps);
                fr *= Mathf.Max(0.01f, e.fireRate);
                rg *= Mathf.Max(0.01f, e.range);
                sp *= Mathf.Max(0.01f, e.splash);
                burnDps *= Mathf.Max(0.01f, e.burnDps);
                burnDur *= Mathf.Max(0.01f, e.burnDur);
                poisonDps *= Mathf.Max(0.01f, e.poisonDps);
                poisonDur *= Mathf.Max(0.01f, e.poisonDur);
                slowPct *= Mathf.Max(0.01f, e.slowPct);
                slowDur *= Mathf.Max(0.01f, e.slowDur);
                stunChance *= Mathf.Max(0.01f, e.stunChance);
                stunDur *= Mathf.Max(0.01f, e.stunDur);
            }
            _dpsMul = dps;
            _fireRateMul = fr;
            _rangeMul = rg;
            _splashMul = sp;
            _burnDpsMul = burnDps;
            _burnDurMul = burnDur;
            _poisonDpsMul = poisonDps;
            _poisonDurMul = poisonDur;
            _slowPctMul = slowPct;
            _slowDurMul = slowDur;
            _stunChanceMul = stunChance;
            _stunDurMul = stunDur;
            OnBuffsChanged?.Invoke();
        }

        // Public read-only accessors for specialized towers
        public float GetDpsMultiplier() => _dpsMul;
        public float GetFireRateMultiplier() => _fireRateMul;
        public float GetRangeMultiplier() => _rangeMul;
        public float GetSplashMultiplier() => _splashMul;
        public float GetBurnDpsMultiplier() => _burnDpsMul;
        public float GetBurnDurMultiplier() => _burnDurMul;
        public float GetPoisonDpsMultiplier() => _poisonDpsMul;
        public float GetPoisonDurMultiplier() => _poisonDurMul;
        public float GetSlowPercentMultiplier() => _slowPctMul;
        public float GetSlowDurMultiplier() => _slowDurMul;
        public float GetStunChanceMultiplier() => _stunChanceMul;
        public float GetStunDurMultiplier() => _stunDurMul;

        // Effective stat helpers (used by hover UI)
        public float GetEffectiveDps() => Mathf.Max(0f, _stats.dps * _dpsMul);
        public float GetEffectiveFireRate() => Mathf.Max(0.01f, _stats.fireRate * _fireRateMul);
        public float GetEffectiveRange()
        {
            if (definition is TR.Data.BuffCardDefinition buffDef) return buffDef.GetBuffRange(level);
            if (definition is TR.Data.PulseCardDefinition pulseDef) return Mathf.Max(0f, pulseDef.GetPulseRadius(level) * _rangeMul);
            return Mathf.Max(0f, _stats.range * _rangeMul);
        }
        public float GetEffectiveSplashRadius() => Mathf.Max(0f, _stats.splashRadius * _splashMul);

        private void TrySpawnIdleVfx()
        {
            // Prefer per-tower override; otherwise use CardDefinition's idle VFX key
            string key = !string.IsNullOrEmpty(idleVfxKey) ? idleVfxKey : (definition != null ? definition.GetIdleVfxKey() : string.Empty);
            if (string.IsNullOrEmpty(key)) return;
            if (_idleVfx != null) return;
            var pos = idleVfxAnchor != null ? idleVfxAnchor.position : transform.position;
            var parent = idleVfxAnchor != null ? idleVfxAnchor : transform;
            _idleVfx = ParticleManager.Spawn(key, pos, Quaternion.identity, parent, true);
            if (_idleVfx != null)
            {
                var main = _idleVfx.main;
                main.loop = true;
                _idleVfx.gameObject.SetActive(true);
                _idleVfx.Play(true);
            }
        }

        private void TryReleaseIdleVfx()
        {
            if (_idleVfx == null) return;
            var pooled = _idleVfx.GetComponent<PooledParticle>();
            if (pooled != null)
            {
                pooled.ForceReturn();
            }
            else
            {
                // Fallback: stop and disable
                _idleVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _idleVfx.gameObject.SetActive(false);
            }
            _idleVfx = null;
        }

        private void TryMuzzleFlash()
        {
            if (string.IsNullOrEmpty(muzzleFlashVfxKey)) return;
            var pos = muzzleFlashAnchor != null ? muzzleFlashAnchor.position : transform.position;
            ParticleManager.SpawnOneShot(muzzleFlashVfxKey, pos);
        }

        // ===== Public API for Buff Glow (called by BuffTower) =====
        public void AddBuffGlowRef(object source)
        {
            if (!SupportsBuffGlow()) return;
            if (source == null) return;
            bool wasActive = _glowSources.Count > 0;
            _glowSources.Add(source);
            if (!wasActive && _glowSources.Count > 0)
            {
                ApplyGlow(true);
            }
        }

        public void RemoveBuffGlowRef(object source)
        {
            if (!SupportsBuffGlow()) return;
            if (source == null) return;
            if (_glowSources.Remove(source) && _glowSources.Count == 0)
            {
                ApplyGlow(false);
            }
        }

        private void ApplyGlow(bool enable)
        {
            if (_cachedRenderers == null) _cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (_cachedRenderers == null || _cachedRenderers.Length == 0) return;
            if (enable)
            {
                // Preserve originals once; do NOT overwrite on subsequent enables, otherwise we lose true base colors
                if (_origColors == null || _origColors.Count == 0)
                {
                    _origColors = new System.Collections.Generic.Dictionary<SpriteRenderer, Color>();
                    for (int i = 0; i < _cachedRenderers.Length; i++)
                    {
                        var sr = _cachedRenderers[i];
                        if (sr == null) continue;
                        _origColors[sr] = sr.color;
                    }
                }
                // Initial application uses base intensity; pulse will animate subsequently
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    var sr = _cachedRenderers[i]; if (sr == null) continue;
                    // Get original for this renderer; if missing, fall back to current then cache it for future restores
                    Color baseCol;
                    if (!_origColors.TryGetValue(sr, out baseCol)) { baseCol = sr.color; _origColors[sr] = baseCol; }
                    Color target = Color.Lerp(baseCol, buffGlowColor, Mathf.Clamp01(buffGlowIntensity));
                    sr.color = target;
                }
                _glowActive = true;
            }
            else
            {
                if (_origColors != null && _origColors.Count > 0)
                {
                    foreach (var kv in _origColors)
                    {
                        if (kv.Key != null) kv.Key.color = kv.Value;
                    }
                }
                _glowActive = false;
            }
        }

        private void UpdateGlowPulse()
        {
            if (_origColors == null || _origColors.Count == 0) return;
            float t = Time.time * Mathf.Max(0f, buffGlowPulseSpeed) * Mathf.PI * 2f;
            // Pulse around the base intensity
            float pulse = (Mathf.Sin(t) * 0.5f + 0.5f) * Mathf.Clamp01(buffGlowPulseAmplitude);
            float lerpAmt = Mathf.Clamp01(buffGlowIntensity + pulse);
            foreach (var kv in _origColors)
            {
                var sr = kv.Key;
                if (sr == null) continue;
                Color baseCol = kv.Value;
                sr.color = Color.Lerp(baseCol, buffGlowColor, lerpAmt);
            }
        }
    }
}
