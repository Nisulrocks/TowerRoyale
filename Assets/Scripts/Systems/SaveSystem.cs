using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TR.Systems
{
    public static class SaveSystem
    {
        private const string FileName = "player_profile.json";

        private static string FullPath => Path.Combine(Application.persistentDataPath, FileName);

        private static readonly byte[] _key = Encoding.UTF8.GetBytes("TR_Salty_2024_xK9mP3vQ7wZ1aR5tL8n");

        public static bool WasTampered { get; private set; }

        [Serializable]
        private class SaveWrapper
        {
            public string data;
            public string hash;
        }

        public static void Save(string json)
        {
            try
            {
                string hash = ComputeHash(json);
                var wrapper = new SaveWrapper { data = json, hash = hash };
                string wrapperJson = JsonUtility.ToJson(wrapper);
                File.WriteAllText(FullPath, wrapperJson);
#if UNITY_EDITOR
                Debug.Log($"TR Save: {FullPath}\n{json}");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"TR Save failed: {ex}");
            }
        }

        public static string Load()
        {
            WasTampered = false;
            try
            {
                if (!File.Exists(FullPath)) return null;
                string raw = File.ReadAllText(FullPath);

                var wrapper = JsonUtility.FromJson<SaveWrapper>(raw);
                if (wrapper == null || string.IsNullOrEmpty(wrapper.data) || string.IsNullOrEmpty(wrapper.hash))
                {
                    if (!string.IsNullOrEmpty(raw) && raw.TrimStart().StartsWith("{"))
                    {
                        try
                        {
                            JsonUtility.FromJson<PlayerProfileDTO>(raw);
                            return raw;
                        }
                        catch { }
                    }
                    return null;
                }

                string expectedHash = ComputeHash(wrapper.data);
                if (wrapper.hash != expectedHash)
                {
                    WasTampered = true;
                    Debug.LogWarning("[SaveSystem] Profile data tampered - hash mismatch detected.");
                    return null;
                }

                return wrapper.data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"TR Load failed: {ex}");
                return null;
            }
        }

        private static string ComputeHash(string input)
        {
            using (var hmac = new HMACSHA256(_key))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = hmac.ComputeHash(bytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
