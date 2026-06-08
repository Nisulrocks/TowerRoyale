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

        [ContextMenu("Give Money (+1000 Soft Currency)")]
        public void GiveMoney()
        {
            PlayerProfile.AddSoftCurrency(1000);
            Debug.Log("[Dev] Gave player +1000 soft currency.");
        }

        [ContextMenu("Give Trophies (+100)")]
        public void GiveTrophies()
        {
            PlayerProfile.AddTrophies(100);
            Debug.Log("[Dev] Gave player +100 trophies.");
        }

        [ContextMenu("Ban Player (60 min)")]
        public void BanPlayer()
        {
            PlayerProfile.Ban(60);
            Debug.Log("[Dev] Banned player for 60 minutes.");
        }

        [ContextMenu("Unban Player")]
        public void UnbanPlayer()
        {
            PlayerProfile.Unban();
            Debug.Log("[Dev] Unbanned player.");
        }
    }
}
