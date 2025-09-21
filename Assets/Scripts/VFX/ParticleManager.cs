using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TR.Battle;

namespace TR.VFX
{
    [DisallowMultipleComponent]
    public class ParticleManager : MonoBehaviour
    {
        [Serializable]
        public class ParticleEntry
        {
            public string key;
            public ParticleSystem prefab;
            [Tooltip("Preload instances to minimize hitches on first spawn")] public int preloadCount = 0;
            [Tooltip("Optional cap; 0 = unlimited")] public int maxPoolSize = 0;
        }

        [Header("Registry")] 
        [Tooltip("Register your particle prefabs here with unique keys.")]
        public List<ParticleEntry> particles = new List<ParticleEntry>();

        private static ParticleManager _instance;
        public static ParticleManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ParticleManager>(FindObjectsInactive.Include);
                    if (_instance == null)
                    {
                        var go = new GameObject("ParticleManager");
                        _instance = go.AddComponent<ParticleManager>();
                    }
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, ParticleEntry> _registry = new();
        private readonly Dictionary<string, Queue<ParticleSystem>> _pools = new();
        private readonly HashSet<int> _autoBound = new(); // instanceIDs of ParticleSystems we have ensured a binder for
        private Coroutine _scanCo;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildRegistry();
            PreloadAll();
        }

        private void OnEnable()
        {
            ParticleQuality.OnChanged += OnQualityChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Initial scan for current scene
            TryScanAndBindAll();
            // Periodic scan for newly spawned PS in scenes
            if (_scanCo == null) _scanCo = StartCoroutine(PeriodicScan());
        }

