using System.Collections.Generic;
using UnityEngine;

namespace TR.Battle
{
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private float maxHp = 20f;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private int killReward = 5;
        [SerializeField] private BattlePath path;

        private float _hp;
        private int _wpIndex;
        private bool _reachedGoal;

        public System.Action<Enemy> OnDeath;
        public System.Action<Enemy> OnGoal;

        // Global registry and events for convenience
        public static readonly HashSet<Enemy> All = new HashSet<Enemy>();
        public static System.Action<Enemy> OnAnyDeath;
        public static System.Action<Enemy> OnAnyGoal;

        private void Awake()
        {
            _hp = maxHp;
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Update()
        {
            if (path == null || path.Count == 0) return;
            if (_reachedGoal) return;

            var target = path.Get(_wpIndex);
            if (target == null) return;

            var pos = transform.position;
            var to = target.position - pos;
            var step = moveSpeed * Time.deltaTime;
            if (to.magnitude <= step)
            {
                transform.position = target.position;
                _wpIndex++;
                if (_wpIndex >= path.Count)
                {
                    _reachedGoal = true;
                    OnGoal?.Invoke(this);
                    OnAnyGoal?.Invoke(this);
                    Destroy(gameObject);
                }
            }
            else
            {
                transform.position += to.normalized * step;
            }
        }

        public void Init(BattlePath p)
        {
            path = p;
        }

        public void TakeDamage(float dmg)
        {
            _hp -= dmg;
            if (_hp <= 0f)
            {
                OnDeath?.Invoke(this);
                OnAnyDeath?.Invoke(this);
                Destroy(gameObject);
            }
        }

        public int Reward => killReward;
        public float Hp => _hp;
    }
}
