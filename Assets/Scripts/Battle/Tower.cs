using UnityEngine;
using TR.Data;
using TR.Systems;

namespace TR.Battle
{
    
    public class Tower : MonoBehaviour
    {
        [Header("Card Data")]
        [SerializeField] private CardDefinition card;

        [SerializeField] private int overrideLevel = 0;

        [Header("Runtime (read-only)")]
        [SerializeField] private float range;
        [SerializeField] private float fireRate;
        [SerializeField] private float dps;
        [SerializeField] private float damagePerShot;

        private float _cooldown;

        private void Start()
        {
            ApplyStats();
        }

        private void Update()
        {
            if (card == null) return;

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;

            var target = AcquireTarget();
            if (target == null) return;

            
            DealDamage(target, damagePerShot);
            _cooldown = 1f / Mathf.Max(0.01f, fireRate);
        }

        private void ApplyStats()
        {
            if (card == null) return;
            int level = overrideLevel > 0 ? overrideLevel : 1;
            if (overrideLevel <= 0 && !string.IsNullOrEmpty(card.CardId))
            {
                var cp = PlayerProfile.GetOrCreateCard(card.CardId);
                level = Mathf.Max(1, cp.level);
            }
            var stats = card.GetStatsForLevel(level);
            range = stats.range;
            fireRate = stats.fireRate;
            dps = stats.dps;
            damagePerShot = Mathf.Max(0f, dps / Mathf.Max(0.01f, fireRate));
        }

        private Enemy AcquireTarget()
        {
            Enemy best = null;
            float bestDist = float.MaxValue;
            var myPos = transform.position;
            foreach (var e in Enemy.All)
            {
                if (e == null) continue;
                float dist = Vector3.Distance(myPos, e.transform.position);
                if (dist <= range && dist < bestDist)
                {
                    best = e; bestDist = dist;
                }
            }
            return best;
        }

        private void DealDamage(Enemy e, float dmg)
        {
            if (e == null) return;
            e.TakeDamage(dmg);
            
        }

        public CardDefinition Card => card;
    }
}
