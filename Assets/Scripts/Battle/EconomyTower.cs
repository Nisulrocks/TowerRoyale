using UnityEngine;
using TR.Data;
using TR.VFX;

namespace TR.Battle
{
    
    
    [RequireComponent(typeof(TowerBase))]
    public class EconomyTower : MonoBehaviour
    {
        private EconomyCardDefinition _def;
        private int _level;
        private float _maxHp;
        private float _hp;
        private float _incomePerSec;
        private float _decayPerSec;
        private MatchEconomy _economy;
        private RadialProgressRing _ring;
        private bool _dying; 
        private static readonly System.Collections.Generic.HashSet<EconomyTower> s_all = new();
        public static System.Collections.Generic.IReadOnlyCollection<EconomyTower> All => s_all;

        [Header("HP Ring (World-Space)")]
        [SerializeField] private float ringRadius = 0.6f;
        [SerializeField] private float ringThickness = 0.06f;
        [SerializeField] private int ringSegments = 64;
        [SerializeField] private Vector3 ringLocalOffset = new Vector3(0f, 0.35f, 0f);

        [Header("VFX")]
        [Tooltip("ParticleManager key to play when this tower generates money (one-shot)")]
        [SerializeField] private string incomeVfxKey = "";

        [SerializeField] private Transform incomeVfxAnchor;
        [Tooltip("Seconds between income VFX spawns while generating (regular cadence)")]
        [SerializeField] private float incomeVfxEverySeconds = 1.0f;
        private float _incomeVfxAccum;

        
        public float CurrentHP => _hp;
        public float MaxHP => _maxHp;
        public EconomyCardDefinition Definition => _def;

        public void Initialize(EconomyCardDefinition def, int level)
        {
            _def = def;
            _level = Mathf.Max(1, level);
            _maxHp = def.GetMaxHealth(_level);
            _hp = _maxHp;
            _incomePerSec = def.GetIncomePerSecond(_level);
            _decayPerSec = def.GetDecayPerSecond(_level);
            _economy = FindFirstObjectByType<MatchEconomy>(FindObjectsInactive.Include);

            
            var baseTower = GetComponent<TowerBase>();
            if (baseTower != null && baseTower.Stats.dps > 0f)
            {
                Debug.LogWarning("[EconomyTower] CardDefinition has non-zero DPS; economy towers should have DPS=0 in curves.");
            }

            
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

        private void OnEnable()
        {
            s_all.Add(this);
        }

        private void OnDisable()
        {
            s_all.Remove(this);
        }

        private void Update()
        {
            if (_def == null) return;
            if (_dying) return; 
            float dt = Time.deltaTime;

            
            if (_economy != null && _incomePerSec > 0f)
            {
                
                float gain = GetEffectiveIncomePerSecond() * dt;
                _incomeAcc += gain;
                int whole = Mathf.FloorToInt(_incomeAcc);
                if (whole > 0)
                {
                    _incomeAcc -= whole;
                    _economy.Earn(whole);
                }

                
                _incomeVfxAccum += dt;
                if (!string.IsNullOrEmpty(incomeVfxKey) && _incomeVfxAccum >= Mathf.Max(0.1f, incomeVfxEverySeconds))
                {
                    var pos = incomeVfxAnchor != null ? incomeVfxAnchor.position : transform.position;
                    ParticleManager.SpawnOneShot(incomeVfxKey, pos);
                    _incomeVfxAccum = 0f;
                }
            }

            
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

        private float _incomeAcc;

        
        private readonly System.Collections.Generic.Dictionary<object, float> _incomeBuffs = new System.Collections.Generic.Dictionary<object, float>();
        private float _incomeMul = 1f;
        public void AddOrUpdateIncomeBuff(object source, float incomeMultiplier)
        {
            if (source == null) return;
            _incomeBuffs[source] = Mathf.Max(0f, incomeMultiplier);
            RecomputeIncomeMul();
        }
        public void RemoveIncomeBuff(object source)
        {
            if (source == null) return;
            if (_incomeBuffs.Remove(source)) RecomputeIncomeMul();
        }
        private void RecomputeIncomeMul()
        {
            float mul = 1f;
            foreach (var kv in _incomeBuffs)
            {
                mul *= Mathf.Max(0f, kv.Value);
            }
            _incomeMul = Mathf.Max(0f, mul);
            
            var tb = GetComponent<TowerBase>();
            if (tb != null && tb.OnBuffsChanged != null) tb.OnBuffsChanged.Invoke();
        }
        public float GetEffectiveIncomePerSecond()
        {
            return Mathf.Max(0f, _incomePerSec * _incomeMul);
        }

        private System.Collections.IEnumerator DespawnAndDestroy()
        {
            _dying = true;
            
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            var startColors = new System.Collections.Generic.Dictionary<SpriteRenderer, Color>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                startColors[renderers[i]] = renderers[i].color;
            }
            Vector3 startScale = transform.localScale;
            Vector3 endScale = startScale * 0.7f;
            float t = 0f;
            const float dur = 0.35f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, dur);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                foreach (var kv in startColors)
                {
                    var sr = kv.Key; if (sr == null) continue;
                    var c = kv.Value; c.a = Mathf.Lerp(c.a, 0f, e); sr.color = c;
                }
                if (_ring != null)
                {
                    
                    var rr = _ring.GetComponent<SpriteRenderer>();
                    if (rr != null)
                    {
                        var rc = rr.color; rc.a = Mathf.Lerp(rc.a, 0f, e); rr.color = rc;
                    }
                }
                transform.localScale = Vector3.Lerp(startScale, endScale, e);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
