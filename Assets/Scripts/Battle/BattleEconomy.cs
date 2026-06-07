using UnityEngine;

namespace TR.Battle
{
    
    public class BattleEconomy : MonoBehaviour
    {
        [SerializeField] private int startCurrency = 100;
        private int _currency;

        public int Currency => _currency;

        private void OnEnable()
        {
            _currency = startCurrency;
            Enemy.OnAnyDeath += HandleEnemyDeath;
        }

        private void OnDisable()
        {
            Enemy.OnAnyDeath -= HandleEnemyDeath;
        }

        private void HandleEnemyDeath(Enemy e)
        {
            _currency += e != null ? e.Reward : 0;
        }

        public bool TrySpend(int cost)
        {
            if (cost <= 0) return true;
            if (_currency < cost) return false;
            _currency -= cost;
            return true;
        }
    }
}
