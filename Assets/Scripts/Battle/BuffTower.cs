using System.Collections.Generic;
using UnityEngine;
using TR.Data;

namespace TR.Battle
{
    // Buff tower: applies percentage buffs to nearby allied towers. Does no damage.
    public class BuffTower : MonoBehaviour
    {
        private BuffCardDefinition _def;
        private int _level;
        private float _range;
        private float _maxHp;
        private float _hp;
        private float _decayPerSec;
        private readonly List<TowerBase> _snapshot = new();
        private readonly HashSet<TowerBase> _buffed = new();
        private bool _dying; // guard to prevent double-destroy and disable Update logic during despawn

        [Header("HP Ring (World-Space)")]
        [SerializeField] private float ringRadius = 0.6f;
        [SerializeField] private float ringThickness = 0.06f;
        [SerializeField] private int ringSegments = 64;
        [SerializeField] private Vector3 ringLocalOffset = new Vector3(0f, 0.35f, 0f);
        private RadialProgressRing _ring;
        public BuffCardDefinition Definition => _def;

        [Header("VFX")]
        [SerializeField] private string auraVfxKey = ""; // optional looping aura at tower
        [SerializeField] private Transform auraAnchor;
        private ParticleSystem _auraVfx;
        [Tooltip("If true, automatically scales the aura particle to match the buff range.")]
        [SerializeField] private bool autoScaleAuraToRange = true;
        [Tooltip("Multiplier applied to computed scale to fit your authored particle (tweak to match visuals).\nFinal scale = buffRange * auraScaleMultiplier")]
        [SerializeField] private float auraScaleMultiplier = 1.0f;

        public void Initialize(BuffCardDefinition def, int level)
        {
            _def = def;
            _level = Mathf.Max(1, level);
            _range = def.GetBuffRange(_level);
            _maxHp = def.GetMaxHealth(_level);
            _hp = _maxHp;
            _decayPerSec = def.GetDecayPerSecond(_level);
            TrySpawnAura();
            ApplyAuraScaleFromRange();
            // Create HP ring child
            if (_ring == null)
            {
                var go = new GameObject("HP_Ring");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = ringLocalOffset;
                _ring = go.AddComponent<RadialProgressRing>();
                _ring.Radius = ringRadius;
                _ring.Thickness = ringThickness;
                _ring.Segments = ringSegments;
                _ring.SetProgress(1f);
            }
        }

        private void OnDisable()
        {
            // Remove buffs from all previously buffed towers
            if (_buffed.Count > 0)
            {
                var list = new List<TowerBase>(_buffed);
                for (int i = 0; i < list.Count; i++)
                {
                    var tb = list[i];
                    if (tb != null)
                    {
                        tb.RemoveBuff(this);
                        tb.RemoveBuffGlowRef(this);
                        if (_def != null && _def.BuffEconomyIncome)
                        {
                            var econ = tb.GetComponent<EconomyTower>();
                            if (econ != null) econ.RemoveIncomeBuff(this);
                        }
                    }
                }
                _buffed.Clear();
            }
            TryReleaseAura();
        }

        private void Update()
        {
            if (_def == null) return;
            if (_dying) return; // skip logic while playing despawn
            float dt = Time.deltaTime;
            // Ensure aura keeps playing if present
            if (_auraVfx != null && !_auraVfx.isPlaying)
            {
                _auraVfx.Play(true);
            }
            // Scan towers in range
            _snapshot.Clear();
            foreach (var t in TowerBase.All) _snapshot.Add((TowerBase)t);

            // Compute multipliers from def
            float dpsMul = _def.BuffDps ? (1f + _def.GetDpsPercent(_level)) : 1f;
            float frMul = _def.BuffFireRate ? (1f + _def.GetFireRatePercent(_level)) : 1f;
            float rgMul = _def.BuffRange ? (1f + _def.GetRangePercent(_level)) : 1f;
            float spMul = _def.BuffSplash ? (1f + _def.GetSplashPercent(_level)) : 1f;
            // On-hit effect multipliers
            float burnDpsMul = _def.BuffBurn ? (1f + _def.GetBurnDpsBuffPercent(_level)) : 1f;
            float burnDurMul = _def.BuffBurn ? (1f + _def.GetBurnDurBuffPercent(_level)) : 1f;
            float poisonDpsMul = _def.BuffPoison ? (1f + _def.GetPoisonDpsBuffPercent(_level)) : 1f;
            float poisonDurMul = _def.BuffPoison ? (1f + _def.GetPoisonDurBuffPercent(_level)) : 1f;
            float slowPctMul = _def.BuffSlow ? (1f + _def.GetSlowPercentBuffPercent(_level)) : 1f;
            float slowDurMul = _def.BuffSlow ? (1f + _def.GetSlowDurBuffPercent(_level)) : 1f;
            float stunChanceMul = _def.BuffStun ? (1f + _def.GetStunChanceBuffPercent(_level)) : 1f;
            float stunDurMul = _def.BuffStun ? (1f + _def.GetStunDurBuffPercent(_level)) : 1f;
            // Economy income multiplier
            float econIncomeMul = _def.BuffEconomyIncome ? (1f + _def.GetEconomyIncomePercent(_level)) : 1f;

            var newlyBuffed = new HashSet<TowerBase>();
            for (int i = 0; i < _snapshot.Count; i++)
            {
                var t = _snapshot[i];
                if (t == null || t.gameObject == this.gameObject) continue; // skip self
                // Use this tower's position as center
                float d = Vector2.Distance((Vector2)transform.position, (Vector2)t.transform.position);
                if (d <= _range)
                {
                    // Check rarity filter
                    if (_def.ShouldAffect(t.Definition))
                    {
                        newlyBuffed.Add(t);
                        t.AddOrUpdateBuffExtended(this,
                            dpsMul, frMul, rgMul, spMul,
                            burnDpsMul, burnDurMul,
                            poisonDpsMul, poisonDurMul,
                            slowPctMul, slowDurMul,
                            stunChanceMul, stunDurMul);
                        // Visual glow enter
                        t.AddBuffGlowRef(this);
                        // Economy towers: apply income buff if enabled
                        if (_def.BuffEconomyIncome)
                        {
                            var econ = t.GetComponent<EconomyTower>();
                            if (econ == null)
                            {
                                // some economy might not derive from TowerBase; also search registry nearby
                                var maybe = TR.Battle.EconomyTower.All;
                                // No positional match needed since t is a tower base for economy towers too, but try component first
                            }
                            if (econ != null)
                            {
                                econ.AddOrUpdateIncomeBuff(this, econIncomeMul);
                            }
                        }
                    }
                }
            }

            // Remove buff from towers that left range
            if (_buffed.Count > 0)
            {
                var prev = new List<TowerBase>(_buffed);
                for (int i = 0; i < prev.Count; i++)
                {
                    var t = prev[i];
                    if (t == null || !newlyBuffed.Contains(t))
                    {
                        if (t != null)
                        {
                            t.RemoveBuff(this);
                            // Visual glow exit
                            t.RemoveBuffGlowRef(this);
                            // Remove income buff if it was applied
                            if (_def.BuffEconomyIncome)
                            {
                                var econ = t.GetComponent<EconomyTower>();
                                if (econ != null) econ.RemoveIncomeBuff(this);
                            }
                        }
                        _buffed.Remove(t);
                    }
                }
            }
            // Add any newly buffed towers to tracking set
            foreach (var t in newlyBuffed) _buffed.Add(t);

            // Decay HP and update ring
            if (_decayPerSec > 0f)
            {
                _hp -= _decayPerSec * dt;
                if (_ring != null)
                {
                    _ring.SetProgress(_maxHp > 0f ? _hp / _maxHp : 0f);
                }
                if (_hp <= 0f)
                {
                    _hp = 0f;
                    if (!_dying) StartCoroutine(DespawnAndDestroy());
                }
            }
        }

