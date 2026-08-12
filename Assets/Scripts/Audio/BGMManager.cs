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
         public float sceneSwitchFade = 0.5f;

        private static BGMManager _instance;
        private AudioSource _a;
        private AudioSource _b;
        private AudioSource _active; 
        private AudioSource _idle;   
        private Coroutine _fadeCo;
        private string _currentScene;
        private SceneTrack _currentTrack;



        private float _baseVolume;
        private float _duck = 1f;
        private Coroutine _duckCo;

        
        private const string PREF_MUSIC_VOL = "tr_music_volume";
        private const string PREF_MUSIC_MUTE = "tr_music_mute";



        public static BGMManager Active => _instance;

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
            catch {  }
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
                _baseVolume = Mathf.Clamp01(volume * masterVolume);
                ApplyActiveVolume();
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
            _baseVolume = masterVolume * GetCurrentTrackVolume();
            ApplyActiveVolume();
        }





        public void DuckFor(float seconds, float level = 0.25f, float downSeconds = 0.25f, float upSeconds = 0.8f)
        {
            if (_duckCo != null) StopCoroutine(_duckCo);
            level = Mathf.Clamp01(level);
            downSeconds = Mathf.Max(0f, downSeconds);
            float hold = Mathf.Max(0f, seconds - downSeconds);
            _duckCo = StartCoroutine(DuckRoutine(level, hold, downSeconds, Mathf.Max(0f, upSeconds)));
        }



        public void ClearDuck(float upSeconds = 0.4f)
        {
            if (_duckCo != null) StopCoroutine(_duckCo);
            _duckCo = StartCoroutine(DuckRoutine(_duck, 0f, 0f, Mathf.Max(0f, upSeconds)));
        }




        private void ApplyActiveVolume()
        {
            if (_fadeCo != null) return;
            if (_active != null) _active.volume = Mathf.Clamp01(_baseVolume * _duck);
        }

        private IEnumerator DuckRoutine(float level, float hold, float down, float up)
        {
            float from = _duck;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, down);
                _duck = Mathf.Lerp(from, level, Mathf.Clamp01(t));
                ApplyActiveVolume();
                yield return null;
            }
            _duck = level;
            ApplyActiveVolume();

            float h = 0f;
            while (h < hold)
            {
                h += Time.unscaledDeltaTime;
                ApplyActiveVolume();
                yield return null;
            }

            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, up);
                _duck = Mathf.Lerp(level, 1f, Mathf.Clamp01(t));
                ApplyActiveVolume();
                yield return null;
            }
            _duck = 1f;
            ApplyActiveVolume();
            _duckCo = null;
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
                _baseVolume = targetVolume;
                _fadeCo = null;
                ApplyActiveVolume();
                yield break;
            }
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, duration);
                float k = Mathf.Clamp01(t);
                _active.volume = Mathf.Lerp(startActive, 0f, k);



                _idle.volume = Mathf.Lerp(0f, targetVolume * _duck, k);
                yield return null;
            }

            _active.Stop();
            _active.clip = null;
            var swap = _active; _active = _idle; _idle = swap;

            _idle.volume = 0f;
            _baseVolume = targetVolume;
            _fadeCo = null;
            ApplyActiveVolume();
        }
    }
}
