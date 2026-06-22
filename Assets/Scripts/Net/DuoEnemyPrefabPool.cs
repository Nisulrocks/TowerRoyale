using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TR.Systems;
using TR.Data;

namespace TR.Net
{
    
    
    public class DuoEnemyPrefabPool : IPunPrefabPool
    {
        public const string EnemyPrefix = "enemy:";

        private readonly DefaultPool _fallback = new DefaultPool();
        private readonly Dictionary<string, GameObject> _enemyPrefabs = new Dictionary<string, GameObject>();

        public DuoEnemyPrefabPool()
        {
            BuildEnemyMap();
        }

        
        public static string EnemyPrefabId(EnemyDefinition def)
        {
            if (def == null) return null;
            string key = !string.IsNullOrEmpty(def.EnemyId) ? def.EnemyId : def.name;
            return EnemyPrefix + key;
        }

        private void BuildEnemyMap()
        {
            GameDB.EnsureLoaded();
            var enemies = GameDB.Enemies;
            if (enemies == null) return;
            for (int i = 0; i < enemies.Count; i++)
            {
                var def = enemies[i];
                if (def == null || def.Prefab == null) continue;
                string id = EnemyPrefabId(def);
                if (string.IsNullOrEmpty(id) || _enemyPrefabs.ContainsKey(id)) continue;
                _enemyPrefabs[id] = def.Prefab;
            }
            Debug.Log($"[DuoEnemyPrefabPool] Registered {_enemyPrefabs.Count} enemy prefabs for networked spawning.");
        }

        public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation)
        {
            if (!string.IsNullOrEmpty(prefabId) && prefabId.StartsWith(EnemyPrefix))
            {
                if (_enemyPrefabs.TryGetValue(prefabId, out var prefab) && prefab != null)
                {
                    
                    bool wasActive = prefab.activeSelf;
                    if (wasActive) prefab.SetActive(false);
                    GameObject go = Object.Instantiate(prefab, position, rotation);
                    if (wasActive) prefab.SetActive(true);
                    return go;
                }
                Debug.LogError($"[DuoEnemyPrefabPool] No enemy prefab registered for id '{prefabId}'. Did the EnemyDefinition lack a prefab, or is its PhotonView missing?");
                return null;
            }
            
            return _fallback.Instantiate(prefabId, position, rotation);
        }

        public void Destroy(GameObject gameObject)
        {
            if (gameObject != null) Object.Destroy(gameObject);
        }
    }
}
