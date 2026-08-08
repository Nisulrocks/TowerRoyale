using UnityEngine;
using TR.Data;
using TR.Audio;
using TR.UI;
using TR.Net;

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
        private bool _visualOnly;

        private static readonly System.Collections.Generic.List<EnemyBase2D> _splashSnapshot = new System.Collections.Generic.List<EnemyBase2D>(64);

        public void Init(EnemyBase2D target, float speed, float damage, float splashRadius,
                         TowerBase owner, CardDefinition def, int level, string impactVfxKey = null, bool isCritShot = false,
                         bool visualOnly = false)
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
            _visualOnly = visualOnly;
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
            if (dist > 0.001f)
                transform.up = to / dist;
            float step = _speed * Time.deltaTime;
            if (dist <= step || dist <= 0.001f)
            {
                // Destroy unconditionally: if Impact throws, an undestroyed projectile would
                // re-impact every frame and flood the log.
                try { Impact(dest); }
                finally { Destroy(gameObject); }
                return;
            }
            transform.position = pos + to.normalized * step;
        }

        private void PlayImpactSfx()
        {
            if (_def == null || _target == null || !_target.gameObject.activeInHierarchy || _target.CurrentHealth <= 0f)
                return;

            if (_isCritShot)
            {
                var ck = _def.GetSfxCritKey();
                if (!string.IsNullOrEmpty(ck)) SFXManager.Instance?.Play(ck);
            }

            if (_splashRadius > 0.01f)
            {
                var splashKey = _def.GetSfxSplashKey();
                if (!string.IsNullOrEmpty(splashKey)) SFXManager.Instance?.Play(splashKey);
            }
            else
            {
                var hitKey = _def.GetSfxHitKey();
                if (!string.IsNullOrEmpty(hitKey)) SFXManager.Instance?.Play(hitKey);
            }
        }

        private void Impact(Vector3 hitPos)
        {
            if (!string.IsNullOrEmpty(_impactVfxKey))
            {
                TR.VFX.ParticleManager.SpawnOneShot(_impactVfxKey, hitPos);
            }

            PlayImpactSfx();

            if (_visualOnly)
            {
                if (_owner != null && _target != null)
                {
                    _owner.PlayOnHitSfx(_target);
                    if (_splashRadius <= 0.01f && _def != null && _def.HasChainOnHit())
                        _owner.PlayChainSfx(_target);
                }
                return;
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
                    if (DuoRuntime.IsDuo)
                        DuoBattleCoordinator.Instance?.BroadcastTowerCrit(_target.transform.position, _def.GetCritBurstText());
                }
                // TakeDamage can kill the enemy, which removes it from EnemyBase2D.All mid-loop.
                // Snapshot first, as the other splash sites do.
                _splashSnapshot.Clear();
                foreach (var e in EnemyBase2D.All) _splashSnapshot.Add(e);
                for (int i = 0; i < _splashSnapshot.Count; i++)
                {
                    var e = _splashSnapshot[i];
                    if (e == null || !e.gameObject.activeInHierarchy || e.CurrentHealth <= 0f) continue;
                    float d = Vector2.Distance((Vector2)hitPos, (Vector2)e.transform.position);
                    if (d <= _splashRadius)
                    {
                        e.TakeDamage(_damage);
                        if (e != _target) _owner.ApplyOnHitEffects(e);
                    }
                }
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
                        if (DuoRuntime.IsDuo)
                            DuoBattleCoordinator.Instance?.BroadcastTowerCrit(_target.transform.position, _def.GetCritBurstText());
                    }
                    bool stunned = _owner.ApplyOnHitEffects(_target);
                    
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
