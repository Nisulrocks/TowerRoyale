using UnityEngine;
using UnityEngine.SceneManagement;
using TR.Systems;
using TR.Net;

namespace TR.Dev
{
    public class DeveloperTools : MonoBehaviour
    {
        [Header("Dev Build Panel")]
        public KeyCode togglePanelKey = KeyCode.D;

        private bool _showPanel = false;
        private Rect _panelRect = new Rect(10, 10, 220, 400);
        private string _trophyInput = "0";
        private string _expInput = "0";

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

            GUILayout.BeginHorizontal();
            GUILayout.Label("Set Trophies:", GUILayout.Width(90));
            _trophyInput = GUILayout.TextField(_trophyInput, GUILayout.Width(90));
            if (GUILayout.Button("Set", GUILayout.Width(40)))
                SetTrophiesExact();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (GUILayout.Button("Ban (60 min)"))
                BanPlayer();

            if (GUILayout.Button("Unban"))
                UnbanPlayer();

            GUILayout.Space(4);

            if (GUILayout.Button("Unlock All Cards"))
                UnlockAllCards();

            if (GUILayout.Button("Max Out All Unlocked Cards"))
                MaxOutAllUnlockedCards();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Add XP:", GUILayout.Width(60));
            _expInput = GUILayout.TextField(_expInput, GUILayout.Width(90));
            if (GUILayout.Button("Add", GUILayout.Width(40)))
                AddCastleXP();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUI.color = Color.red;
            if (GUILayout.Button("WIPE PROFILE"))
                WipeProfile();
            GUI.color = Color.white;

            GUILayout.Space(4);
            GUI.color = new Color(1f, 0.6f, 0.2f);
            if (GUILayout.Button("DISCONNECT & REBOOT"))
                DisconnectAndReboot();
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
            PlayerProfile.SetTrophies(PlayerProfile.GetTrophies() - 50, true);
            Debug.Log("[Dev] Removed 50 trophies from player.");
        }

        public void SetTrophiesExact()
        {
            if (!int.TryParse(_trophyInput, out int target))
            {
                Debug.LogWarning("[Dev] Invalid trophy input.");
                return;
            }

            PlayerProfile.SetTrophies(target, true);
            _trophyInput = PlayerProfile.GetTrophies().ToString();
            Debug.Log($"[Dev] Set trophies to {PlayerProfile.GetTrophies()}.");
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

        [ContextMenu("Unlock All Cards")]
        public void UnlockAllCards()
        {
            GameDB.EnsureLoaded();
            foreach (var card in GameDB.Cards)
            {
                var cp = PlayerProfile.GetOrCreateCard(card.CardId);
                if (cp.ownedCount <= 0) cp.ownedCount = 1;
            }
            PlayerProfile.Save();
            Debug.Log("[Dev] Unlocked all cards.");
        }

        [ContextMenu("Max Out All Unlocked Cards")]
        public void MaxOutAllUnlockedCards()
        {
            GameDB.EnsureLoaded();
            foreach (var card in GameDB.Cards)
            {
                var cp = PlayerProfile.GetOrCreateCard(card.CardId);
                if (cp.ownedCount <= 0) continue;
                cp.level = card.Rarity != null ? card.Rarity.MaxLevel : 1;
            }
            PlayerProfile.Save();
            Debug.Log("[Dev] Maxed out all unlocked cards.");
        }

        [ContextMenu("Add Castle XP")]
        public void AddCastleXP()
        {
            if (!int.TryParse(_expInput, out int amount))
            {
                Debug.LogWarning("[Dev] Invalid XP input.");
                return;
            }
            PlayerProfile.AddCastleXP(Mathf.Max(0, amount));
            _expInput = "0";
        }

        [ContextMenu("Disconnect & Reboot (Test Rejoin)")]
        public void DisconnectAndReboot()
        {
            StartCoroutine(DisconnectAndReloadCo());
        }

        private System.Collections.IEnumerator DisconnectAndReloadCo()
        {
            if (MatchContext.IsDuo && Photon.Pun.PhotonNetwork.InRoom)
            {
                DuoRejoinService.SaveActiveMatch();
                Debug.Log("[Dev] Saved active duo match for rejoin test.");
            }

            MatchContext.Reset();
            PlayerProfile.Save();
            PlayerPrefs.Save();

            if (Photon.Pun.PhotonNetwork.IsConnected)
            {
                Photon.Pun.PhotonNetwork.Disconnect();
            }

            Debug.Log("[Dev] Disconnecting and reloading first scene...");

            float timeout = 2.5f;
            while (Photon.Pun.PhotonNetwork.IsConnected && timeout > 0f)
            {
                yield return null;
                timeout -= Time.unscaledDeltaTime;
            }

            SceneManager.LoadScene(0);
        }
    }
}
