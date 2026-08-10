using System;
using UnityEngine;
using Firebase.Firestore;

namespace TR.Systems
{
    public static class FirestoreProvider
    {
        private static FirebaseFirestore _db;
        private static bool _failed;

        public static bool IsReady => _db != null;

        public static bool TryGet(out FirebaseFirestore db)
        {
            db = _db;
            if (_db != null) return true;
            if (_failed) return false;

            try
            {
                var instance = FirebaseFirestore.DefaultInstance;

                instance.Settings.PersistenceEnabled = false;

                _db = instance;
                db = _db;
                Debug.Log("[FirestoreProvider] Firestore ready (local persistence disabled).");
                return true;
            }
            catch (Exception ex)
            {
                _failed = true;
                Debug.LogWarning($"[FirestoreProvider] Firestore unavailable: {ex.Message}");
                return false;
            }
        }

        public static void Reset()
        {
            _db = null;
            _failed = false;
        }
    }
}
