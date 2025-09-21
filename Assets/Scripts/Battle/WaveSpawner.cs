using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TR.Data;
using TR.VFX;

namespace TR.Battle
{
    // Spawns enemies defined by the current ArenaDefinition.
    public class WaveSpawner : MonoBehaviour
    {
        [Header("Config")] 
        [SerializeField] private Transform[] spawnPoints; // where enemies appear
        [SerializeField] private Path2D path; // path enemies should follow
        [SerializeField] private float spawnInterval = 0.3f; // delay between enemies within a wave
        [Header("VFX")] 
        [Tooltip("Looping portal VFX key to play at each spawn point during a wave.")]
        [SerializeField] private string spawnPortalVfxKey = "";
        private readonly Dictionary<Transform, ParticleSystem> _activePortals = new();

        private ArenaDefinition _arena;
        // Tracking for skip gating
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

        private void CloseAllPortals()
        {
            if (_activePortals.Count == 0) return;
            foreach (var kv in _activePortals)
            {
                var ps = kv.Value;
                if (ps == null) continue;
                // Stop emitting so it fades out naturally; pooled particle will auto-return when not alive
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            _activePortals.Clear();
        }

        public void SpawnWave(int waveNumber)
        {
            if (_arena == null)
            {
                Debug.LogWarning("[WaveSpawner] Arena not configured; call Configure before spawning.");
                return;
            }
            // Validate there is at least one enemy somewhere
            var any = _arena.Enemies;
            if (any == null || any.Length == 0)
            {
                Debug.LogWarning($"[WaveSpawner] Arena '{_arena.DisplayName}' has no enemies assigned.");
                return;
            }
            _arena.GetEnemyCountRangeForWave(waveNumber, out int min, out int max);
            int count = Random.Range(min, max + 1);

            // Boss spawn logic
            bool spawnBoss = _arena.ShouldSpawnBoss(waveNumber, out string warn);
            if (!string.IsNullOrEmpty(warn)) Debug.LogWarning($"[WaveSpawner] {warn}");
            EnemyDefinition boss = spawnBoss ? _arena.BossEnemy : null;

            // Precompute total planned spawns for this wave (include boss if any)
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
            // Ensure previous portals are closed (if any)
            CloseAllPortals();
            // Spawn looping portal VFX at all spawn points at the start of the wave
            if (!string.IsNullOrEmpty(spawnPortalVfxKey))
            {
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
            // If boss present, spawn the boss first at a random spawn point
            if (boss != null)
            {
                var sp = GetSpawnPoint(Random.Range(0, Mathf.Max(1, spawnPoints.Length)));
                SpawnEnemy(boss, sp);
                _spawnedThisWave++;
                // If count is small, ensure at least 0 remaining spawns
                count = Mathf.Max(0, count - 1);
                yield return new WaitForSeconds(Mathf.Max(0f, spawnInterval));
            }

            for (int i = 0; i < count; i++)
            {
                var def = GetWeightedEnemyForWave(waveNumber);
                var sp = GetSpawnPoint(i % spawnPoints.Length);
                SpawnEnemy(def, sp);
                _spawnedThisWave++;
                yield return new WaitForSeconds(Mathf.Max(0f, spawnInterval));
            }
            // All enemies for this wave have been spawned; now close portals (let them fade naturally)
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

        private void SpawnEnemy(EnemyDefinition def, Transform point)
        {
            if (def == null)
            {
                Debug.LogWarning("[WaveSpawner] Null EnemyDefinition; skipping spawn.");
                return;
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
        }

        // === Probabilistic tier mixing per spawn ===
        private EnemyDefinition GetWeightedEnemyForWave(int waveNumber)
        {
            var easy = _arena.EasyEnemies;
            var med = _arena.MediumEnemies;
            var hard = _arena.HardEnemies;
            int total = Mathf.Max(1, _arena.WaveCount);
            int waveIdx = Mathf.Clamp(waveNumber, 1, total);
            float t = total > 1 ? (waveIdx - 1) / (float)(total - 1) : 1f; // 0..1 across waves

            // Use arena-tunable thresholds (0..1), with safety clamps
            float mediumStart = Mathf.Clamp01(_arena.MediumStartPercent);
            float hardStart = Mathf.Clamp01(_arena.HardStartPercent);
            // Ensure hard starts after or equal to medium
            if (hardStart < mediumStart)
            {
                hardStart = Mathf.Min(1f, mediumStart + 0.05f);
            }

            // Compute unnormalized weights
            float wEasy = Mathf.Clamp01(1f - t); // gradually decreases
            float wMed = Mathf.Clamp01(Mathf.InverseLerp(mediumStart, 1f, t));
            float wHard = Mathf.Clamp01(Mathf.InverseLerp(hardStart, 1f, t));

            // If a tier list is empty, re-distribute its weight to others
            if (easy == null || easy.Length == 0) { wMed += wEasy * 0.5f; wHard += wEasy * 0.5f; wEasy = 0f; }
            if (med == null || med.Length == 0) { wEasy += wMed * 0.5f; wHard += wMed * 0.5f; wMed = 0f; }
            if (hard == null || hard.Length == 0) { wEasy += wHard * 0.5f; wMed += wHard * 0.5f; wHard = 0f; }

            // Normalize
            float sum = wEasy + wMed + wHard;
            if (sum <= 1e-5f)
            {
                // fallback: any available
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
