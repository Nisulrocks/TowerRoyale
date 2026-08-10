using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;

namespace TR.Systems
{
    public class CloudProfileService : MonoBehaviour
    {
        public static CloudProfileService Instance { get; private set; }

        private const string CollectionName = "profiles";
        private const string FieldProfileJson = "profileJson";
        private const string FieldLastSavedAt = "lastSavedAt";
        private const string FieldPlayerName = "playerName";
        private const string FieldNameLower = "nameLower";
        private const string FieldTrophies = "trophies";

        public static event Action<string> OnProfileLoaded;
        public static event Action OnProfileSaved;
        public static event Action<string> OnProfileLoadFailed;
        public static event Action<string> OnProfileSaveFailed;

        private FirebaseFirestore _db;
        private bool _dbReady = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            OnProfileLoaded += HandleProfileLoaded;
        }

        private void OnDestroy()
        {
            OnProfileLoaded -= HandleProfileLoaded;
        }

        private void HandleProfileLoaded(string json)
        {
            PlayerProfile.LoadFromCloud(json);
        }

        public void Initialize()
        {
            if (_dbReady) return;
            if (FirestoreProvider.TryGet(out _db))
            {
                _dbReady = true;
                Debug.Log("[CloudProfileService] Firestore initialized.");
            }
            else
            {
                Debug.LogWarning("[CloudProfileService] Firestore unavailable; cloud profile disabled.");
            }
        }

        public void LoadProfile(string uid)
        {
            if (!_dbReady)
            {
                OnProfileLoadFailed?.Invoke("Firestore not initialized.");
                return;
            }
            StartCoroutine(LoadProfileCoroutine(uid));
        }

        private IEnumerator LoadProfileCoroutine(string uid)
        {
            var docRef = _db.Collection(CollectionName).Document(uid);
            var loadTask = docRef.GetSnapshotAsync();
            yield return new WaitUntil(() => loadTask.IsCompleted);

            if (loadTask.IsFaulted)
            {
                string error = loadTask.Exception?.Message ?? "Unknown error";
                Debug.LogError($"[CloudProfileService] Load failed: {error}");
                OnProfileLoadFailed?.Invoke(error);
                yield break;
            }

            var snapshot = loadTask.Result;
            if (snapshot == null || !snapshot.Exists)
            {
                Debug.Log($"[CloudProfileService] No cloud profile found for {uid}. Fresh start.");
                OnProfileLoaded?.Invoke(null);
                yield break;
            }

            string profileJson = null;
            bool hasProfile = false;
            string readError = null;
            try
            {
                hasProfile = snapshot.TryGetValue<string>(FieldProfileJson, out profileJson);
            }
            catch (Exception ex)
            {
                readError = ex.Message;
                Debug.LogError($"[CloudProfileService] Failed to read profile data: {ex}");
            }

            if (readError != null)
            {
                OnProfileLoadFailed?.Invoke(readError);
                yield break;
            }

            if (!hasProfile)
            {
                Debug.Log($"[CloudProfileService] Document for {uid} has no profile yet. Fresh start.");
                OnProfileLoaded?.Invoke(null);
                yield break;
            }

            OnProfileLoaded?.Invoke(profileJson);
        }

        public void SaveProfile(string uid, string json, string playerName = "", int trophies = 0)
        {
            if (!_dbReady)
            {
                OnProfileSaveFailed?.Invoke("Firestore not initialized.");
                return;
            }
            if (SessionGuardService.IsKicked)
            {
                Debug.LogWarning("[CloudProfileService] Save blocked: session was taken over by another device.");
                OnProfileSaveFailed?.Invoke("Session ended on this device.");
                return;
            }
            StartCoroutine(SaveProfileCoroutine(uid, json, playerName, trophies));
        }

        private IEnumerator SaveProfileCoroutine(string uid, string json, string playerName, int trophies)
        {
            var docRef = _db.Collection(CollectionName).Document(uid);
            var data = new Dictionary<string, object>
            {
                { FieldProfileJson, json },
                { FieldLastSavedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                { FieldPlayerName, playerName ?? "" },
                { FieldNameLower, (playerName ?? "").ToLowerInvariant() },
                { FieldTrophies, trophies }
            };

            var setTask = docRef.SetAsync(data, SetOptions.MergeAll);
            yield return new WaitUntil(() => setTask.IsCompleted);

            if (setTask.IsFaulted)
            {
                string error = setTask.Exception?.Message ?? "Unknown error";
                Debug.LogError($"[CloudProfileService] Save failed: {error}");
                OnProfileSaveFailed?.Invoke(error);
                yield break;
            }

            OnProfileSaved?.Invoke();
        }
    }
}
