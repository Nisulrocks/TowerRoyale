using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TR.Battle;
using TR.UI;

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
        
        
        public event Action<string, int, Vector3, int, int> OnTowerPlacedReceived;


        public event Action<int, bool> OnTowerRemovedReceived;
        
        public event Action<int, float> OnTowerHpReceived;

        public event Action<int> OnTowerSyncRequested;

        public event Action<string[], int[], Vector3[], int[], int[], float[]> OnTowerSyncReceived;

        
        public event Action<int> OnEnemySyncRequested;
        
        public event Action<int[], string[], Vector3[], float[], float[], float[], float[], int[], int[]> OnEnemySyncReceived;

        
        public event Action<int> OnEnemyRespawnRequested;

        
        
        public event Action OnMasterClientSwitchedEvent;
        
        public event Action<string> OnSystemMessage;

        public event Action<Vector3, string> OnCritReceived;

        private readonly HashSet<int> _leftPlayers = new HashSet<int>();

        private bool _matchStarted;

        private void Awake()
        {
            Instance = this;
            DuoEnemyPrefabPool.EnsurePool();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            OnMasterClientSwitchedEvent?.Invoke();

            if (PhotonNetwork.IsMasterClient && newMasterClient != null)
            {
                TransferEnemyOwnershipTo(newMasterClient.ActorNumber);
                TakeOverTowerSimulation();
                RecalculateEnemyWaypoints();
            }

            if (newMasterClient != null)
            {
                bool isLocal = PhotonNetwork.LocalPlayer != null && newMasterClient.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
                string name = isLocal ? "You" : (string.IsNullOrEmpty(newMasterClient.NickName) ? "Partner" : newMasterClient.NickName);
                string suffix = isLocal ? "are" : "is";
                OnSystemMessage?.Invoke($"{name} {suffix} now the host.");
            }
        }

        public void TransferEnemyOwnershipTo(int actorNumber)
        {
            if (actorNumber <= 0) return;
            var syncs = FindObjectsByType<EnemyNetSync>(FindObjectsSortMode.None);
            int transferred = 0;
            int fixedCount = 0;
            foreach (var sync in syncs)
            {
                if (sync == null) continue;
                var pv = sync.photonView;
                if (pv != null && pv.OwnerActorNr != actorNumber)
                {
                    if (pv.OwnershipTransfer == OwnershipOption.Fixed)
                    {
                        fixedCount++;
                        continue;
                    }
                    pv.TransferOwnership(actorNumber);
                    transferred++;
                }
            }
            if (fixedCount > 0)
            {
                Debug.LogWarning($"[DuoBattleCoordinator] {fixedCount} enemy PhotonViews have OwnershipTransfer=Fixed; cannot transfer. Set enemy prefab PhotonViews to Takeover or Request. Enemies owned by a leaving master will be destroyed.");
            }
            Debug.Log($"[DuoBattleCoordinator] Transferred {transferred} enemy PhotonViews to actor {actorNumber}.");
        }

        private void TakeOverTowerSimulation()
        {
            var towers = TowerBase.All;
            int enabled = 0;
            foreach (var tower in towers)
            {
                if (tower == null) continue;
                if (tower.IsVisualOnly)
                {
                    tower.SetVisualOnly(false);
                    var bind = tower.GetComponent<TowerSnapBinding>();
                    if (bind != null) bind.SetMirror(false);
                    enabled++;
                }
            }
            Debug.Log($"[DuoBattleCoordinator] Took over simulation for {enabled} tower(s).");
        }

        private void RecalculateEnemyWaypoints()
        {
            var enemies = EnemyBase2D.All;
            int recalced = 0;
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                enemy.RecalculateWaypointFromPosition();
                recalced++;
            }
            Debug.Log($"[DuoBattleCoordinator] Recalculated waypoints for {recalced} enemy(s).");
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (otherPlayer != null)
            {
                _leftPlayers.Add(otherPlayer.ActorNumber);
                _ready.Remove(otherPlayer.ActorNumber);
                string name = string.IsNullOrEmpty(otherPlayer.NickName) ? "Partner" : otherPlayer.NickName;
                OnSystemMessage?.Invoke($"{name} has disconnected.");
            }

            if (PhotonNetwork.IsMasterClient)
            {
                ResetSkipVotes();
                TakeOverTowerSimulation();
                TryStartMatchIfReady();
            }
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (newPlayer != null)
            {
                bool wasLeft = _leftPlayers.Remove(newPlayer.ActorNumber);
                _ready.Remove(newPlayer.ActorNumber);
                if (wasLeft)
                {
                    string name = string.IsNullOrEmpty(newPlayer.NickName) ? "Partner" : newPlayer.NickName;
                    OnSystemMessage?.Invoke($"{name} has rejoined the match.");
                }
            }

            if (PhotonNetwork.IsMasterClient)
            {
                ResetSkipVotes();
            }
        }

        
        public static int GetActivePlayerCount()
        {
            if (PhotonNetwork.CurrentRoom == null) return 1;
            int active = 0;
            foreach (var p in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (p != null && !p.IsInactive) active++;
            }
            return Mathf.Max(1, active);
        }

        
        public void LocalReadyUp()
        {
            photonView.RPC(nameof(RpcReadyUp), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
        }

        [PunRPC]
        private void RpcReadyUp(int actorNumber)
        {
            if (!PhotonNetwork.IsMasterClient || _matchStarted) return;
            _ready.Add(actorNumber);
            TryStartMatchIfReady();
        }

        [PunRPC]
        private void RpcMatchStarted()
        {
            _matchStarted = true;
            OnMatchStarted?.Invoke();
        }

        public void MarkMatchStarted()
        {
            _matchStarted = true;
        }

        private void TryStartMatchIfReady()
        {
            if (_matchStarted || !PhotonNetwork.IsMasterClient) return;
            int needed = GetActivePlayerCount();
            if (_ready.Count >= needed)
            {
                _matchStarted = true;
                photonView.RPC(nameof(RpcMatchStarted), RpcTarget.All);
            }
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
            int needed = GetActivePlayerCount();
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
            int needed = GetActivePlayerCount();
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

        public void AwardWaveBonus(int wave, int total)
        {
            if (photonView == null || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
            if (total <= 0) return;

            var players = PhotonNetwork.CurrentRoom?.Players;
            int count = players != null ? players.Count : 1;
            if (count <= 0) count = 1;

            int perPlayer = total / count;
            int extra = total % count;

            LocalEarn(perPlayer + extra);
            BattleToast.Show($"Wave {wave} cleared! +${perPlayer + extra}");

            if (perPlayer > 0)
            {
                photonView.RPC(nameof(RpcWaveBonus), RpcTarget.Others, wave, perPlayer);
            }
        }

        [PunRPC]
        private void RpcWaveBonus(int wave, int amount)
        {
            LocalEarn(amount);
            BattleToast.Show($"Wave {wave} cleared! +${amount}");
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

        
        public void BroadcastTowerPlaced(string cardId, int level, Vector3 position, int placementId)
        {
            if (string.IsNullOrEmpty(cardId)) return;
            photonView.RPC(nameof(RpcTowerPlaced), RpcTarget.Others, cardId, level, position, placementId);
        }

        [PunRPC]
        private void RpcTowerPlaced(string cardId, int level, Vector3 position, int placementId, PhotonMessageInfo info)
        {
            OnTowerPlacedReceived?.Invoke(cardId, level, position, placementId, info.Sender != null ? info.Sender.ActorNumber : 0);
        }

        public void BroadcastTowerCrit(Vector3 worldPos, string text)
        {
            if (string.IsNullOrEmpty(text) || photonView == null) return;
            photonView.RPC(nameof(RpcTowerCrit), RpcTarget.Others, worldPos, text);
        }

        [PunRPC]
        private void RpcTowerCrit(Vector3 worldPos, string text)
        {
            OnCritReceived?.Invoke(worldPos, text);
        }




        public void BroadcastTowerRemoved(int placementId, bool playDestroyFeedback)
        {
            if (photonView == null || !PhotonNetwork.InRoom || placementId < 0) return;
            photonView.RPC(nameof(RpcTowerRemoved), RpcTarget.Others, placementId, playDestroyFeedback);
        }

        [PunRPC]
        private void RpcTowerRemoved(int placementId, bool playDestroyFeedback)
        {
            OnTowerRemovedReceived?.Invoke(placementId, playDestroyFeedback);
        }

        
        public void BroadcastTowerHp(int placementId, float hp)
        {
            if (placementId < 0) return;
            photonView.RPC(nameof(RpcTowerHp), RpcTarget.Others, placementId, hp);
        }

        [PunRPC]
        private void RpcTowerHp(int placementId, float hp)
        {
            OnTowerHpReceived?.Invoke(placementId, hp);
        }

        
        
        public void RequestTowerSync()
        {
            if (photonView == null) return;
            photonView.RPC(nameof(RpcRequestTowerSync), RpcTarget.MasterClient);
        }

        [PunRPC]
        private void RpcRequestTowerSync(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            int actor = info.Sender != null ? info.Sender.ActorNumber : 0;
            OnTowerSyncRequested?.Invoke(actor);
        }

        public void SendTowerSync(int targetActor, string[] cardIds, int[] levels, Vector3[] positions, int[] placementIds, int[] owners, float[] hps)
        {
            if (!PhotonNetwork.IsMasterClient || photonView == null) return;
            var target = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(targetActor) : null;
            if (target == null) return;
            photonView.RPC(nameof(RpcTowerSync), target, cardIds, levels, positions, placementIds, owners, hps);
        }

        [PunRPC]
        private void RpcTowerSync(string[] cardIds, int[] levels, Vector3[] positions, int[] placementIds, int[] owners, float[] hps)
        {
            OnTowerSyncReceived?.Invoke(cardIds, levels, positions, placementIds, owners, hps);
        }

        
        public void RequestEnemySync()
        {
            if (photonView == null) return;
            photonView.RPC(nameof(RpcRequestEnemySync), RpcTarget.MasterClient);
        }

        [PunRPC]
        private void RpcRequestEnemySync(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            int actor = info.Sender != null ? info.Sender.ActorNumber : 0;
            OnEnemySyncRequested?.Invoke(actor);
        }

        public void SendEnemySync(int targetActor, int[] viewIds, string[] enemyIds, Vector3[] positions, float[] hMul, float[] dMul, float[] sMul, float[] health, int[] waveNumbers, int[] waypointIndices)
        {
            if (!PhotonNetwork.IsMasterClient || photonView == null) return;
            var target = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(targetActor) : null;
            if (target == null) return;
            photonView.RPC(nameof(RpcEnemySync), target, viewIds, enemyIds, positions, hMul, dMul, sMul, health, waveNumbers, waypointIndices);
        }

        [PunRPC]
        private void RpcEnemySync(int[] viewIds, string[] enemyIds, Vector3[] positions, float[] hMul, float[] dMul, float[] sMul, float[] health, int[] waveNumbers, int[] waypointIndices)
        {
            OnEnemySyncReceived?.Invoke(viewIds, enemyIds, positions, hMul, dMul, sMul, health, waveNumbers, waypointIndices);
        }

        
        public void RequestEnemyRespawn()
        {
            if (photonView == null) return;
            photonView.RPC(nameof(RpcRequestEnemyRespawn), RpcTarget.MasterClient);
        }

        [PunRPC]
        private void RpcRequestEnemyRespawn(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            int actor = info.Sender != null ? info.Sender.ActorNumber : 0;
            OnEnemyRespawnRequested?.Invoke(actor);
        }
    }
}
