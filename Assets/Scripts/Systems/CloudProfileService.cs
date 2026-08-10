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
            if (Instance == this) Instance = null;
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
            PlayerProfile.BeginCloudSync();

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

            bool fromServer = true;
            var loadTask = docRef.GetSnapshotAsync(Source.Server);
            yield return new WaitUntil(() => loadTask.IsCompleted);

            if (loadTask.IsFaulted)
            {
                fromServer = false;
                string error = loadTask.Exception?.Message ?? "Unknown error";
                Debug.LogWarning($"[CloudProfileService] Server read failed ({error}); retrying from cache.");

                loadTask = docRef.GetSnapshotAsync(Source.Default);
                yield return new WaitUntil(() => loadTask.IsCompleted);

                if (loadTask.IsFaulted)
                {
                    string err2 = loadTask.Exception?.Message ?? "Unknown error";
                    Debug.LogError($"[CloudProfileService] Load failed: {err2}");
                    OnProfileLoadFailed?.Invoke(err2);
                    yield break;
                }
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
                if (!fromServer || snapshot.Metadata.IsFromCache)
                {
                    Debug.LogError($"[CloudProfileService] Document for {uid} has no profile, but this snapshot is " +
                                   $"not authoritative (fromServer={fromServer}, fromCache={snapshot.Metadata.IsFromCache}). " +
                                   "Refusing to treat it as a new account.");
                    OnProfileLoadFailed?.Invoke("Could not confirm the account's cloud profile.");
                    yield break;
                }

                Debug.Log($"[CloudProfileService] Server confirms {uid} has no profile yet. New account.");
                OnProfileLoaded?.Invoke(null);
                yield break;
            }

            Debug.Log($"[CloudProfileService] Loaded profile for {uid} ({profileJson.Length} bytes).");
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
                Debug.LogError($"[CloudProfileService] Save failed for {uid}: {error}");
                OnProfileSaveFailed?.Invoke(error);
                yield break;
            }

            Debug.Log($"[CloudProfileService] Profile uploaded for {uid} ({json.Length} bytes, trophies={trophies}).");
            OnProfileSaved?.Invoke();
        }
    }
}
