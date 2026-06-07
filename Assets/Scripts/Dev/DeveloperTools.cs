using UnityEngine;
using TR.Systems;

namespace TR.Dev
{
    
    public class DeveloperTools : MonoBehaviour
    {
        [ContextMenu("Wipe Player Profile (ALL DATA)")]
        public void WipeProfile()
        {
            PlayerProfile.WipeAllData();
            Debug.Log("[Dev] Wiped player profile (all progress reset).");
        }
    }
}
