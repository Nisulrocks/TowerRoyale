using System.Collections.Generic;

namespace TR.Systems
{
    // Lightweight cross-scene parameter passing. Not persisted.
    public static class SceneParams
    {
        private static readonly Dictionary<string, object> _store = new();

        public static void Set<T>(string key, T value) => _store[key] = value;
        public static T Get<T>(string key, T defaultValue = default)
        {
            if (_store.TryGetValue(key, out var obj) && obj is T t) return t;
            return defaultValue;
        }
        public static bool Has(string key) => _store.ContainsKey(key);
        public static void Clear(string key) { if (_store.ContainsKey(key)) _store.Remove(key); }
        public static void ClearAll() => _store.Clear();
    }
}
