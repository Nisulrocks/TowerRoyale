using UnityEngine;
using TR.Systems;

namespace TR.Dev
{
    // Attach to any GameObject in the Lobby scene for quick dev utilities.
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
