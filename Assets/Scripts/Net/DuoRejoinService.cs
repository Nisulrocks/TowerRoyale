using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TR.Systems;
using TR.Infrastructure;

namespace TR.Net
{
    
    
    public class DuoRejoinService : MonoBehaviourPunCallbacks
    {
        public static DuoRejoinService Instance { get; private set; }

        private const string PrefsRoomName = "DuoRoomName";
        private const string PrefsSceneName = "DuoSceneName";
        private const string PrefsArenaId = "DuoArenaId";
        private const string PrefsNick = "DuoNick";
        private const string PrefsMatchEnded = "DuoMatchEnded";

        
        public static bool IsMatchEnded => PlayerPrefs.HasKey(PrefsMatchEnded);
        public static bool HasActiveMatch => !IsMatchEnded && PlayerPrefs.HasKey(PrefsRoomName);

        
        public static bool IsRejoinAttempt { get; set; }

        
        public static string SavedRoomName => PlayerPrefs.GetString(PrefsRoomName, "");
        public static string SavedSceneName => PlayerPrefs.GetString(PrefsSceneName, "DuoBattle");
        public static string SavedArenaId => PlayerPrefs.GetString(PrefsArenaId, "");
        public static string SavedNick => PlayerPrefs.GetString(PrefsNick, "");

        
        public event System.Action<bool> OnRejoinComplete;

        private bool _rejoining;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (Instance == this) Instance = null;
        }

        
        
        public static void SaveActiveMatch()
        {
            if (!PhotonNetwork.InRoom || !MatchContext.IsDuo)
            {
                Debug.Log($"[DuoRejoinService] SaveActiveMatch skipped: InRoom={PhotonNetwork.InRoom}, IsDuo={MatchContext.IsDuo}");
                return;
            }
            var room = PhotonNetwork.CurrentRoom;
            if (room == null)
            {
                Debug.Log("[DuoRejoinService] SaveActiveMatch skipped: CurrentRoom is null.");
                return;
            }

            PlayerPrefs.SetString(PrefsRoomName, room.Name);
            PlayerPrefs.SetString(PrefsSceneName, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            PlayerPrefs.SetString(PrefsArenaId, MatchContext.ArenaId ?? "");
            PlayerPrefs.SetString(PrefsNick, PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.NickName : PhotonNetwork.NickName);
            PlayerPrefs.Save();
            Debug.Log($"[DuoRejoinService] Saved active match: room={room.Name}, scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, arena={MatchContext.ArenaId}");
        }

        
        public static void EndMatch()
        {
            if (!MatchContext.IsDuo)
            {
                ClearActiveMatch();
                return;
            }
            PlayerPrefs.SetInt(PrefsMatchEnded, 1);
            PlayerPrefs.DeleteKey(PrefsRoomName);
            PlayerPrefs.DeleteKey(PrefsSceneName);
            PlayerPrefs.DeleteKey(PrefsArenaId);
            PlayerPrefs.DeleteKey(PrefsNick);
            PlayerPrefs.Save();
            IsRejoinAttempt = false;
        }

        public static void ClearActiveMatch()
        {
            PlayerPrefs.DeleteKey(PrefsRoomName);
            PlayerPrefs.DeleteKey(PrefsSceneName);
            PlayerPrefs.DeleteKey(PrefsArenaId);
            PlayerPrefs.DeleteKey(PrefsNick);
            PlayerPrefs.DeleteKey(PrefsMatchEnded);
            PlayerPrefs.Save();
            IsRejoinAttempt = false;
        }

        
        public void BeginRejoin()
        {
            if (_rejoining) return;

            if (IsMatchEnded)
            {
                OnRejoinComplete?.Invoke(false);
                return;
            }

            string roomName = PlayerPrefs.GetString(PrefsRoomName, "");
            if (string.IsNullOrEmpty(roomName))
            {
                OnRejoinComplete?.Invoke(false);
                return;
            }

            _rejoining = true;
            IsRejoinAttempt = true;

            DuoEnemyPrefabPool.EnsurePool();

            MatchContext.Mode = GameMode.Duo;
            MatchContext.ArenaId = PlayerPrefs.GetString(PrefsArenaId, "");
            string nick = PlayerPrefs.GetString(PrefsNick, "");
            if (!string.IsNullOrEmpty(nick)) PhotonNetwork.NickName = nick;

            if (PhotonNetwork.IsConnectedAndReady)
            {
                if (PhotonNetwork.InRoom)
                {
                    
                    PhotonNetwork.LeaveRoom();
                }
                else
                {
                    PhotonNetwork.RejoinRoom(roomName);
                }
            }
            else
            {
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        
        public void AbandonMatch()
        {
            _rejoining = false;
            ClearActiveMatch();
        }

        public override void OnConnectedToMaster()
        {
            if (!_rejoining) return;
            string roomName = PlayerPrefs.GetString(PrefsRoomName, "");
            if (!string.IsNullOrEmpty(roomName))
            {
                PhotonNetwork.RejoinRoom(roomName);
            }
            else
            {
                _rejoining = false;
                IsRejoinAttempt = false;
                OnRejoinComplete?.Invoke(false);
            }
        }

        public override void OnLeftRoom()
        {
            if (!_rejoining) return;
            string roomName = PlayerPrefs.GetString(PrefsRoomName, "");
            if (!string.IsNullOrEmpty(roomName))
            {
                PhotonNetwork.RejoinRoom(roomName);
            }
            else
            {
                _rejoining = false;
                IsRejoinAttempt = false;
                OnRejoinComplete?.Invoke(false);
            }
        }

        public override void OnJoinedRoom()
        {
            if (!_rejoining) return;
            _rejoining = false;

            var room = PhotonNetwork.CurrentRoom;
            bool roomEnded = room != null && room.CustomProperties != null && room.CustomProperties.TryGetValue("DuoMatchEnded", out object endedVal) && endedVal is bool endedBool && endedBool;
            bool ended = IsMatchEnded || roomEnded;
            if (ended)
            {
                IsRejoinAttempt = false;
                EndMatch();
                OnRejoinComplete?.Invoke(false);
                return;
            }

            OnRejoinComplete?.Invoke(true);

            string scene = SavedSceneName;
            string arenaId = SavedArenaId;
            var arena = GameDB.GetArenaById(arenaId);
            string arenaName = arena != null ? arena.DisplayName : null;
            string message = string.IsNullOrEmpty(arenaName) ? null : arenaName;

            FadeToBattle(scene, message);
        }

        private async void FadeToBattle(string scene, string transitionMessage)
        {
            Photon.Pun.PhotonNetwork.IsMessageQueueRunning = false;
            try
            {
                var fader = SceneFader.Instance;
                if (!string.IsNullOrEmpty(transitionMessage))
                {
                    fader.SetNextTransitionMessage(transitionMessage, 1.0f);
                }
                await fader.LoadSceneWithFade(scene);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DuoRejoinService] Fade transition failed: {ex.Message}");
                UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
            }
            finally
            {
                Photon.Pun.PhotonNetwork.IsMessageQueueRunning = true;
            }
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            if (!_rejoining) return;
            _rejoining = false;
            IsRejoinAttempt = false;
            EndMatch();
            Debug.LogWarning($"[DuoRejoinService] Rejoin failed: {message} (code {returnCode})");
            OnRejoinComplete?.Invoke(false);
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (_rejoining)
            {
                _rejoining = false;
                IsRejoinAttempt = false;
                OnRejoinComplete?.Invoke(false);
            }
        }
    }
}
