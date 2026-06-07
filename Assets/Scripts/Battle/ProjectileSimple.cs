using UnityEngine;
using TR.Data;
using TR.Audio;
using TR.UI;

namespace TR.Battle
{
    
    public class ProjectileSimple : MonoBehaviour
    {
        private EnemyBase2D _target;
        private float _speed;
        private float _damage;
        private float _splashRadius;
        private TowerBase _owner;
        private CardDefinition _def;
        private int _level;
        private string _impactVfxKey;
        private bool _isCritShot;

        public void Init(EnemyBase2D target, float speed, float damage, float splashRadius,
                         TowerBase owner, CardDefinition def, int level, string impactVfxKey = null, bool isCritShot = false)
        {
            _target = target;
            _speed = Mathf.Max(0.1f, speed);
            _damage = Mathf.Max(0f, damage);
            _splashRadius = Mathf.Max(0f, splashRadius);
            _owner = owner;
            _def = def;
            _level = Mathf.Max(1, level);
            _impactVfxKey = impactVfxKey ?? string.Empty;
            _isCritShot = isCritShot;
        }

        private void Update()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy || _target.CurrentHealth <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            Vector3 pos = transform.position;
            Vector3 dest = _target.transform.position;
            Vector3 to = dest - pos;
            float dist = to.magnitude;
            float step = _speed * Time.deltaTime;
            if (dist <= step || dist <= 0.001f)
            {
                Impact(dest);
                Destroy(gameObject);
                return;
            }
            transform.position = pos + to.normalized * step;
        }

        private void Impact(Vector3 hitPos)
        {
            if (!string.IsNullOrEmpty(_impactVfxKey))
            {
                TR.VFX.ParticleManager.SpawnOneShot(_impactVfxKey, hitPos);
            }
            if (_owner == null || _def == null)
            {
                return;
            }
            
            if (_splashRadius > 0.01f)
            {
                
                bool stunPrimary = false;
                if (_target != null && _target.gameObject.activeInHierarchy && _target.CurrentHealth > 0f)
                {
                    stunPrimary = _owner.ApplyOnHitEffects(_target);
                }
                if (_isCritShot && _target != null)
                {
                    DamageNumbers.ShowCrit(_target.transform, _def.GetCritBurstText());
                    var ck = _def.GetSfxCritKey(); if (!string.IsNullOrEmpty(ck)) SFXManager.Instance?.Play(ck);
                }
                foreach (var e in EnemyBase2D.All)
                {
                    if (e == null || !e.gameObject.activeInHierarchy || e.CurrentHealth <= 0f) continue;
                    float d = Vector2.Distance((Vector2)hitPos, (Vector2)e.transform.position);
                    if (d <= _splashRadius)
                    {
                        e.TakeDamage(_damage);
                        if (e != _target) _owner.ApplyOnHitEffects(e);
                    }
                }
                var splashKey = _def.GetSfxSplashKey(); if (!string.IsNullOrEmpty(splashKey)) SFXManager.Instance?.Play(splashKey);
                
                _owner.TryScheduleMoveOnAfterEffect(_target, stunPrimary);
            }
            else
            {
                if (_target != null && _target.gameObject.activeInHierarchy && _target.CurrentHealth > 0f)
                {
                    _target.TakeDamage(_damage);
                    if (_isCritShot)
                    {
                        DamageNumbers.ShowCrit(_target.transform, _def.GetCritBurstText());
                        var ck = _def.GetSfxCritKey(); if (!string.IsNullOrEmpty(ck)) SFXManager.Instance?.Play(ck);
                    }
                    bool stunned = _owner.ApplyOnHitEffects(_target);
                    var hitKey = _def.GetSfxHitKey(); if (!string.IsNullOrEmpty(hitKey)) SFXManager.Instance?.Play(hitKey);
                    
                    _owner.TryDoChainRicochet(_target, _owner.transform.position, _damage);
                    
                    _owner.TryScheduleMoveOnAfterEffect(_target, stunned);
                }
            }

            
            if (_def.HasTornadoOnHit())
            {
                float tRad = _def.GetTornadoRadius(_level);
                float tStr = _def.GetTornadoStrength(_level);
                float tDur = _def.GetTornadoDuration(_level);
                if (tRad > 0f && tStr > 0f && tDur > 0f)
                {
                    int maxTargets = _def.GetTornadoMaxPullTargets();
                    bool allowEasy = _def.TornadoAllowsTier(TR.Data.ArenaDefinition.EnemyTier.Easy);
                    bool allowMedium = _def.TornadoAllowsTier(TR.Data.ArenaDefinition.EnemyTier.Medium);
                    bool allowHard = _def.TornadoAllowsTier(TR.Data.ArenaDefinition.EnemyTier.Hard);
                    bool allowBoss = _def.TornadoAllowsTier(TR.Data.ArenaDefinition.EnemyTier.Boss);
                    string vfxKey = _def.GetTornadoVfxKey();
                    float vfxMul = _def.GetTornadoVfxScaleMultiplier();
                    bool allowCenterStack = _def.GetTornadoAllowCenterStack();
                    float falloffPower = _def.GetTornadoFalloffPower();
                    TornadoField.Spawn(hitPos, tRad, tStr, tDur,
                                       maxTargets, allowEasy, allowMedium, allowHard, allowBoss,
                                       vfxKey, vfxMul,
                                       allowCenterStack, falloffPower);
                }
            }

            
        }
    }
}
