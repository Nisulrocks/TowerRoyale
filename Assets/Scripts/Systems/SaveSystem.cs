using System.IO;
using UnityEngine;

namespace TR.Systems
{
    public static class SaveSystem
    {
        private const string FileName = "player_profile.json";
        private const string BackupFileName = "player_profile.backup.json";

        private static string FullPath => Path.Combine(Application.persistentDataPath, FileName);
        private static string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);

        public static void Save(string json)
        {
            try
            {
                File.WriteAllText(FullPath, json);
#if UNITY_EDITOR
                Debug.Log($"TR Save: {FullPath}\n{json}");
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"TR Save failed: {ex}");
            }
        }

        public static string Load()
        {
            try
            {
                if (!File.Exists(FullPath)) return null;
                return File.ReadAllText(FullPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"TR Load failed: {ex}");
                return null;
            }
        }

        
        public static void SaveBackup(string json)
        {
            try
            {
                File.WriteAllText(BackupPath, json);
#if UNITY_EDITOR
                Debug.Log($"TR Save Backup: {BackupPath}");
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"TR Save backup failed: {ex}");
            }
        }

        
        public static string LoadBackup()
        {
            try
            {
                if (!File.Exists(BackupPath)) return null;
                return File.ReadAllText(BackupPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"TR Load backup failed: {ex}");
                return null;
            }
        }
    }
}
