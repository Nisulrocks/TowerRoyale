using System.Collections.Generic;
using UnityEngine;
using TR.Data;
using TR.Audio;

namespace TR.Battle
{
    
    
    public class InfernoTower : MonoBehaviour
    {
        private InfernoCardDefinition _def;
        private int _level;
        private TowerStats _stats;
        private TowerBase _hostBase; 

        private readonly Dictionary<EnemyBase2D, float> _ramp = new(); 
        private readonly List<EnemyBase2D> _enemySnapshot = new(64);
        private readonly List<EnemyBase2D> _currentTargets = new(8);
        private readonly System.Collections.Generic.Dictionary<EnemyBase2D, BeamController> _beams = new();
        private int _beamSfxHandle = -1;
        
        [SerializeField] private float slowReapplyInterval = 0.5f; 
        private readonly Dictionary<EnemyBase2D, float> _slowCooldown = new();
        
        [SerializeField] private float burnReapplyInterval = 0.5f;
        [SerializeField] private float poisonReapplyInterval = 0.5f;
        [SerializeField] private float stunReapplyInterval = 0.6f;
        private readonly Dictionary<EnemyBase2D, float> _burnCooldown = new();
        private readonly Dictionary<EnemyBase2D, float> _poisonCooldown = new();
        private readonly Dictionary<EnemyBase2D, float> _stunCooldown = new();

        public void Initialize(InfernoCardDefinition def, int level)
        {
            _def = def;
            _level = Mathf.Max(1, level);
            _stats = def.GetStatsForLevel(_level);
            _hostBase = GetComponent<TowerBase>();
        }

        private void OnDisable()
        {
            _ramp.Clear();
            
            if (_beams.Count > 0)
            {
                var vals = new System.Collections.Generic.List<BeamController>(_beams.Values);
                for (int i = 0; i < vals.Count; i++)
                {
                    if (vals[i] != null) Destroy(vals[i].gameObject);
                }
                _beams.Clear();
            }
            _slowCooldown.Clear();
            _burnCooldown.Clear();
            _poisonCooldown.Clear();
            _stunCooldown.Clear();
        }

