using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TR.Infrastructure;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace TR.Net
{
    
    public class DuoNetworkManager : MonoBehaviourPunCallbacks
    {
        public static DuoNetworkManager Instance { get; private set; }

        
        public const string KEY_ARENA = "C0";    // SQL-filterable arena id (hard match filter)
        public const string KEY_TROPHIES = "C1"; // SQL-filterable trophies (kept for future ranking, NOT used to block)

        
        public const string PROP_NICK = "nick";
        public const string PROP_TROPHIES = "tr";
        public const string PROP_CASTLE = "cl";

        
        
        public const string PROP_READY = "rdy";
        
        public const string PROP_STARTING = "st";

        
        
        
        public const int RejoinPlayerTtlMs = 90000;

        private static readonly TypedLobby DuoSqlLobby = new TypedLobby("tr_duo_sql", LobbyType.SqlLobby);

        public enum MatchState { Idle, Connecting, JoiningLobby, Searching, WaitingForPartner, PartnerFound, Starting, Failed }
        public MatchState State { get; private set; } = MatchState.Idle;

        
        public System.Action<string> OnStatusChanged;   // human-readable status text
        public System.Action OnCancelled;
        public System.Action<string> OnFailed;          // error message

        private string _arenaId;
        private string _arenaDisplayName;
        private int _trophies;
        private int _castleLevel;
        private string _battleSceneName;
        private bool _cancelRequested;
        private bool _matchmakingActive;
        private bool _loadStarted;
        
        private bool _pendingStart;
        
        private bool _rejoiningToSearch;
        private Coroutine _rejoinCo;
        
        private bool _pendingRejoinLeave;
        
        [SerializeField] private float rejoinMinDelay = 1.5f;
        [SerializeField] private float rejoinMaxDelay = 3.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            
            DuoEnemyPrefabPool.EnsurePool();

            
            
            var peer = PhotonNetwork.NetworkingClient?.LoadBalancingPeer;
            if (peer != null)
            {
                
                peer.DisconnectTimeout = 15000;   // ms without ACK before disconnect (default 10000)
                peer.SentCountAllowance = 10;     // resend attempts before considering the peer lost (default 7)
            }
        }

        // ---- friend (private) matches ----
        private bool _friendRoomMode;
        private string _friendRoomName;
        private bool _friendRoomIsHost;

        public static string NewFriendRoomName() => $"duofriend_{System.Guid.NewGuid():N}";

        // Same flow as StartMatchmaking, but targets one named invisible room instead of the
        // random-matchmaking pool. The inviter hosts it; the invitee joins by name.
        public void StartFriendMatch(string roomName, bool asHost, string arenaId, int trophies, int castleLevel,
                                     string battleSceneName, string arenaDisplayName = null, string nickname = null)
        {
            if (string.IsNullOrEmpty(roomName)) { OnFailed?.Invoke("Invalid room."); return; }
            BeginMatchmaking(roomName, asHost, arenaId, trophies, castleLevel, battleSceneName, arenaDisplayName, nickname);
        }

        private void EnterFriendRoom()
        {
            if (_friendRoomIsHost)
            {
                var roomProps = new Hashtable
                {
                    { KEY_ARENA, _arenaId },
                    { KEY_TROPHIES, _trophies },
                };
                var options = new RoomOptions
                {
                    MaxPlayers = 2,
                    // Invisible so random matchmaking can never drop a stranger into a friend room.
                    IsVisible = false,
                    CustomRoomProperties = roomProps,
                    CustomRoomPropertiesForLobby = new[] { KEY_ARENA, KEY_TROPHIES },
                    CleanupCacheOnLeave = false,
                    PlayerTtl = RejoinPlayerTtlMs,
                    EmptyRoomTtl = 0,
                };
                SetState(MatchState.WaitingForPartner, "Waiting for your friend...");
                PhotonNetwork.CreateRoom(_friendRoomName, options, DuoSqlLobby);
            }
            else
            {
                SetState(MatchState.WaitingForPartner, "Joining your friend...");
                PhotonNetwork.JoinRoom(_friendRoomName);
            }
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            if (!_friendRoomMode || !_matchmakingActive || _cancelRequested) return;
            _matchmakingActive = false;
            _friendRoomMode = false;
            SetState(MatchState.Failed, "Could not join your friend.");
            OnFailed?.Invoke("That match is no longer available.");
        }


        public void StartMatchmaking(string arenaId, int trophies, int castleLevel, string battleSceneName, string arenaDisplayName = null, string nickname = null)
        {
            // Passing no room name clears friend-room state. This manager persists across matches,
            // and previously _friendRoomMode stayed set after a friend match, so the next normal
            // matchmaking attempt re-entered the old private room instead of the random pool —
            // which is why two players could no longer find each other.
            BeginMatchmaking(null, false, arenaId, trophies, castleLevel, battleSceneName, arenaDisplayName, nickname);
        }

        // Single entry point for both flows, so friend-room state is always set explicitly and can
        // never leak from one match into the next.
        private void BeginMatchmaking(string friendRoomName, bool friendIsHost, string arenaId, int trophies,
                                      int castleLevel, string battleSceneName, string arenaDisplayName, string nickname)
        {
            _friendRoomMode = !string.IsNullOrEmpty(friendRoomName);
            _friendRoomName = friendRoomName;
            _friendRoomIsHost = friendIsHost;

            _arenaId = arenaId ?? string.Empty;
            _arenaDisplayName = arenaDisplayName;
            _trophies = Mathf.Max(0, trophies);
            _castleLevel = Mathf.Max(1, castleLevel);
            _battleSceneName = battleSceneName;
            _cancelRequested = false;
            _matchmakingActive = true;
            _loadStarted = false;
            _pendingRejoinLeave = false;

            
            
            PhotonNetwork.AutomaticallySyncScene = false;
            if (!string.IsNullOrEmpty(nickname)) PhotonNetwork.NickName = nickname;
            if (string.IsNullOrEmpty(PhotonNetwork.NickName)) PhotonNetwork.NickName = "Player" + Random.Range(1000, 9999);

            if (PhotonNetwork.IsConnectedAndReady)
            {
                
                if (PhotonNetwork.InRoom)
                {
                    _pendingStart = true;
                    SetState(MatchState.JoiningLobby, "Leaving previous match...");
                    PhotonNetwork.LeaveRoom();
                }
                else if (PhotonNetwork.InLobby)
                {
                    TryJoinRandom();
                }
                else
                {
                    SetState(MatchState.JoiningLobby, "Entering lobby...");
                    PhotonNetwork.JoinLobby(DuoSqlLobby);
                }
            }
            else
            {
                SetState(MatchState.Connecting, "Connecting...");
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        public override void OnLeftRoom()
        {
            
            if (!_matchmakingActive || _cancelRequested) return;
            
            if (!_pendingStart && !_rejoiningToSearch) return;
            _pendingStart = false;
            _rejoiningToSearch = false;
            if (PhotonNetwork.InLobby)
            {
                TryJoinRandom();
            }
            else
            {
                SetState(MatchState.JoiningLobby, "Entering lobby...");
                PhotonNetwork.JoinLobby(DuoSqlLobby);
            }
        }

        
        public void CancelMatchmaking()
        {
            
            
            if (_loadStarted) return;

            _cancelRequested = true;
            _matchmakingActive = false;
            _friendRoomMode = false;
            _pendingRejoinLeave = false;
            StopRejoinTimer();
            SetState(MatchState.Idle, "Cancelled");

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
            OnCancelled?.Invoke();
        }

        public override void OnConnectedToMaster()
        {
            DuoEnemyPrefabPool.EnsurePool();

            if (!_matchmakingActive || _cancelRequested) return;
            SetState(MatchState.JoiningLobby, "Entering lobby...");
            PhotonNetwork.JoinLobby(DuoSqlLobby);
        }

        public override void OnJoinedLobby()
        {
            if (!_matchmakingActive || _cancelRequested) return;
            TryJoinRandom();
        }

        private void TryJoinRandom()
        {
            // Friend matches reach the room by name. Branching here keeps every existing caller
            // (OnJoinedLobby, OnLeftRoom, OnConnectedToMaster) working unchanged.
            if (_friendRoomMode)
            {
                EnterFriendRoom();
                return;
            }

            SetState(MatchState.Searching, "Searching for a partner...");

            string sql = $"{KEY_ARENA} = '{EscapeSql(_arenaId)}'";
            PhotonNetwork.JoinRandomRoom(null, 2, MatchmakingMode.FillRoom, DuoSqlLobby, sql);
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            if (!_matchmakingActive || _cancelRequested) return;
            
            CreateDuoRoom();
        }

        private void CreateDuoRoom()
        {
            var roomProps = new Hashtable
            {
                { KEY_ARENA, _arenaId },
                { KEY_TROPHIES, _trophies },
            };
            var options = new RoomOptions
            {
                MaxPlayers = 2,
                CustomRoomProperties = roomProps,
                CustomRoomPropertiesForLobby = new[] { KEY_ARENA, KEY_TROPHIES },
                
                
                
                CleanupCacheOnLeave = false,
                
                
                PlayerTtl = RejoinPlayerTtlMs,
                
                EmptyRoomTtl = 0,
            };
            string roomName = $"duo_{_arenaId}_{System.Guid.NewGuid():N}";
            SetState(MatchState.WaitingForPartner, "Waiting for a partner...");
            PhotonNetwork.CreateRoom(roomName, options, DuoSqlLobby);
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            if (!_matchmakingActive || _cancelRequested) return;
            
            TryJoinRandom();
        }

        public override void OnJoinedRoom()
        {
            if (_cancelRequested)
            {
                PhotonNetwork.LeaveRoom();
                return;
            }

            
            
            if (!_matchmakingActive) return;

            
            var props = new Hashtable
            {
                { PROP_NICK, PhotonNetwork.NickName },
                { PROP_TROPHIES, _trophies },
                { PROP_CASTLE, _castleLevel },
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            int count = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
            SetState(MatchState.WaitingForPartner, $"In room ({count}/2)...");
            
            
            if (count < 2) StartRejoinTimer();
            MarkReadyIfFull();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (_cancelRequested) return;
            
            if (!_matchmakingActive) return;
            
            StopRejoinTimer();
            int count = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
            SetState(MatchState.PartnerFound, $"Partner found ({count}/2)!");
            MarkReadyIfFull();
        }

        
        
        
        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            
            if (_loadStarted || _cancelRequested || !_matchmakingActive) return;

            
            if (IsLocalReady())
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PROP_READY, false } });

            
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
            {
                var room = PhotonNetwork.CurrentRoom;
                room.IsOpen = true;
                room.IsVisible = true;
                room.SetCustomProperties(new Hashtable { { PROP_STARTING, false } });
            }

            int count = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
            SetState(MatchState.WaitingForPartner, $"Partner left. Waiting for a partner ({count}/2)...");
            StartRejoinTimer();
        }

        
        
        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (_loadStarted || _cancelRequested || !_matchmakingActive) return;
            if (changedProps != null && changedProps.ContainsKey(PROP_READY))
                TryMasterStart();
        }

        
        
        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            if (_loadStarted || _cancelRequested) return;
            if (propertiesThatChanged != null
                && propertiesThatChanged.TryGetValue(PROP_STARTING, out object v)
                && v is bool starting && starting)
            {
                if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount >= 2)
                    BeginLoad();
            }
        }

        private bool IsLocalReady()
        {
            return PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PROP_READY, out object v)
                   && v is bool b && b;
        }

        
        
        private void MarkReadyIfFull()
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room == null || room.PlayerCount < 2) return;
            if (_loadStarted || _cancelRequested || !_matchmakingActive) return;

            _pendingRejoinLeave = false;
            StopRejoinTimer();
            if (!IsLocalReady())
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PROP_READY, true } });

            TryMasterStart();
        }

        
        
        private void TryMasterStart()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (_loadStarted || _cancelRequested) return;
            var room = PhotonNetwork.CurrentRoom;
            if (room == null || room.PlayerCount < 2) return;

            
            foreach (var pl in room.Players.Values)
            {
                bool ready = pl.CustomProperties.TryGetValue(PROP_READY, out object v) && v is bool b && b;
                if (!ready) return;
            }

            
            room.IsOpen = false;
            room.IsVisible = false;
            room.SetCustomProperties(new Hashtable { { PROP_STARTING, true } });
        }

        
        
        
        
        private void StartRejoinTimer()
        {
            StopRejoinTimer();
            if (!_matchmakingActive || _cancelRequested) return;
            _rejoinCo = StartCoroutine(RejoinAfterDelay());
        }

        private void StopRejoinTimer()
        {
            if (_rejoinCo != null)
            {
                StopCoroutine(_rejoinCo);
                _rejoinCo = null;
            }
            _pendingRejoinLeave = false;
        }

        private System.Collections.IEnumerator RejoinAfterDelay()
        {
            
            float delay = Random.Range(rejoinMinDelay, rejoinMaxDelay);
            float t = 0f;
            while (t < delay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            
            if (!_matchmakingActive || _cancelRequested || _loadStarted) { _rejoinCo = null; yield break; }
            if (PhotonNetwork.CurrentRoom == null) { _rejoinCo = null; yield break; }
            if (PhotonNetwork.CurrentRoom.PlayerCount >= 2) { _rejoinCo = null; yield break; }

            
            
            _pendingRejoinLeave = true;
            t = 0f;
            float grace = 1.0f;
            while (t < grace)
            {
                if (!_pendingRejoinLeave || !_matchmakingActive || _cancelRequested || _loadStarted || PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.PlayerCount >= 2)
                {
                    _pendingRejoinLeave = false;
                    _rejoinCo = null;
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            _pendingRejoinLeave = false;
            _rejoinCo = null;
            _rejoiningToSearch = true;
            SetState(MatchState.Searching, "Searching for a partner...");
            PhotonNetwork.LeaveRoom();
        }

        private async void BeginLoad()
        {
            if (PhotonNetwork.CurrentRoom == null) return;
            if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return;

            if (_loadStarted) return;
            _loadStarted = true;
            _matchmakingActive = false;
            // The room has served its purpose; nothing after this point should route back into it.
            _friendRoomMode = false;
            _friendRoomName = null;
            StopRejoinTimer();
            SetState(MatchState.Starting, "Match found! Loading...");

            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.CurrentRoom.IsOpen = false;
                PhotonNetwork.CurrentRoom.IsVisible = false;
            }

            PhotonNetwork.IsMessageQueueRunning = false;
            try
            {
                if (!string.IsNullOrEmpty(_arenaDisplayName) && SceneFader.Instance != null)
                {
                    var arenaDef = TR.Systems.GameDB.GetArenaById(_arenaId);
                    SceneFader.Instance.SetNextTransitionMessage(
                        _arenaDisplayName, 1.0f, arenaDef != null ? arenaDef.LoadingScreenImage : null);
                }
                if (SceneFader.Instance != null)
                {
                    await SceneFader.Instance.LoadSceneWithFade(_battleSceneName);
                }
                else
                {
                    Debug.LogWarning("[DuoNet] SceneFader missing; loading scene without fade.");
                    UnityEngine.SceneManagement.SceneManager.LoadScene(_battleSceneName);
                }
            }
            finally
            {
                PhotonNetwork.IsMessageQueueRunning = true;
            }
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (!_matchmakingActive || _loadStarted || _cancelRequested) return;
            if (!PhotonNetwork.IsMasterClient) return;

            var room = PhotonNetwork.CurrentRoom;
            if (room == null) return;

            
            
            if (room.PlayerCount < 2)
            {
                room.IsOpen = true;
                room.IsVisible = true;
                room.SetCustomProperties(new Hashtable { { PROP_STARTING, false } });
                SetState(MatchState.WaitingForPartner, "Partner left. Waiting for a partner...");
                StartRejoinTimer();
            }
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (_cancelRequested)
            {
                SetState(MatchState.Idle, "Cancelled");
                return;
            }
            if (!_matchmakingActive && State != MatchState.Starting)
            {
                
                return;
            }
            _matchmakingActive = false;
            _friendRoomMode = false;
            _friendRoomName = null;
            SetState(MatchState.Failed, $"Disconnected: {cause}");
            OnFailed?.Invoke(cause.ToString());
        }

        private void SetState(MatchState s, string msg)
        {
            State = s;
            OnStatusChanged?.Invoke(msg);
            Debug.Log($"[DuoNet] {s}: {msg}");
        }

        private static string EscapeSql(string s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace("'", "''");
    }
}
