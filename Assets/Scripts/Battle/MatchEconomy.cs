using UnityEngine;

namespace TR.Battle
{
    
    public class MatchEconomy : MonoBehaviour
    {
        [SerializeField] private int startingMoney = 500;
        [SerializeField] private int current;

        public int Current => current;

        public System.Action<int> OnMoneyChanged; 

        public void BeginMatch()
        {
            current = Mathf.Max(0, startingMoney);
            OnMoneyChanged?.Invoke(current);
        }

        public bool CanAfford(int amount) => current >= Mathf.Max(0, amount);

        public bool Spend(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (current < amount) return false;
            current -= amount;
            OnMoneyChanged?.Invoke(current);
            return true;
        }

        public void Earn(int amount)
        {
            current += Mathf.Max(0, amount);
            OnMoneyChanged?.Invoke(current);
        }
    }
}