        private void OnDisable()
        {
            ParticleQuality.OnChanged -= OnQualityChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_scanCo != null) { StopCoroutine(_scanCo); _scanCo = null; }
        }

        private void OnQualityChanged(int q)
        {
            // When turned Off, immediately stop/return any active particles managed by us
            if (q <= 0)
            {
                StopAllActive();
            }
            // Re-scan to ensure new systems get a binder and receive the state
            TryScanAndBindAll();
            // If turned ON, proactively resume scene-bound particle systems (not under our manager)
            if (q > 0)
            {
                ResumeAllSceneParticles();
            }
        }

        private void StopAllActive()
        {
            // Iterate all children under the manager (these are our active pooled particles by default)
            int childCount = transform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                var t = transform.GetChild(i);
                if (t == null) continue;
                var ps = t.GetComponent<ParticleSystem>();
                if (ps == null) continue;
                var pooled = t.GetComponent<PooledParticle>();
                if (pooled != null)
                {
                    pooled.ForceReturn();
                }
                else
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    t.gameObject.SetActive(false);
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryScanAndBindAll();
        }

        private IEnumerator PeriodicScan()
        {
            var wait = new WaitForSeconds(1.0f);
            while (true)
            {
                TryScanAndBindAll();
                yield return wait;
            }
        }

        private void TryScanAndBindAll()
        {
            ParticleSystem[] systems = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i]; if (ps == null) continue;
                int id = ps.GetInstanceID();
                if (_autoBound.Contains(id)) continue;
                // Skip pooled particles managed under this manager (they already obey quality via StopAllActive)
                if (ps.transform.IsChildOf(this.transform)) { _autoBound.Add(id); continue; }
                // If there's already a binder in parents, skip
                if (ps.GetComponentInParent<ParticleQualityBinder>(true) != null) { _autoBound.Add(id); continue; }
                // Add binder to the PS owner object; include children so nested systems also follow
                var owner = ps.gameObject;
                var binder = owner.GetComponent<ParticleQualityBinder>();
                if (binder == null) binder = owner.AddComponent<ParticleQualityBinder>();
                // Ensure it includes children and auto-plays
                var includeChildrenField = owner.GetComponent<ParticleQualityBinder>();
                if (includeChildrenField != null)
                {
                    // nothing else required; defaults are includeChildren=true, autoPlayWhenEnabled=true per our implementation
                }
                _autoBound.Add(id);
                // If quality is currently ON, ensure the system is active and emitting
                if (ParticleQuality.Current > 0)
                {
                    if (!owner.activeInHierarchy) { /* do not activate parents implicitly */ }
                    else
                    {
                        var em = ps.emission; em.enabled = true;
                        if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);
                        // If there's a binder on this owner, ask it to refresh state; otherwise play directly
                        var ownerBinder = owner.GetComponent<ParticleQualityBinder>();
                        if (ownerBinder != null) ownerBinder.Refresh(); else ps.Play(true);
                    }
                }
            }

            // Ensure all towers have a ParticleQualityActivator so idle VFX from card keys can spawn when VFX is toggled on
            var towers = FindObjectsByType<TR.Battle.TowerBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < towers.Length; i++)
            {
                var tower = towers[i]; if (tower == null) continue;
                if (tower.GetComponent<ParticleQualityActivator>() == null)
                {
                    tower.gameObject.AddComponent<ParticleQualityActivator>();
                }
            }
        }

        private void ResumeAllSceneParticles()
        {
            ParticleSystem[] systems = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i]; if (ps == null) continue;
                // Skip pooled particles under manager
                if (ps.transform.IsChildOf(this.transform)) continue;
                // Try to use binder if present on owner
                var binder = ps.GetComponentInParent<ParticleQualityBinder>(true);
                if (binder != null)
                {
                    binder.Refresh();
                }
                else
                {
                    // Fallback: directly enable and play
                    var em = ps.emission; em.enabled = true;
                    if (ps.gameObject.activeInHierarchy)
                    {
                        ps.Play(true);
                    }
                }
            }
        }

        private void BuildRegistry()
        {
            _registry.Clear();
            foreach (var e in particles)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.key) || e.prefab == null) continue;
                _registry[e.key] = e;
                if (!_pools.ContainsKey(e.key)) _pools[e.key] = new Queue<ParticleSystem>();
            }
        }

        private void PreloadAll()
        {
            foreach (var kv in _registry)
            {
                var entry = kv.Value;
                if (entry.preloadCount <= 0) continue;
                for (int i = 0; i < entry.preloadCount; i++)
                {
                    var ps = CreateInstance(entry);
                    ReturnToPool(entry.key, ps);
                }
            }
        }

        private ParticleSystem CreateInstance(ParticleEntry entry)
        {
            var ps = Instantiate(entry.prefab, transform);
            ps.gameObject.SetActive(false);
            // Ensure Stop Action doesn't auto destroy; we pool
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
            // Attach pooled helper
            var pooled = ps.gameObject.GetComponent<PooledParticle>();
            if (pooled == null) pooled = ps.gameObject.AddComponent<PooledParticle>();
            pooled.Bind(this, entry.key, ps);
            return ps;
        }

        private ParticleSystem GetFromPool(string key)
        {
            if (!_registry.ContainsKey(key)) return null;
            var pool = _pools[key];
            if (pool.Count > 0)
            {
                var ps = pool.Dequeue();
                if (ps != null) return ps;
            }
            var entry = _registry[key];
            // Respect max pool size only when returning; we can still instantiate here
            return CreateInstance(entry);
        }

        internal void ReturnToPool(string key, ParticleSystem ps)
        {
            if (ps == null) return;
            ps.gameObject.SetActive(false);
            if (!_pools.ContainsKey(key)) _pools[key] = new Queue<ParticleSystem>();
            var entry = _registry.ContainsKey(key) ? _registry[key] : null;
            if (entry != null && entry.maxPoolSize > 0 && _pools[key].Count >= entry.maxPoolSize)
            {
                Destroy(ps.gameObject);
                return;
            }
            _pools[key].Enqueue(ps);
        }

        // -------- Static API --------
        public static ParticleSystem Spawn(string key, Vector3 position)
            => Spawn(key, position, Quaternion.identity, null, true);

        public static ParticleSystem Spawn(string key, Vector3 position, Quaternion rotation)
            => Spawn(key, position, rotation, null, true);

        public static ParticleSystem Spawn(string key, Vector3 position, Quaternion rotation, Transform parent, bool play = true)
        {
            if (!ParticleQuality.AllowVfx()) return null;
            var mgr = Instance;
            var ps = mgr.GetFromPool(key);
            if (ps == null)
            {
                Debug.LogWarning($"[ParticleManager] Unknown key '{key}'.");
                return null;
            }
            var tr = ps.transform;
            tr.SetParent(parent != null ? parent : mgr.transform, worldPositionStays: false);
            tr.position = position;
            tr.rotation = rotation;
            ps.gameObject.SetActive(true);
            if (play) ps.Play(true);
            return ps;
        }

        public static void SpawnOneShot(string key, Vector3 position)
        {
            if (!ParticleQuality.AllowVfx()) return;
            var ps = Spawn(key, position, Quaternion.identity, null, true);
            // PooledParticle will auto-return when finished
            if (ps == null)
            {
                Debug.LogWarning($"[ParticleManager] Failed to spawn key '{key}'.");
            }
        }
    }
}
