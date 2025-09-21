using System.IO;
using UnityEngine;

namespace TR.Systems
{
    public static class SaveSystem
    {
        private const string FileName = "player_profile.json";

        private static string FullPath => Path.Combine(Application.persistentDataPath, FileName);

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
    }
}
