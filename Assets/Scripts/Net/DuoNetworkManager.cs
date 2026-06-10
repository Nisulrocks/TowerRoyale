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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        
        public void StartMatchmaking(string arenaId, int trophies, int castleLevel, string battleSceneName, string arenaDisplayName = null, string nickname = null)
        {
            _arenaId = arenaId ?? string.Empty;
            _arenaDisplayName = arenaDisplayName;
            _trophies = Mathf.Max(0, trophies);
            _castleLevel = Mathf.Max(1, castleLevel);
            _battleSceneName = battleSceneName;
            _cancelRequested = false;
            _matchmakingActive = true;
            _loadStarted = false;

            
            
            PhotonNetwork.AutomaticallySyncScene = false;
            if (!string.IsNullOrEmpty(nickname)) PhotonNetwork.NickName = nickname;
            if (string.IsNullOrEmpty(PhotonNetwork.NickName)) PhotonNetwork.NickName = "Player" + Random.Range(1000, 9999);

            if (PhotonNetwork.IsConnectedAndReady)
            {
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
            else
            {
                SetState(MatchState.Connecting, "Connecting...");
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        
        public void CancelMatchmaking()
        {
            _cancelRequested = true;
            _matchmakingActive = false;
            SetState(MatchState.Idle, "Cancelled");

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
            OnCancelled?.Invoke();
        }

        public override void OnConnectedToMaster()
        {
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
                CleanupCacheOnLeave = true,
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

            
            var props = new Hashtable
            {
                { PROP_NICK, PhotonNetwork.NickName },
                { PROP_TROPHIES, _trophies },
                { PROP_CASTLE, _castleLevel },
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            int count = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
            SetState(MatchState.WaitingForPartner, $"In room ({count}/2)...");
            TryStartIfRoomFull();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (_cancelRequested) return;
            int count = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
            SetState(MatchState.PartnerFound, $"Partner found ({count}/2)!");
            TryStartIfRoomFull();
        }

        private void TryStartIfRoomFull()
        {
            if (PhotonNetwork.CurrentRoom == null) return;
            if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return;

            if (_loadStarted) return;
            _loadStarted = true;
            _matchmakingActive = false;
            SetState(MatchState.Starting, "Match found! Loading...");

            
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.CurrentRoom.IsOpen = false;
                PhotonNetwork.CurrentRoom.IsVisible = false;
            }

            
            
            if (!string.IsNullOrEmpty(_arenaDisplayName) && SceneFader.Instance != null)
            {
                SceneFader.Instance.SetNextTransitionMessage(_arenaDisplayName, 1.0f);
            }
            if (SceneFader.Instance != null)
            {
                _ = SceneFader.Instance.LoadSceneWithFade(_battleSceneName);
            }
            else
            {
                Debug.LogWarning("[DuoNet] SceneFader missing; loading scene without fade.");
                UnityEngine.SceneManagement.SceneManager.LoadScene(_battleSceneName);
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
