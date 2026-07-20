using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TR.Data;
using TR.VFX;

namespace TR.Battle
{
    
    public class WaveSpawner : MonoBehaviour
    {
        [Header("Config")] 
        [SerializeField] private Transform[] spawnPoints; 
        [SerializeField] private Path2D path; 
        [SerializeField] private float spawnInterval = 0.3f; 
        [Header("VFX")] 

        [SerializeField] private string spawnPortalVfxKey = "";
        private readonly Dictionary<Transform, ParticleSystem> _activePortals = new();

        private ArenaDefinition _arena;
        
        private int _plannedThisWave;
        private int _spawnedThisWave;
        private bool _spawning;

        public int GetPendingSpawns()
        {
            if (!_spawning) return 0;
            return Mathf.Max(0, _plannedThisWave - _spawnedThisWave);
        }

        public void Configure(ArenaDefinition arena)
        {
            _arena = arena;
        }

        public void CloseAllPortals()
        {
            
            if (TR.Net.DuoRuntime.IsSimulationAuthority && _activePortals.Count > 0)
            {
                TR.Net.DuoBattleCoordinator.Instance?.BroadcastSpawnPortals(false);
            }
            CloseAllPortalsLocal();
        }

        
        
        private void CloseAllPortalsLocal()
        {
            if (_activePortals.Count == 0) return;
            foreach (var kv in _activePortals)
            {
                var ps = kv.Value;
                if (ps == null) continue;
                
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            _activePortals.Clear();
        }

        
        
        public void OpenSpawnPortals()
        {
            
            if (TR.Net.DuoRuntime.IsSimulationAuthority)
            {
                TR.Net.DuoBattleCoordinator.Instance?.BroadcastSpawnPortals(true);
            }
            OpenSpawnPortalsLocal();
        }

        private void OpenSpawnPortalsLocal()
        {
            if (string.IsNullOrEmpty(spawnPortalVfxKey)) return;
            if (spawnPoints == null || spawnPoints.Length == 0) return;
            CloseAllPortalsLocal();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var sp = spawnPoints[i];
                if (sp != null)
                {
                    var ps = ParticleManager.Spawn(spawnPortalVfxKey, sp.position, Quaternion.identity, sp, true);
                    if (ps != null) _activePortals[sp] = ps;
                }
            }
        }

        public void SpawnWave(int waveNumber)
        {
            if (_arena == null)
            {
                Debug.LogWarning("[WaveSpawner] Arena not configured; call Configure before spawning.");
                return;
            }
            
            var any = _arena.Enemies;
            if (any == null || any.Length == 0)
            {
                Debug.LogWarning($"[WaveSpawner] Arena '{_arena.DisplayName}' has no enemies assigned.");
                return;
            }
            _arena.GetEnemyCountRangeForWave(waveNumber, out int min, out int max);
            int count = Random.Range(min, max + 1);

            
            bool spawnBoss = _arena.ShouldSpawnBoss(waveNumber, out string warn);
            if (!string.IsNullOrEmpty(warn)) Debug.LogWarning($"[WaveSpawner] {warn}");
            EnemyDefinition boss = spawnBoss ? _arena.BossEnemy : null;

            
            _plannedThisWave = count + (boss != null ? 1 : 0);
            _spawnedThisWave = 0;
            _spawning = true;
            StartCoroutine(SpawnWaveRoutine(waveNumber, count, boss));
        }

        private IEnumerator SpawnWaveRoutine(int waveNumber, int count, EnemyDefinition boss)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("[WaveSpawner] No spawn points assigned.");
                yield break;
            }
            
            OpenSpawnPortals();
            
            
            float coop = TR.Net.DuoRuntime.IsDuo ? CoopEnemyStatMultiplier : 1f;

            if (boss != null)
            {
                var sp = GetSpawnPoint(Random.Range(0, Mathf.Max(1, spawnPoints.Length)));
                _arena.GetBossScalingForWave(waveNumber, out float hMul, out float dMul, out float sMul);
                
                var bossDef = ResolveDuoSpawnable(boss);
                if (bossDef != null) SpawnEnemyScaled(bossDef, sp, hMul * coop, dMul * coop, sMul, waveNumber);
                _spawnedThisWave++;
                
                count = Mathf.Max(0, count - 1);
                yield return new WaitForSeconds(Mathf.Max(0f, spawnInterval));
            }

            for (int i = 0; i < count; i++)
            {
                var def = ResolveDuoSpawnable(GetWeightedEnemyForWave(waveNumber));
                var sp = GetSpawnPoint(i % spawnPoints.Length);
                
                if (def != null) SpawnEnemyScaled(def, sp, coop, coop, 1f, waveNumber);
                _spawnedThisWave++;
                yield return new WaitForSeconds(Mathf.Max(0f, spawnInterval));
            }
            
