using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

namespace TR.Net
{
    
    
    
    
    public class DuoBattleCoordinator : MonoBehaviourPunCallbacks
    {
        public static DuoBattleCoordinator Instance { get; private set; }

        
        public const int PHASE_COUNTDOWN = 0;
        public const int PHASE_SPAWNING = 1;
        public const int PHASE_FINAL = 2;

        private readonly HashSet<int> _ready = new HashSet<int>();

        
        public event Action OnMatchStarted;
        
        public event Action<int, int, float, int, bool> OnWaveStateReceived;
        
        public event Action OnVictoryReceived;
        
        public event Action OnDefeatReceived;
        
        
        public event Action<string, int, int, int> OnTowerPlacedReceived;
        
        
        public event Action<int> OnTowerRemovedReceived;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        
        public void LocalReadyUp()
        {
            photonView.RPC(nameof(RpcReadyUp), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
        }

        [PunRPC]
        private void RpcReadyUp(int actorNumber)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            _ready.Add(actorNumber);
            int needed = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
            if (_ready.Count >= needed)
            {
                photonView.RPC(nameof(RpcMatchStarted), RpcTarget.All);
            }
        }

        [PunRPC]
        private void RpcMatchStarted()
        {
            OnMatchStarted?.Invoke();
        }

        
        public void BroadcastWaveState(int wave, int total, float timer, int phase, bool allowSkip)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RpcWaveState), RpcTarget.Others, wave, total, timer, phase, allowSkip);
        }

        [PunRPC]
        private void RpcWaveState(int wave, int total, float timer, int phase, bool allowSkip)
        {
            OnWaveStateReceived?.Invoke(wave, total, timer, phase, allowSkip);
        }

        
        
        
        private readonly HashSet<int> _skipVotes = new();
        
        public event Action<int, int> OnSkipVoteChanged;
        
        public event Action OnSkipConfirmed;

        
        public void CastSkipVote()
        {
            photonView.RPC(nameof(RpcCastSkipVote), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
        }

        [PunRPC]
        private void RpcCastSkipVote(int actorNumber)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!_skipVotes.Add(actorNumber)) return; 
            int needed = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
            int votes = _skipVotes.Count;
            photonView.RPC(nameof(RpcSkipVoteState), RpcTarget.All, votes, needed);
            if (votes >= needed)
            {
                _skipVotes.Clear();
                photonView.RPC(nameof(RpcSkipConfirmed), RpcTarget.All);
            }
        }

        [PunRPC]
        private void RpcSkipVoteState(int votes, int needed)
        {
            OnSkipVoteChanged?.Invoke(votes, needed);
        }

        [PunRPC]
        private void RpcSkipConfirmed()
        {
            OnSkipConfirmed?.Invoke();
        }

        
        public void ResetSkipVotes()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            _skipVotes.Clear();
            int needed = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
            photonView.RPC(nameof(RpcSkipVoteState), RpcTarget.All, 0, needed);
        }

        
        
        
        public void AwardKillMoney(int actorNumber, int amount)
        {
            if (!PhotonNetwork.IsMasterClient || amount <= 0) return;

            
            if (actorNumber == 0 || actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                LocalEarn(amount);
                return;
            }

            
            var target = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(actorNumber) : null;
            if (target != null)
            {
                photonView.RPC(nameof(RpcAwardMoney), target, amount);
            }
            else
            {
                
                LocalEarn(amount);
            }
        }

        [PunRPC]
        private void RpcAwardMoney(int amount)
        {
            LocalEarn(amount);
        }

        
        
        public event Action<bool> OnSpawnPortalsChanged;

        public void BroadcastSpawnPortals(bool open)
        {
            if (!PhotonNetwork.IsMasterClient || photonView == null) return;
            photonView.RPC(nameof(RpcSpawnPortals), RpcTarget.Others, open);
        }

        [PunRPC]
        private void RpcSpawnPortals(bool open)
        {
            OnSpawnPortalsChanged?.Invoke(open);
        }

        private static void LocalEarn(int amount)
        {
            if (amount <= 0) return;
            var econ = UnityEngine.Object.FindFirstObjectByType<TR.Battle.MatchEconomy>(FindObjectsInactive.Include);
            if (econ != null) econ.Earn(amount);
        }

        
        
        public event Action<string[], int[]> OnPartnerDeckReceived;

        
        private string[] _partnerDeckIds;
        private int[] _partnerDeckLevels;
        public bool HasPartnerDeck => _partnerDeckIds != null;
        public string[] PartnerDeckIds => _partnerDeckIds;
        public int[] PartnerDeckLevels => _partnerDeckLevels;

        public void BroadcastLocalDeck(string[] cardIds, int[] levels)
        {
            if (cardIds == null || levels == null || photonView == null) return;
            
            photonView.RPC(nameof(RpcPartnerDeck), RpcTarget.OthersBuffered, cardIds, levels);
        }

        [PunRPC]
        private void RpcPartnerDeck(string[] cardIds, int[] levels)
        {
            _partnerDeckIds = cardIds;
            _partnerDeckLevels = levels;
            OnPartnerDeckReceived?.Invoke(cardIds, levels);
        }

        
        
        public event Action<string, string, bool> OnChatMessageReceived;

        public void SendChat(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || photonView == null) return;
            string sender = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.NickName : "Player";
            
            OnChatMessageReceived?.Invoke(sender, message, true);
            photonView.RPC(nameof(RpcChat), RpcTarget.Others, sender, message);
        }

        [PunRPC]
        private void RpcChat(string sender, string message)
        {
            OnChatMessageReceived?.Invoke(sender, message, false);
        }

        
        
        
        
        public event Action<bool, string, int, float, float, int> OnPartnerDragChanged;

        public void SendDragState(bool active, string cardId, int level, float worldX, float worldY)
        {
            if (photonView == null) return;
            photonView.RPC(nameof(RpcPartnerDrag), RpcTarget.Others, active, cardId ?? string.Empty, level, worldX, worldY);
        }

        [PunRPC]
        private void RpcPartnerDrag(bool active, string cardId, int level, float worldX, float worldY, PhotonMessageInfo info)
        {
            int actor = info.Sender != null ? info.Sender.ActorNumber : 0;
            OnPartnerDragChanged?.Invoke(active, cardId, level, worldX, worldY, actor);
        }

        
        public void BroadcastVictory()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RpcVictory), RpcTarget.Others);
        }

        [PunRPC]
        private void RpcVictory()
        {
            OnVictoryReceived?.Invoke();
        }

        
        public void BroadcastDefeat()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RpcDefeat), RpcTarget.Others);
        }

        [PunRPC]
        private void RpcDefeat()
        {
            OnDefeatReceived?.Invoke();
        }

        
        public void BroadcastTowerPlaced(string cardId, int level, int snapIndex)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            photonView.RPC(nameof(RpcTowerPlaced), RpcTarget.Others, cardId, level, snapIndex);
        }

        [PunRPC]
        private void RpcTowerPlaced(string cardId, int level, int snapIndex, PhotonMessageInfo info)
        {
            OnTowerPlacedReceived?.Invoke(cardId, level, snapIndex, info.Sender != null ? info.Sender.ActorNumber : 0);
        }

        
        public void BroadcastTowerRemoved(int snapIndex)
        {
            photonView.RPC(nameof(RpcTowerRemoved), RpcTarget.Others, snapIndex);
        }

        [PunRPC]
        private void RpcTowerRemoved(int snapIndex)
        {
            OnTowerRemovedReceived?.Invoke(snapIndex);
        }
    }
}