        private void Update()
        {
            if (_def == null) return;
            
            if (_hostBase != null && _hostBase.IsStunnedByEnemy)
            {
                
                _currentTargets.Clear();
                
                if (_beams.Count > 0)
                {
                    var vals = new System.Collections.Generic.List<BeamController>(_beams.Values);
                    for (int i = 0; i < vals.Count; i++)
                    {
                        if (vals[i] != null) Destroy(vals[i].gameObject);
                    }
                    _beams.Clear();
                }
                
                if (_beamSfxHandle > 0 && SFXManager.Instance != null)
                {
                    SFXManager.Instance.StopLoop(_beamSfxHandle, 0.2f);
                    _beamSfxHandle = -1;
                }
                return;
            }

            
            int maxTargets = _def.GetMaxTargets(_level);
            float rangeMul = _hostBase != null ? _hostBase.GetRangeMultiplier() : 1f;
            float dpsMul = _hostBase != null ? _hostBase.GetDpsMultiplier() : 1f;
            float range = _stats.range * Mathf.Max(0.01f, rangeMul);
            float rampPerSec = _def.GetRampUpPerSecond(_level);
            float rampMax = _def.GetRampMaxMultiplier(_level);
            float rampDownPerSec = _def.GetRampDownPerSecond(_level);
            float penalty = _def.GetMultiTargetPenalty(_level);

            _enemySnapshot.Clear();
            foreach (var e in EnemyBase2D.All) _enemySnapshot.Add(e);

            
            for (int i = _enemySnapshot.Count - 1; i >= 0; i--)
            {
                var e = _enemySnapshot[i];
                if (e == null || !e.gameObject.activeInHierarchy || e.CurrentHealth <= 0f)
                {
                    _enemySnapshot.RemoveAt(i);
                    continue;
                }
                float d = Vector2.Distance((Vector2)transform.position, (Vector2)e.transform.position);
                if (d > range)
                {
                    _enemySnapshot.RemoveAt(i);
                }
            }

            
            if (_def != null && _def.FocusOnHighestHp)
            {
                
                _enemySnapshot.Sort((a, b) =>
                {
                    if (a == null || b == null) return 0;
                    int hpCmp = b.CurrentHealth.CompareTo(a.CurrentHealth); 
                    if (hpCmp != 0) return hpCmp;
                    float da = Vector2.Distance((Vector2)transform.position, (Vector2)a.transform.position);
                    float db = Vector2.Distance((Vector2)transform.position, (Vector2)b.transform.position);
                    return da.CompareTo(db);
                });
            }
            else
            {
                
                _enemySnapshot.Sort((a, b) =>
                {
                    float da = Vector2.Distance((Vector2)transform.position, (Vector2)a.transform.position);
                    float db = Vector2.Distance((Vector2)transform.position, (Vector2)b.transform.position);
                    return da.CompareTo(db);
                });
            }

            
            if (_enemySnapshot.Count > maxTargets)
            {
                _enemySnapshot.RemoveRange(maxTargets, _enemySnapshot.Count - maxTargets);
            }

            
            var selectedSet = new HashSet<EnemyBase2D>(_enemySnapshot);
            float dt = Time.deltaTime;
            
            if (rampDownPerSec > 0f && _ramp.Count > 0)
            {
                var keys = new List<EnemyBase2D>(_ramp.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    var k = keys[i];
                    if (!selectedSet.Contains(k))
                    {
                        float f = _ramp[k] - rampDownPerSec * dt;
                        if (f <= 1f) _ramp.Remove(k); else _ramp[k] = f;
                    }
                }
            }

            
            for (int i = 0; i < _enemySnapshot.Count; i++)
            {
                var e = _enemySnapshot[i];
                if (!_ramp.TryGetValue(e, out float f)) f = 1f;
                f += rampPerSec * dt;
                if (f > rampMax) f = rampMax;
                _ramp[e] = f;
            }

            int kTargets = Mathf.Max(1, _enemySnapshot.Count);
            float splitDivisor = 1f + penalty * (kTargets - 1);
            float baseDps = Mathf.Max(0f, _stats.dps * Mathf.Max(0.01f, dpsMul));

            
            _currentTargets.Clear();
            _currentTargets.AddRange(_enemySnapshot);

            
            if (_slowCooldown.Count > 0)
            {
                var keys = new List<EnemyBase2D>(_slowCooldown.Keys);
                float delta = Time.deltaTime;
                for (int i = 0; i < keys.Count; i++)
                {
                    var k = keys[i];
                    if (k == null || !k.gameObject.activeInHierarchy || k.CurrentHealth <= 0f)
                    {
                        _slowCooldown.Remove(k);
                        continue;
                    }
                    _slowCooldown[k] = Mathf.Max(0f, _slowCooldown[k] - delta);
                }
            }
            
            if (_burnCooldown.Count > 0)
            {
                var keys = new List<EnemyBase2D>(_burnCooldown.Keys);
                float delta = Time.deltaTime;
                for (int i = 0; i < keys.Count; i++)
                {
                    var k = keys[i];
                    if (k == null || !k.gameObject.activeInHierarchy || k.CurrentHealth <= 0f)
                    {
                        _burnCooldown.Remove(k);
                        continue;
                    }
                    _burnCooldown[k] = Mathf.Max(0f, _burnCooldown[k] - delta);
                }
            }
            if (_poisonCooldown.Count > 0)
            {
                var keys = new List<EnemyBase2D>(_poisonCooldown.Keys);
                float delta = Time.deltaTime;
                for (int i = 0; i < keys.Count; i++)
                {
                    var k = keys[i];
                    if (k == null || !k.gameObject.activeInHierarchy || k.CurrentHealth <= 0f)
                    {
                        _poisonCooldown.Remove(k);
                        continue;
                    }
                    _poisonCooldown[k] = Mathf.Max(0f, _poisonCooldown[k] - delta);
                }
            }
            if (_stunCooldown.Count > 0)
            {
                var keys = new List<EnemyBase2D>(_stunCooldown.Keys);
                float delta = Time.deltaTime;
                for (int i = 0; i < keys.Count; i++)
                {
                    var k = keys[i];
                    if (k == null || !k.gameObject.activeInHierarchy || k.CurrentHealth <= 0f)
                    {
                        _stunCooldown.Remove(k);
                        continue;
                    }
                    _stunCooldown[k] = Mathf.Max(0f, _stunCooldown[k] - delta);
                }
            }

            
            for (int i = 0; i < _enemySnapshot.Count; i++)
            {
                var e = _enemySnapshot[i];
                if (!_ramp.TryGetValue(e, out float f)) f = 1f;
                float perTargetDps = (baseDps * f) / splitDivisor;
                float dmg = perTargetDps * dt;
                e.TakeDamage(dmg);
                
                if (_def != null && _def.HasSlowOnHit())
                {
                    float sp = _def.GetSlowPercent(_level);
                    float sd = _def.GetSlowDuration(_level);
                    if (sp > 0f && sd > 0f)
                    {
                        
                        float cd = 0f;
                        _slowCooldown.TryGetValue(e, out cd);
                        if (cd <= 0f)
                        {
                            e.ApplySlow(sp, sd);
                            _slowCooldown[e] = Mathf.Max(0.05f, slowReapplyInterval);
                        }
                    }
                }

                
                if (_def != null)
                {
                    float burnDps = _def.GetBurnDps(_level) * (_hostBase != null ? _hostBase.GetBurnDpsMultiplier() : 1f);
                    float burnDur = _def.GetBurnDuration(_level) * (_hostBase != null ? _hostBase.GetBurnDurMultiplier() : 1f);
                    if (burnDps > 0f && burnDur > 0f)
                    {
                        float cd = 0f; _burnCooldown.TryGetValue(e, out cd);
                        if (cd <= 0f)
                        {
                            e.ApplyBurn(burnDps, burnDur);
                            _burnCooldown[e] = Mathf.Max(0.05f, burnReapplyInterval);
                        }
                    }
                }
                
                if (_def != null)
                {
                    float poisonDps = _def.GetPoisonDps(_level) * (_hostBase != null ? _hostBase.GetPoisonDpsMultiplier() : 1f);
                    float poisonDur = _def.GetPoisonDuration(_level) * (_hostBase != null ? _hostBase.GetPoisonDurMultiplier() : 1f);
                    if (poisonDps > 0f && poisonDur > 0f)
                    {
                        float cd = 0f; _poisonCooldown.TryGetValue(e, out cd);
                        if (cd <= 0f)
                        {
                            e.ApplyPoison(poisonDps, poisonDur);
                            _poisonCooldown[e] = Mathf.Max(0.05f, poisonReapplyInterval);
                        }
                    }
                }
                
                if (_def != null && _def.HasStunOnHit())
                {
                    float chance = Mathf.Clamp01(_def.GetStunChance(_level));
                    float dur = Mathf.Max(0f, _def.GetStunDuration(_level));
                    if (chance > 0f && dur > 0f)
                    {
                        float cd = 0f; _stunCooldown.TryGetValue(e, out cd);
                        if (cd <= 0f)
                        {
                            if (Random.value <= chance)
                            {
                                e.ApplyStun(dur);
                            }
                            _stunCooldown[e] = Mathf.Max(0.05f, stunReapplyInterval);
                        }
                    }
                }
            }

            
            UpdateBeams();

            
            bool haveTargets = _currentTargets.Count > 0;
            string beamKey = _def != null ? _def.GetSfxBeamKey() : string.Empty;
            if (haveTargets && !string.IsNullOrEmpty(beamKey))
            {
                if (_beamSfxHandle <= 0)
                {
                    _beamSfxHandle = SFXManager.Instance != null ? SFXManager.Instance.PlayLoop(beamKey, 0.15f) : -1;
                }
            }
            else
            {
                if (_beamSfxHandle > 0 && SFXManager.Instance != null)
                {
                    SFXManager.Instance.StopLoop(_beamSfxHandle, 0.2f);
                    _beamSfxHandle = -1;
                }
            }
        }

