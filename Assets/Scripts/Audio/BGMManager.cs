using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TR.Audio
{
    
    
    
    
    public class BGMManager : MonoBehaviour
    {
        [System.Serializable]
        public class SceneTrack
        {
public string sceneName;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            public bool loop = true;
        }

        [Header("Tracks by Scene")]

        public List<SceneTrack> tracks = new List<SceneTrack>();

        [Header("Defaults")]

        public AudioClip defaultClip;
        [Range(0f, 1f)] public float defaultVolume = 0.8f;
        public bool defaultLoop = true;

        [Header("Playback")] 
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Tooltip("Fade duration (seconds) when switching scenes")] public float sceneSwitchFade = 0.5f;

        private static BGMManager _instance;
        private AudioSource _a;
        private AudioSource _b;
        private AudioSource _active; 
        private AudioSource _idle;   
        private Coroutine _fadeCo;
        private string _currentScene;
        private SceneTrack _currentTrack;

        
        private const string PREF_MUSIC_VOL = "tr_music_volume";
        private const string PREF_MUSIC_MUTE = "tr_music_mute";

        public static BGMManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<BGMManager>(FindObjectsInactive.Include);
                    if (_instance == null)
                    {
                        var go = new GameObject("BGMManager");
                        _instance = go.AddComponent<BGMManager>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _a = gameObject.AddComponent<AudioSource>();
            _b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { _a, _b })
            {
                s.playOnAwake = false;
                s.loop = true;
                s.volume = 0f;
            }
            _active = _a; _idle = _b;
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            
            
            try
            {
                float vol = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 1f);
                bool mute = PlayerPrefs.GetInt(PREF_MUSIC_MUTE, 0) != 0;
                SetMasterVolume(mute ? 0f : Mathf.Clamp01(vol));
            }
            catch { /* ignore */ }
            var active = SceneManager.GetActiveScene();
            _currentScene = active.name;
            PlayForScene(_currentScene, sceneSwitchFade);
        }

        private void OnActiveSceneChanged(Scene prev, Scene next)
        {
            _currentScene = next.name;
            PlayForScene(_currentScene, sceneSwitchFade);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            
            if (scene.name == _currentScene)
            {
                PlayForScene(_currentScene, sceneSwitchFade);
            }
        }

        
        public void PlayForScene(string sceneName, float fadeSeconds = 0f)
        {
            var track = FindTrackForScene(sceneName);
            if (track == null && defaultClip == null)
            {
                Stop(fadeSeconds);
                return;
            }
            if (track != null)
            {
                Play(track.clip, track.loop, track.volume, fadeSeconds);
                _currentTrack = track;
            }
            else
            {
                Play(defaultClip, defaultLoop, defaultVolume, fadeSeconds);
                _currentTrack = null;
            }
        }

        public void Play(AudioClip clip, bool loop, float volume, float fadeSeconds = 0f)
        {
            if (clip == null)
            {
                Stop(fadeSeconds);
                return;
            }
            if (_active.clip == clip)
            {
                
                _active.loop = loop;
                _active.volume = volume * masterVolume;
                if (!_active.isPlaying) _active.Play();
                return;
            }
            
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(CrossfadeTo(clip, loop, volume * masterVolume, Mathf.Max(0f, fadeSeconds)));
        }

        public void Stop(float fadeSeconds = 0f)
        {
            if (_active == null) return;
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            if (fadeSeconds > 0f && _active.isPlaying)
            {
                _fadeCo = StartCoroutine(FadeOutAndStop(fadeSeconds));
            }
            else
            {
                _active.Stop();
                _active.clip = null;
            }
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            float target = masterVolume * GetCurrentTrackVolume();
            if (_active != null) _active.volume = target;
            
        }

        public float GetCurrentTrackVolume()
        {
            if (_active == null) return 0f;
            if (_active.clip == null) return 0f;
            if (_currentTrack != null) return Mathf.Clamp01(_currentTrack.volume);
            return Mathf.Clamp01(defaultVolume);
        }

        private SceneTrack FindTrackForScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || tracks == null) return null;
            for (int i = 0; i < tracks.Count; i++)
            {
                var t = tracks[i];
                if (t != null && !string.IsNullOrEmpty(t.sceneName) && t.sceneName == sceneName)
                {
                    return t;
                }
            }
            return null;
        }

        private IEnumerator FadeOutAndStop(float duration)
        {
            float start = _active.volume;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, duration);
                _active.volume = Mathf.Lerp(start, 0f, t);
                yield return null;
            }
            _active.Stop();
            _active.clip = null;
            _fadeCo = null;
        }

        private IEnumerator CrossfadeTo(AudioClip nextClip, bool loop, float targetVolume, float duration)
        {
            targetVolume = Mathf.Clamp01(targetVolume);
            
            _idle.clip = nextClip;
            _idle.loop = loop;
            _idle.volume = 0f;
            _idle.Play();

            float startActive = _active.volume;
            float t = 0f;
            if (duration <= 0f)
            {
                
                _active.Stop();
                _active.clip = null;
                
                var tmp = _active; _active = _idle; _idle = tmp;
                _active.volume = targetVolume;
                _fadeCo = null;
                yield break;
            }
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, duration);
                float k = Mathf.Clamp01(t);
                _active.volume = Mathf.Lerp(startActive, 0f, k);
                _idle.volume = Mathf.Lerp(0f, targetVolume, k);
                yield return null;
            }
            
            _active.Stop();
            _active.clip = null;
            var swap = _active; _active = _idle; _idle = swap;
            
            _idle.volume = 0f;
            _fadeCo = null;
        }
    }
}
