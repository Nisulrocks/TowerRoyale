using UnityEngine;
using TR.Systems;
using TR.Net;

namespace TR.UI
{
    
    public class LobbyBootstrap : MonoBehaviour
    {
        [Header("Daily Free Pack")]
        [SerializeField] private bool enableDailyFreePack = true;
        [SerializeField] private string dailyPackId = "normal_pack";
        [SerializeField] private int dailyPackCount = 1;
        [SerializeField] private int dailyCooldownHours = 24;

        [Header("Rejoin")]
        [Tooltip("Optional prefab with a DuoRejoinPromptUI. If assigned, it is spawned when a saved duo match exists.")]
        [SerializeField] private GameObject rejoinPromptPrefab;
        [Tooltip("Optional canvas/parent transform to place the spawned prompt under. If null, the prompt is spawned at the root.")]
        [SerializeField] private RectTransform rejoinPromptParent;

        private void Awake()
        {
            GameDB.EnsureLoaded();

            if (enableDailyFreePack)
            {
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

            
            if (rejoinPromptPrefab == null)
            {
                Debug.Log("[LobbyBootstrap] No rejoinPromptPrefab assigned; skipping rejoin prompt.");
                return;
            }

            bool hasMatch = DuoRejoinService.HasActiveMatch;
            bool ended = DuoRejoinService.IsMatchEnded;
            Debug.Log($"[LobbyBootstrap] HasActiveMatch={hasMatch}, IsMatchEnded={ended}, prefab={(rejoinPromptPrefab != null ? rejoinPromptPrefab.name : "null")}");
            if (hasMatch || ended)
            {
                Transform parent = rejoinPromptParent != null ? rejoinPromptParent.transform : null;
                var inst = Instantiate(rejoinPromptPrefab, parent);
                if (inst != null)
                {
                    if (inst.transform is RectTransform rect)
                    {
                        rect.anchoredPosition = Vector2.zero;
                        rect.localScale = Vector3.one;
                        rect.localRotation = Quaternion.identity;
                    }
                    inst.SetActive(true);
                    Debug.Log($"[LobbyBootstrap] Spawned and activated rejoin prompt: {inst.name} under parent={(parent != null ? parent.name : "null")}");
                }
            }
        }
    }
}
