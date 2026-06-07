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
public int preloadCount = 0;
public int maxPoolSize = 0;
        }

        [Header("Registry")] 

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
        private readonly HashSet<int> _autoBound = new(); 
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
            
            TryScanAndBindAll();
            
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
            
            if (q <= 0)
            {
                StopAllActive();
            }
            
            TryScanAndBindAll();
            
            if (q > 0)
            {
                ResumeAllSceneParticles();
            }
        }

        private void StopAllActive()
        {
            
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
                
                if (ps.transform.IsChildOf(this.transform)) { _autoBound.Add(id); continue; }
                
                if (ps.GetComponentInParent<ParticleQualityBinder>(true) != null) { _autoBound.Add(id); continue; }
                
                var owner = ps.gameObject;
                var binder = owner.GetComponent<ParticleQualityBinder>();
                if (binder == null) binder = owner.AddComponent<ParticleQualityBinder>();
                
                var includeChildrenField = owner.GetComponent<ParticleQualityBinder>();
                if (includeChildrenField != null)
                {
                    
                }
                _autoBound.Add(id);
                
                if (ParticleQuality.Current > 0)
                {
                    if (!owner.activeInHierarchy) { /* do not activate parents implicitly */ }
                    else
                    {
                        var em = ps.emission; em.enabled = true;
                        if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);
                        
                        var ownerBinder = owner.GetComponent<ParticleQualityBinder>();
                        if (ownerBinder != null) ownerBinder.Refresh(); else ps.Play(true);
                    }
                }
            }

            
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
                
                if (ps.transform.IsChildOf(this.transform)) continue;
                
                var binder = ps.GetComponentInParent<ParticleQualityBinder>(true);
                if (binder != null)
                {
                    binder.Refresh();
                }
                else
                {
                    
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
            
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
            
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
            
            if (ps == null)
            {
                Debug.LogWarning($"[ParticleManager] Failed to spawn key '{key}'.");
            }
        }
    }
}
