using UnityEngine;
using TR.Systems;

namespace TR.UI
{
    
    public class LobbyBootstrap : MonoBehaviour
    {
        [Header("Daily Free Pack")]
        [SerializeField] private bool enableDailyFreePack = true;
        [SerializeField] private string dailyPackId = "normal_pack";
        [SerializeField] private int dailyPackCount = 1;
        [SerializeField] private int dailyCooldownHours = 24;

        private void Awake()
        {
            GameDB.EnsureLoaded();

            if (!enableDailyFreePack) return;

            
            long last = PlayerProfile.GetLastDailyPackUnix();
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long cool = Mathf.Max(1, dailyCooldownHours) * 3600L;
            if (now - last >= cool)
            {
                var id = string.IsNullOrEmpty(dailyPackId) ? "normal_pack" : dailyPackId;
                if (dailyPackCount > 0)
                {
                    PlayerProfile.Data.AddPacks(id, dailyPackCount);
                    PlayerProfile.SetLastDailyPackNow();
                    PlayerProfile.Save();
#if UNITY_EDITOR
                    Debug.Log($"TR: Granted {dailyPackCount} daily pack(s) '{id}'.");
#endif
                }
            }
        }
    }
}
