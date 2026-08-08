using System;
using System.Collections;
using System.Collections.Generic;
using Firebase.Firestore;
using UnityEngine;

namespace TR.Systems
{
    public class LeaderboardService : MonoBehaviour
    {
        public static LeaderboardService Instance { get; private set; }

        private const string CollectionName = "profiles";
        private const string FieldTrophies = "trophies";
        private const string FieldPlayerName = "playerName";
        private const int QueryLimit = 100;

        public static event Action<List<LeaderboardEntry>> OnLeaderboardLoaded;
        public static event Action<string> OnLeaderboardFailed;

        public class LeaderboardEntry
        {
            public string uid;
            public string playerName;
            public int trophies;
            public int rank;
        }

        private FirebaseFirestore _db;
        private bool _dbReady;

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

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Initialize(FirebaseFirestore db)
        {
            if (_dbReady) return;
            _db = db;
            _dbReady = db != null;
            if (_dbReady)
                Debug.Log("[LeaderboardService] Initialized.");
        }

        public void FetchTopPlayers()
        {
            if (!_dbReady)
            {
                OnLeaderboardFailed?.Invoke("Firestore not initialized.");
                return;
            }
            StartCoroutine(FetchTopPlayersCoroutine());
        }

        private IEnumerator FetchTopPlayersCoroutine()
        {
            var query = _db.Collection(CollectionName)
                .OrderByDescending(FieldTrophies)
                .Limit(QueryLimit);

            var queryTask = query.GetSnapshotAsync();
            yield return new WaitUntil(() => queryTask.IsCompleted);

            if (queryTask.IsFaulted)
            {
                string error = queryTask.Exception?.Message ?? "Unknown error";
                Debug.LogError($"[LeaderboardService] Fetch failed: {error}");
                OnLeaderboardFailed?.Invoke(error);
                yield break;
            }

            var snapshot = queryTask.Result;
            if (snapshot == null)
            {
                OnLeaderboardFailed?.Invoke("Empty snapshot.");
                yield break;
            }

            var entries = new List<LeaderboardEntry>();
            int rank = 1;
            foreach (var doc in snapshot.Documents)
            {
                string playerName = "";
                int trophies = 0;

                try { playerName = doc.GetValue<string>(FieldPlayerName); } catch { }
                try { trophies = doc.GetValue<int>(FieldTrophies); } catch { }

                if (string.IsNullOrEmpty(playerName))
                    playerName = "Player";

                entries.Add(new LeaderboardEntry
                {
                    uid = doc.Id,
                    playerName = playerName,
                    trophies = trophies,
                    rank = rank
                });
                rank++;
            }

            Debug.Log($"[LeaderboardService] Loaded {entries.Count} entries.");
            OnLeaderboardLoaded?.Invoke(entries);
        }
    }
}
