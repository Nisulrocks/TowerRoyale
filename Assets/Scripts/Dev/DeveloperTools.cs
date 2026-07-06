using UnityEngine;
using TR.Systems;

namespace TR.Dev
{
    public class DeveloperTools : MonoBehaviour
    {
        [Header("Dev Build Panel")]
        public KeyCode togglePanelKey = KeyCode.D;

        private bool _showPanel = false;
        private Rect _panelRect = new Rect(10, 10, 220, 260);

        private void Update()
        {
            if (!Debug.isDebugBuild) return;
            if (Input.GetKeyDown(togglePanelKey))
                _showPanel = !_showPanel;
        }

        private void OnGUI()
        {
            if (!Debug.isDebugBuild) return;
            if (!_showPanel) return;

            _panelRect = GUILayout.Window(9999, _panelRect, DrawDevWindow, "Dev Tools");
        }

        private void DrawDevWindow(int id)
        {
            GUILayout.Label($"Soft: {PlayerProfile.GetSoftCurrency()}  Trophies: {PlayerProfile.GetTrophies()}");
            GUILayout.Space(4);

            if (GUILayout.Button("Give +1000 Money"))
                GiveMoney();

            if (GUILayout.Button("Give +100 Trophies"))
                GiveTrophies();

            if (GUILayout.Button("Remove -50 Trophies"))
                RemoveTrophies();

            GUILayout.Space(4);

            if (GUILayout.Button("Ban (60 min)"))
                BanPlayer();

            if (GUILayout.Button("Unban"))
                UnbanPlayer();

            GUILayout.Space(4);
            GUI.color = Color.red;
            if (GUILayout.Button("WIPE PROFILE"))
                WipeProfile();
            GUI.color = Color.white;

            GUI.DragWindow();
        }

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

        [ContextMenu("Remove Trophies (-50)")]
        public void RemoveTrophies()
        {
            PlayerProfile.RemoveTrophies(50);
            Debug.Log("[Dev] Removed 50 trophies from player.");
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
