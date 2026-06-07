using UnityEngine;
using TR.Data;
using TR.Audio;
using System.Collections.Generic;

namespace TR.Battle
{
    
    public class TowerPulse : TowerBase
    {
        private PulseCardDefinition _pulseDef;
        private int _cachedLevel;
        [Header("Debug")] [SerializeField] private bool debugPulseLogs = false;
        private int _missingDefFrames;

        
        protected override bool SupportsBuffGlow() => false;

        private void Awake()
        {
            
            SetCombatEnabled(false);
        }

        private void Start()
        {
            _pulseDef = Definition as PulseCardDefinition;
            _cachedLevel = Level;
            if (_pulseDef == null)
            {
                Debug.LogError("TowerPulse requires a PulseCardDefinition assigned to the tower's CardDefinition.", this);
            }
            
            StartCoroutine(PulseLoop());
        }

        private System.Collections.IEnumerator PulseLoop()
        {
            
            yield return null;
            while (true)
            {
                if (_pulseDef == null && Definition != null)
                {
                    _pulseDef = Definition as PulseCardDefinition;
                }
                if (_pulseDef != null)
                {
                    _missingDefFrames = 0;
                    _cachedLevel = Level;
                    DoPulse();
                    
                    float frMul = GetFireRateMultiplier();
                    float wait = Mathf.Max(0.05f, _pulseDef.GetPulseInterval(_cachedLevel) / Mathf.Max(0.01f, frMul));
                    yield return new WaitForSeconds(wait);
                }
                else
                {
                    _missingDefFrames++;
                    if (debugPulseLogs && _missingDefFrames % 60 == 0)
                    {
                        Debug.LogWarning($"[TowerPulse] Waiting for PulseCardDefinition on {name}. Definition assigned? Prefab set on card?", this);
                    }
                    yield return null;
                }
            }
        }

        private static readonly List<EnemyBase2D> s_snapshot = new List<EnemyBase2D>(64);

        private void DoPulse()
        {
            
            float radius = _pulseDef.GetPulseRadius(_cachedLevel) * Mathf.Max(0f, GetRangeMultiplier());
            float baseDamage = _pulseDef.GetPulseDamage(_cachedLevel) * Mathf.Max(0f, GetDpsMultiplier());
            
            bool isCrit = false;
            float damage = baseDamage;
            if (_pulseDef != null)
            {
                float c = Mathf.Clamp01(_pulseDef.GetCritChance(_cachedLevel));
                float m = Mathf.Max(1f, _pulseDef.GetCritMultiplier(_cachedLevel));
                if (c > 0f && m > 1f)
                {
                    isCrit = Random.value <= c;
                    if (isCrit) damage = baseDamage * m;
                }
            }
            if (radius <= 0.01f || damage <= 0.01f) return;

            
            string vfxKey = _pulseDef.GetShockwaveVfxKey();
            if (!string.IsNullOrEmpty(vfxKey))
            {
                TR.VFX.ParticleManager.SpawnOneShot(vfxKey, transform.position);
            }
            else
            {
                
                var col = _pulseDef.GetRippleColor();
                float dur = _pulseDef.GetRippleDuration();
                float lw = _pulseDef.GetRippleLineWidth();
                int seg = _pulseDef.GetRippleSegments();
                TR.VFX.PulseRipple.Spawn(transform.position, radius, col, dur, lw, seg);
            }
            
            string sfxKey = _pulseDef.GetSfxPulseKey();
            if (!string.IsNullOrEmpty(sfxKey)) SFXManager.Instance?.Play(sfxKey);
            
            if (isCrit)
            {
                TR.UI.DamageNumbers.ShowCrit(transform, _pulseDef.GetCritBurstText());
                var ck = _pulseDef.GetSfxCritKey(); if (!string.IsNullOrEmpty(ck)) SFXManager.Instance?.Play(ck);
            }

            
            s_snapshot.Clear();
            foreach (var e in EnemyBase2D.All) s_snapshot.Add(e);
            int hits = 0;
            for (int i = 0; i < s_snapshot.Count; i++)
            {
                var e = s_snapshot[i];
                if (e == null || !e.gameObject.activeInHierarchy || e.CurrentHealth <= 0f) continue;
                float d = Vector2.Distance((Vector2)transform.position, (Vector2)e.transform.position);
                if (d <= radius)
                {
                    e.TakeDamage(damage);
                    hits++;
                    if (_pulseDef.PulseAppliesOnHitEffects())
                    {
                        
                        ApplyOnHitEffects(e);
                    }
                }
            }
            if (debugPulseLogs)
            {
                Debug.Log($"[TowerPulse] Pulse hit {hits} enemies | radius={radius:0.##} dmg={damage:0.##} interval={_pulseDef.GetPulseInterval(_cachedLevel):0.##}", this);
            }

            
            DebugDrawCircleLocal(transform.position, radius, new Color(0.6f, 0.9f, 1f, 0.8f), 0.15f);
        }

        private void DebugDrawCircleLocal(Vector3 center, float radius, Color color, float duration)
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
    }
}
