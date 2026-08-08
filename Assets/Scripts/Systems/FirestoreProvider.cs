using System;
using UnityEngine;
using Firebase.Firestore;

namespace TR.Systems
{
    // Single place that hands out the Firestore instance, so its settings are applied exactly once
    // before any read or write. Settings are immutable after the first Firestore operation, so
    // every caller must come through here rather than touching DefaultInstance directly.
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

                // Firestore's desktop persistence is a LevelDB store at
                //   %LOCALAPPDATA%/firestore/<app>/<projectId>/main
                // keyed only by app name and project id — not by process or install path. LevelDB
                // takes an exclusive lock on it, so a second instance of the game on the same
                // machine (editor + build, or two builds) aborts in native code with no catchable
                // exception. Disabling persistence keeps everything in memory and lets instances
                // coexist. The game requires a live connection anyway, so the offline cache buys
                // us nothing.
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

        // Lets a retry after sign-in succeed if the very first attempt ran before Firebase was up.
        public static void Reset()
        {
            _db = null;
            _failed = false;
        }
    }
}
