using System.Collections.Generic;
using UnityEngine;

namespace TR.Audio
{
    
    
    
    public class SFXManager : MonoBehaviour
    {
        [Header("Library")]
        [SerializeField] private SFXLibrary library;

        [Header("Pool")]
        [SerializeField] private int initialPoolSize = 16;
        [SerializeField] private int maxPoolSize = 64;

        [Header("Volume")] 
        [Range(0f,1f)] [SerializeField] private float masterVolume = 1f;
        [SerializeField] private bool muted = false;

        private const string PREF_SFX_VOL = "tr_sfx_volume";
        private const string PREF_SFX_MUTE = "tr_sfx_mute";

        private static SFXManager _instance;
        public static SFXManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<SFXManager>(FindObjectsInactive.Include);
                    if (_instance == null)
                    {
                        var go = new GameObject("SFXManager");
                        _instance = go.AddComponent<SFXManager>();
                    }
                }
                return _instance;
            }
        }

        
        public int PlayLoop(string key, float fadeInSeconds = 0.2f)
        {
            if (library == null || string.IsNullOrEmpty(key)) return -1;
            var e = library.Get(key);
            if (e == null) return -1;
            var clip = library.GetRandomClip(e);
            if (clip == null) return -1;
            var src = GetFreeSource();
            src.clip = clip;
            src.pitch = library.GetRandomPitch(e);
            src.loop = true;
            src.time = 0f;
            src.volume = 0f;
            src.Play();
            var inst = new LoopInst { handle = _nextHandle++, src = src, key = key, baseVolume = Mathf.Clamp01(e.volume), co = null };
            _loops[inst.handle] = inst;
            if (inst.co != null) StopCoroutine(inst.co);
            inst.co = StartCoroutine(FadeVolume(inst, inst.baseVolume * (muted ? 0f : masterVolume), Mathf.Max(0f, fadeInSeconds)));
            return inst.handle;
        }

        public void StopLoop(int handle, float fadeOutSeconds = 0.2f)
        {
            if (!_loops.TryGetValue(handle, out var inst) || inst == null || inst.src == null) return;
            if (inst.co != null) StopCoroutine(inst.co);
            inst.co = StartCoroutine(FadeOutAndStop(inst, Mathf.Max(0f, fadeOutSeconds)));
        }

        private readonly List<AudioSource> _pool = new List<AudioSource>(64);
        private readonly Dictionary<string, float> _lastPlay = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _concurrent = new Dictionary<string, int>();
        private int _nextHandle = 1;
        private class LoopInst { public int handle; public AudioSource src; public string key; public float baseVolume; public Coroutine co; }
        private readonly Dictionary<int, LoopInst> _loops = new Dictionary<int, LoopInst>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_SFX_VOL, 1f));
            muted = PlayerPrefs.GetInt(PREF_SFX_MUTE, 0) != 0;
            
            EnsurePool(initialPoolSize);
        }

        private void EnsurePool(int target)
        {
            target = Mathf.Clamp(target, 0, maxPoolSize);
            while (_pool.Count < target)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f; 
                src.volume = 0f;
                _pool.Add(src);
            }
        }

        private AudioSource GetFreeSource()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].isPlaying) return _pool[i];
            }
            if (_pool.Count < maxPoolSize)
            {
                EnsurePool(_pool.Count + 1);
                return _pool[_pool.Count - 1];
            }
            
            return _pool[0];
        }

        public void Play(string key, float volumeScale = 1f)
        {
            if (library == null || string.IsNullOrEmpty(key)) return;
            var e = library.Get(key);
            if (e == null) return;
            
            float now = Time.unscaledTime;
            if (e.cooldown > 0f && _lastPlay.TryGetValue(key, out var last) && (now - last) < e.cooldown)
            {
                return;
            }
            
            if (e.maxConcurrent > 0)
            {
                _concurrent.TryGetValue(key, out var cur);
                if (cur >= e.maxConcurrent) return;
            }
            var clip = library.GetRandomClip(e);
            if (clip == null) return;
            var src = GetFreeSource();
            
            if (e.maxConcurrent > 0)
            {
                _concurrent[key] = (_concurrent.TryGetValue(key, out var c) ? c : 0) + 1;
            }
            src.clip = clip;
            src.pitch = library.GetRandomPitch(e);
            float v = Mathf.Clamp01(e.volume * volumeScale) * (muted ? 0f : masterVolume);
            src.volume = v;
            src.time = 0f;
            src.loop = false;
            src.Play();
            _lastPlay[key] = now;
            
            if (e.maxConcurrent > 0)
            {
                StartCoroutine(ReleaseAfter(src, key));
            }
        }

        private System.Collections.IEnumerator ReleaseAfter(AudioSource src, string key)
        {
            
            while (src != null && src.isPlaying)
            {
                yield return null;
            }
            if (!string.IsNullOrEmpty(key) && _concurrent.TryGetValue(key, out var c))
            {
                c = Mathf.Max(0, c - 1);
                if (c == 0) _concurrent.Remove(key); else _concurrent[key] = c;
            }
        }

        
        public void SetMasterVolume(float vol)
        {
            masterVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat(PREF_SFX_VOL, masterVolume);
            PlayerPrefs.Save();
            
            float factor = muted ? 0f : masterVolume;
            for (int i = 0; i < _pool.Count; i++)
            {
                var src = _pool[i];
                if (src == null) continue;
                
                
                if (factor <= 0f) src.volume = 0f; else src.volume = Mathf.Clamp01(src.volume); 
            }
            
            foreach (var kv in _loops)
            {
                var inst = kv.Value;
                if (inst != null && inst.src != null && inst.src.isPlaying)
                {
                    float target = inst.baseVolume * (muted ? 0f : masterVolume);
                    inst.src.volume = target;
                }
            }
        }

        public void SetMuted(bool m)
        {
            muted = m;
            PlayerPrefs.SetInt(PREF_SFX_MUTE, muted ? 1 : 0);
            PlayerPrefs.Save();
            SetMasterVolume(masterVolume); 
        }

        public float GetMasterVolume() => masterVolume;
        public bool GetMuted() => muted;

        
        public void SetLibrary(SFXLibrary lib) => library = lib;

        private System.Collections.IEnumerator FadeVolume(LoopInst inst, float target, float duration)
        {
            if (inst == null || inst.src == null) yield break;
            float start = inst.src.volume;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
                inst.src.volume = Mathf.Lerp(start, target, t);
                yield return null;
            }
            inst.co = null;
        }

        private System.Collections.IEnumerator FadeOutAndStop(LoopInst inst, float duration)
        {
            if (inst == null || inst.src == null)
            {
                if (inst != null) _loops.Remove(inst.handle);
                yield break;
            }
            float start = inst.src.volume;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
                inst.src.volume = Mathf.Lerp(start, 0f, t);
                yield return null;
            }
            inst.src.Stop();
            inst.src.clip = null;
            _loops.Remove(inst.handle);
            inst.co = null;
        }
    }
}
