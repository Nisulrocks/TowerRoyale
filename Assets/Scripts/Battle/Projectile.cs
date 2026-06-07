using UnityEngine;
using System.Linq;

namespace TR.Battle
{
    
    public class Projectile : MonoBehaviour
    {
        [Header("Click/Physics Settings")]
        [Tooltip("Layer to assign at runtime so this projectile never blocks clicks (2 = Ignore Raycast)")]
        [SerializeField] private int nonBlockingLayer = 2;
        [SerializeField] private float speed = 10f;
        [SerializeField] private float damage = 1f;
        [SerializeField] private float splashRadius = 0f;
        [SerializeField] private float burnDps = 0f;
        [SerializeField] private float burnDuration = 0f;
        [SerializeField] private float poisonDps = 0f;
        [SerializeField] private float poisonDuration = 0f;
        [SerializeField] private float slowPercent = 0f;   
        [SerializeField] private float slowDuration = 0f;

        private EnemyBase2D _target;
        private Vector3 _lastKnownTargetPos;

        private void Awake()
        {
            
            TrySetLayerRecursive(gameObject, nonBlockingLayer);
            
            foreach (var c in GetComponentsInChildren<Collider2D>(true))
            {
                if (c == null) continue;
                c.enabled = false;
            }
            foreach (var c3 in GetComponentsInChildren<Collider>(true))
            {
                if (c3 == null) continue;
                c3.enabled = false;
            }
        }

        public void Init(EnemyBase2D target,
                         float projectileSpeed,
                         float dmg,
                         float splash,
                         float burnDpsOnHit,
                         float burnDurOnHit,
                         float poisonDpsOnHit,
                         float poisonDurOnHit,
                         float slowPercentOnHit,
                         float slowDurationOnHit)
        {
            _target = target;
            _lastKnownTargetPos = target != null ? target.transform.position : transform.position;
            speed = Mathf.Max(0f, projectileSpeed);
            damage = Mathf.Max(0f, dmg);
            splashRadius = Mathf.Max(0f, splash);
            burnDps = Mathf.Max(0f, burnDpsOnHit);
            burnDuration = Mathf.Max(0f, burnDurOnHit);
            poisonDps = Mathf.Max(0f, poisonDpsOnHit);
            poisonDuration = Mathf.Max(0f, poisonDurOnHit);
            slowPercent = Mathf.Max(0f, slowPercentOnHit);
            slowDuration = Mathf.Max(0f, slowDurationOnHit);
        }

        private static void TrySetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            if (layer >= 0 && layer < 32) go.layer = layer;
            foreach (Transform child in go.transform)
            {
                if (child == null) continue;
                TrySetLayerRecursive(child.gameObject, layer);
            }
        }

        private void Update()
        {
            
            Vector3 targetPos;
            if (_target != null && _target.gameObject.activeInHierarchy && _target.CurrentHealth > 0f)
            {
                targetPos = _target.transform.position;
                _lastKnownTargetPos = targetPos;
            }
            else
            {
                targetPos = _lastKnownTargetPos;
            }

            
            Vector3 pos = transform.position;
            Vector3 to = targetPos - pos;
            float dist = to.magnitude;
            float step = speed * Time.deltaTime;
            if (dist <= step || dist < 0.01f)
            {
                
                ApplyHitAt(targetPos);
                Destroy(gameObject);
                return;
            }

            Vector3 dir = to / (dist > 1e-5f ? dist : 1f);
            transform.position = pos + dir * step;
            
            if (dir.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);
            }
        }

        private void ApplyHitAt(Vector3 hitPos)
        {
            if (splashRadius > 0.01f)
            {
                
                
                var list = EnemyBase2D.All != null ? EnemyBase2D.All.ToArray() : System.Array.Empty<EnemyBase2D>();
                foreach (var e in list)
                {
                    if (e == null || !e.gameObject.activeInHierarchy || e.CurrentHealth <= 0f) continue;
                    float d = Vector2.Distance((Vector2)hitPos, (Vector2)e.transform.position);
                    if (d <= splashRadius)
                    {
                        e.TakeDamage(damage);
                        if (burnDps > 0f && burnDuration > 0f) e.ApplyBurn(burnDps, burnDuration);
                        if (poisonDps > 0f && poisonDuration > 0f) e.ApplyPoison(poisonDps, poisonDuration);
                        if (slowPercent > 0f && slowDuration > 0f) e.ApplySlow(slowPercent, slowDuration);
                    }
                }
            }
            else
            {
                
                var e = _target;
                if (e == null)
                {
                    
                    EnemyBase2D closest = null;
                    float best = float.MaxValue;
                    foreach (var cand in EnemyBase2D.All)
                    {
                        if (cand == null || !cand.gameObject.activeInHierarchy || cand.CurrentHealth <= 0f) continue;
                        float d = Vector2.Distance((Vector2)hitPos, (Vector2)cand.transform.position);
                        if (d < best) { best = d; closest = cand; }
                    }
                    e = closest;
                }
                if (e != null)
                {
                    e.TakeDamage(damage);
                    if (burnDps > 0f && burnDuration > 0f) e.ApplyBurn(burnDps, burnDuration);
                    if (poisonDps > 0f && poisonDuration > 0f) e.ApplyPoison(poisonDps, poisonDuration);
                    if (slowPercent > 0f && slowDuration > 0f) e.ApplySlow(slowPercent, slowDuration);
                }
            }
        }
    }
}