        private System.Collections.IEnumerator DespawnAndDestroy()
        {
            _dying = true;
            // Remove buffs immediately so gameplay reflects tower gone
            if (_buffed.Count > 0)
            {
                var list = new List<TowerBase>(_buffed);
                for (int i = 0; i < list.Count; i++)
                {
                    var tb = list[i];
                    if (tb != null)
                    {
                        tb.RemoveBuff(this);
                        tb.RemoveBuffGlowRef(this);
                        if (_def != null && _def.BuffEconomyIncome)
                        {
                            var econ = tb.GetComponent<EconomyTower>();
                            if (econ != null) econ.RemoveIncomeBuff(this);
                        }
                    }
                }
                _buffed.Clear();
            }
            // Stop aura VFX
            TryReleaseAura();
            // Fade out sprites and shrink slightly
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            var startColors = new System.Collections.Generic.Dictionary<SpriteRenderer, Color>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                startColors[renderers[i]] = renderers[i].color;
            }
            Vector3 startScale = transform.localScale;
            Vector3 endScale = startScale * 0.7f;
            float u = 0f;
            const float dur = 0.35f;
            while (u < 1f)
            {
                u += Time.deltaTime / Mathf.Max(0.01f, dur);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u));
                foreach (var kv in startColors)
                {
                    var sr = kv.Key; if (sr == null) continue;
                    var c = kv.Value; c.a = Mathf.Lerp(c.a, 0f, e); sr.color = c;
                }
                transform.localScale = Vector3.Lerp(startScale, endScale, e);
                yield return null;
            }
            Destroy(gameObject);
        }

        private void TrySpawnAura()
        {
            if (string.IsNullOrEmpty(auraVfxKey)) return;
            if (_auraVfx != null) return;
            var pos = auraAnchor != null ? auraAnchor.position : transform.position;
            var parent = auraAnchor != null ? auraAnchor : transform;
            _auraVfx = TR.VFX.ParticleManager.Spawn(auraVfxKey, pos, Quaternion.identity, parent, true);
            if (_auraVfx != null)
            {
                var main = _auraVfx.main;
                main.loop = true;
                _auraVfx.Play(true);
                ApplyAuraScaleFromRange();
            }
        }
        private void TryReleaseAura()
        {
            if (_auraVfx == null) return;
            _auraVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _auraVfx.gameObject.SetActive(false);
            _auraVfx = null;
        }

        private void ApplyAuraScaleFromRange()
        {
            if (!autoScaleAuraToRange) return;
            if (_auraVfx == null) return;
            float range = _range > 0f ? _range : (_def != null ? _def.GetBuffRange(Mathf.Max(1, _level)) : 0f);
            float s = Mathf.Max(0f, range * Mathf.Max(0f, auraScaleMultiplier));
            if (s <= 0f) return;
            var ps = _auraVfx;
            if (ps != null)
            {
                var sh = ps.shape;
                if (sh.enabled)
                {
                    sh.radius = s;
                    return;
                }
            }
            // Fallback: uniform transform scale
            _auraVfx.transform.localScale = new Vector3(s, s, s);
        }

        private void OnDrawGizmosSelected()
        {
            if (_def == null) return;
            float r = _range > 0f ? _range :  _def.GetBuffRange(Mathf.Max(1, _level));
            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, r);
        }
    }
}
