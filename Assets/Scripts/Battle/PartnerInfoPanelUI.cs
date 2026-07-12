using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using TR.Net;

namespace TR.Battle
{
    
    public class PartnerInfoPanelUI : MonoBehaviourPunCallbacks
    {
        [Header("UI")]
        [Tooltip("Root object that holds the whole panel. Hidden entirely in single player.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text trophiesText;
        [SerializeField] private TMP_Text castleLevelText;

        [Header("Disconnect State")]
        [Tooltip("Optional graphic tinted red when the partner disconnects.")]
        [SerializeField] private Graphic backgroundGraphic;
        [SerializeField] private Color connectedColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color disconnectedColor = new Color(0.8f, 0.15f, 0.15f, 1f);
        [Tooltip("Text appended / shown when the partner leaves.")]
        [SerializeField] private TMP_Text statusText;

        private Player _partner;
        private bool _disconnected;

        private void Start()
        {
            
            if (!DuoRuntime.IsDuo || !PhotonNetwork.InRoom)
            {
                if (panelRoot != null) panelRoot.SetActive(false);
                enabled = false;
                return;
            }

            if (panelRoot != null) panelRoot.SetActive(true);
            ResolvePartner();
            RefreshDisplay();
        }

        private void ResolvePartner()
        {
            _partner = null;
            foreach (var p in PhotonNetwork.PlayerListOthers)
            {
                _partner = p;
                break;
            }
        }

        private void RefreshDisplay()
        {
            if (_partner == null)
            {
                if (nameText) nameText.text = "Waiting...";
                if (trophiesText) trophiesText.text = string.Empty;
                if (castleLevelText) castleLevelText.text = string.Empty;
                return;
            }

            string nick = GetProp(_partner, DuoNetworkManager.PROP_NICK, _partner.NickName);
            int trophies = GetIntProp(_partner, DuoNetworkManager.PROP_TROPHIES, 0);
            int castle = GetIntProp(_partner, DuoNetworkManager.PROP_CASTLE, 1);

            if (nameText) nameText.text = string.IsNullOrEmpty(nick) ? "Partner" : nick;
            if (trophiesText) trophiesText.text = $"Trophies: {trophies}";
            if (castleLevelText) castleLevelText.text = $"Castle Lv {castle}";

            if (_disconnected)
            {
                if (backgroundGraphic) backgroundGraphic.color = disconnectedColor;
                if (statusText)
                {
                    statusText.gameObject.SetActive(true);
                    statusText.text = "DISCONNECTED";
                }
            }
            else
            {
                if (backgroundGraphic) backgroundGraphic.color = connectedColor;
                if (statusText) statusText.gameObject.SetActive(false);
            }
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            if (_partner == null) ResolvePartner();
            if (targetPlayer != null && _partner != null && targetPlayer.ActorNumber == _partner.ActorNumber)
            {
                RefreshDisplay();
            }
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            
            if (_partner == null || _disconnected)
            {
                _disconnected = false;
                _partner = newPlayer;
                RefreshDisplay();
            }
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (_partner != null && otherPlayer != null && otherPlayer.ActorNumber != _partner.ActorNumber) return;
            _disconnected = true;
            RefreshDisplay();
            string nick = _partner != null ? GetProp(_partner, DuoNetworkManager.PROP_NICK, _partner.NickName) : "Partner";
            TR.UI.BattleToast.Show($"{(string.IsNullOrEmpty(nick) ? "Partner" : nick)} disconnected. Continuing solo.", 2.5f);
        }

        private static string GetProp(Player p, string key, string fallback)
        {
            if (p != null && p.CustomProperties != null && p.CustomProperties.TryGetValue(key, out var v) && v is string s)
                return s;
            return fallback;
        }

        private static int GetIntProp(Player p, string key, int fallback)
        {
            if (p != null && p.CustomProperties != null && p.CustomProperties.TryGetValue(key, out var v) && v is int i)
                return i;
            return fallback;
        }
    }
}