        private void UpdateBeams()
        {
            
            
            Color cStart = _def.GetBeamStartColor();
            Color cEnd = _def.GetBeamEndColor();
            float wBase = _def.GetBeamBaseWidth();
            float wMax = _def.GetBeamMaxWidth();
            bool jitter = _def.UseBeamJitter();
            float jitterAmp = _def.GetBeamJitterAmplitude();
            float rampMax = _def.GetRampMaxMultiplier(_level);

            
            if (_beams.Count > 0)
            {
                var keys = new System.Collections.Generic.List<EnemyBase2D>(_beams.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    var k = keys[i];
                    if (k == null || !_currentTargets.Contains(k))
                    {
                        var bc = _beams[k];
                        if (bc != null) Destroy(bc.gameObject);
                        _beams.Remove(k);
                        _slowCooldown.Remove(k);
                    }
                }
            }

            
            for (int i = 0; i < _currentTargets.Count; i++)
            {
                var e = _currentTargets[i];
                if (e == null) continue;
                if (!_beams.TryGetValue(e, out var bc) || bc == null)
                {
                    var go = new GameObject("InfernoBeam");
                    bc = go.AddComponent<BeamController>();
                    var lr = go.GetComponent<LineRenderer>();
                    
                    var matFromDef = _def.GetBeamMaterial();
                    if (matFromDef != null)
                    {
                        lr.sharedMaterial = matFromDef;
                    }
                    else if (lr.sharedMaterial == null)
                    {
                        var mat = new Material(Shader.Find("Sprites/Default"));
                        mat.color = Color.white;
                        lr.sharedMaterial = mat;
                    }
                    bc.Configure(cStart, cEnd, wBase, wMax, jitter, jitterAmp);
                    _beams[e] = bc;
                }
                
                bc.SetEndpoints(transform.position, e.transform.position);
                float ramp = 1f; _ramp.TryGetValue(e, out ramp);
                float t01 = Mathf.InverseLerp(1f, Mathf.Max(1.01f, rampMax), ramp);
                bc.SetIntensity01(t01);
            }
        }

        private void OnDrawGizmos()
        {
            if (_currentTargets == null || _currentTargets.Count == 0) return;
            
            for (int i = 0; i < _currentTargets.Count; i++)
            {
                var e = _currentTargets[i];
                if (e == null) continue;
                float f = 1f;
                if (_ramp != null && _ramp.TryGetValue(e, out var rf)) f = rf;
                
                float t = Mathf.InverseLerp(1f, 3f, f); 
                Color c = Color.Lerp(new Color(1f, 0.9f, 0.2f, 1f), Color.red, t);
                Gizmos.color = c;
                Gizmos.DrawLine(transform.position, e.transform.position);
            }
        }
    }
}
