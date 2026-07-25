using UnityEngine;
using TR.Net;
using TR.Data;

namespace TR.Battle
{
    
    public class MatchEconomy : MonoBehaviour
    {
        [SerializeField] private int maxMoney = 10000;
        [SerializeField] private int current;

        public int Current => current;
        public int MaxMoney => maxMoney;

        public System.Action<int> OnMoneyChanged; 

        public void BeginMatch(ArenaDefinition arena = null)
        {
            if (DuoRejoinService.IsRejoinAttempt && DuoRejoinService.TryLoadMatchMoney(out int saved))
            {
                current = Mathf.Clamp(saved, 0, maxMoney);
                Debug.Log($"[MatchEconomy] Rejoined match; restored money={current}");
            }
            else
            {
                int start = arena != null ? arena.StartingMoney : 500;
                current = Mathf.Clamp(start, 0, maxMoney);
                Debug.Log($"[MatchEconomy] Fresh/duo start; starting money={current} (rejoin={DuoRejoinService.IsRejoinAttempt})");
            }
            OnMoneyChanged?.Invoke(current);
            DuoRejoinService.SaveMatchMoney(current);
        }

        public bool CanAfford(int amount) => current >= Mathf.Max(0, amount);

        public bool Spend(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (current < amount) return false;
            current -= amount;
            OnMoneyChanged?.Invoke(current);
            DuoRejoinService.SaveMatchMoney(current);
            return true;
        }

        public void Earn(int amount)
        {
            current = Mathf.Clamp(current + Mathf.Max(0, amount), 0, maxMoney);
            OnMoneyChanged?.Invoke(current);
            DuoRejoinService.SaveMatchMoney(current);
        }
    }
}