            CloseAllPortals();
            Debug.Log($"[WaveSpawner] Spawned wave {waveNumber} with {count} enemies (interval {spawnInterval:F2}s).");
            _spawning = false;
        }

        private Transform GetSpawnPoint(int index)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;
            int i = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
            return spawnPoints[i];
        }

        private static Sprite CreateSquareSprite()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var cols = new Color[size * size];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }

        
        [Tooltip("Multiplier applied to enemy health and damage in duo co-op mode.")]
        public const float CoopEnemyStatMultiplier = 2f;

        
        
        private EnemyBase2D SpawnEnemyScaled(EnemyDefinition def, Transform point, float healthMul, float damageMul, float speedMul, int waveNumber)
        {
            if (def == null)
            {
                Debug.LogWarning("[WaveSpawner] Null EnemyDefinition; skipping spawn.");
                return null;
            }

            
            
            if (TR.Net.DuoRuntime.IsDuo)
            {
                return SpawnEnemyNetworked(def, point != null ? point.position : Vector3.zero, healthMul, damageMul, speedMul, waveNumber);
            }

            GameObject go = null;
            if (def.Prefab != null)
            {
                go = Instantiate(def.Prefab);
                if (point != null) go.transform.position = point.position;
                go.name = $"{def.name}";
            }
            else
            {
                go = new GameObject("EnemyPlaceholder2D");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CreateSquareSprite();
                sr.color = new Color(1f, 0.5f, 0.5f, 1f);
                if (point != null) go.transform.position = point.position;
                go.name = $"Enemy_{def.name}";
            }

            var enemy = go.GetComponent<EnemyBase2D>();
            if (enemy == null) enemy = go.AddComponent<EnemyBase2D>();
            enemy.Initialize(def, path);
            enemy.SetArena(_arena);
            enemy.SetWaveNumber(waveNumber);
            BattleSceneController.Instance?.RegisterWaveEnemy(waveNumber);
            
            if (!(Mathf.Approximately(healthMul, 1f) && Mathf.Approximately(damageMul, 1f) && Mathf.Approximately(speedMul, 1f)))
            {
                enemy.ApplyBossScaling(healthMul, damageMul, speedMul);
            }
            return enemy;
        }

        
        
        public EnemyBase2D SpawnEnemyNetworked(EnemyDefinition def, Vector3 position, float healthMul, float damageMul, float speedMul, int waveNumber)
        {
            
            if (!Photon.Pun.PhotonNetwork.IsMasterClient) return null;

            if (def.Prefab == null)
            {
                Debug.LogError($"[WaveSpawner] Duo spawn requires a prefab on EnemyDefinition '{def.name}'. Placeholder enemies are not supported in duo.");
                return null;
            }

            string prefabId = TR.Net.DuoEnemyPrefabPool.EnemyPrefabId(def);
            object[] data = new object[] { def.EnemyId, healthMul, damageMul, speedMul, waveNumber };

            var go = Photon.Pun.PhotonNetwork.Instantiate(prefabId, position, Quaternion.identity, 0, data);
            if (go == null)
            {
                Debug.LogError($"[WaveSpawner] PhotonNetwork.Instantiate failed for '{prefabId}'. Ensure the enemy prefab has a PhotonView + EnemyNetSync.");
                return null;
            }
            go.name = $"{def.name}";

            
            
            var enemy = go.GetComponent<EnemyBase2D>();
            return enemy;
        }

        
        
        
        
        private EnemyDefinition ResolveDuoSpawnable(EnemyDefinition def)
        {
            
            if (!TR.Net.DuoRuntime.IsDuo) return def;
            if (def != null && def.Prefab != null) return def;

            
            var all = _arena != null ? _arena.Enemies : null;
            if (all != null)
            {
                
                int startIdx = Random.Range(0, all.Length);
                for (int k = 0; k < all.Length; k++)
                {
                    var candidate = all[(startIdx + k) % all.Length];
                    if (candidate != null && candidate.Prefab != null) return candidate;
                }
            }
            
            Debug.LogWarning($"[WaveSpawner] No prefab-backed enemy available to substitute for '{(def != null ? def.name : "null")}' in duo mode; skipping this spawn.");
            return null;
        }

        private EnemyDefinition GetWeightedEnemyForWave(int waveNumber)
        {
            var easy = _arena.EasyEnemies;
            var med = _arena.MediumEnemies;
            var hard = _arena.HardEnemies;
            int total = Mathf.Max(1, _arena.WaveCount);
            int waveIdx = Mathf.Clamp(waveNumber, 1, total);
            float t = total > 1 ? (waveIdx - 1) / (float)(total - 1) : 1f; 

            
            float mediumStart = Mathf.Clamp01(_arena.MediumStartPercent);
            float hardStart = Mathf.Clamp01(_arena.HardStartPercent);
            
            if (hardStart < mediumStart)
            {
                hardStart = Mathf.Min(1f, mediumStart + 0.05f);
            }

            
            float wEasy = Mathf.Clamp01(1f - t); 
            float wMed = Mathf.Clamp01(Mathf.InverseLerp(mediumStart, 1f, t));
            float wHard = Mathf.Clamp01(Mathf.InverseLerp(hardStart, 1f, t));

            
            if (easy == null || easy.Length == 0) { wMed += wEasy * 0.5f; wHard += wEasy * 0.5f; wEasy = 0f; }
            if (med == null || med.Length == 0) { wEasy += wMed * 0.5f; wHard += wMed * 0.5f; wMed = 0f; }
            if (hard == null || hard.Length == 0) { wEasy += wHard * 0.5f; wMed += wHard * 0.5f; wHard = 0f; }

            
            float sum = wEasy + wMed + wHard;
            if (sum <= 1e-5f)
            {
                
                var any = _arena.Enemies;
                return any[Random.Range(0, any.Length)];
            }
            wEasy /= sum; wMed /= sum; wHard /= sum;

            float r = Random.value;
            if (r < wEasy && easy != null && easy.Length > 0)
            {
                return easy[Random.Range(0, easy.Length)];
            }
            r -= wEasy;
            if (r < wMed && med != null && med.Length > 0)
            {
                return med[Random.Range(0, med.Length)];
            }
            if (hard != null && hard.Length > 0)
            {
                return hard[Random.Range(0, hard.Length)];
            }
            var all = _arena.Enemies;
            return all[Random.Range(0, all.Length)];
        }
    }
}
