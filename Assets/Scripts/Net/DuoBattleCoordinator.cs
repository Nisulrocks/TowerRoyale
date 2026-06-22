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
        
        public event Action<int, int, float, int> OnWaveStateReceived;
        
        public event Action OnVictoryReceived;
        
        public event Action OnDefeatReceived;
        
        public event Action OnSkipRequested;

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

        
        public void BroadcastWaveState(int wave, int total, float timer, int phase)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RpcWaveState), RpcTarget.Others, wave, total, timer, phase);
        }

        [PunRPC]
        private void RpcWaveState(int wave, int total, float timer, int phase)
        {
            OnWaveStateReceived?.Invoke(wave, total, timer, phase);
        }

        
        public void RequestSkip()
        {
            photonView.RPC(nameof(RpcRequestSkip), RpcTarget.MasterClient);
        }

        [PunRPC]
        private void RpcRequestSkip()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            OnSkipRequested?.Invoke();
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
    }
}
