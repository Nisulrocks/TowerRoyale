using UnityEngine;
using UnityEngine.EventSystems;
using TR.Audio;

namespace TR.UI
{
    
    
    
    
    
    
    public class BattlePauseController : MonoBehaviour
    {
        [Header("Panel References")] 
        [SerializeField] private GameObject rootPanel;          

        [Header("Behavior")] 
        [SerializeField] private bool startHidden = true;
        [SerializeField] private KeyCode toggleHotkey = KeyCode.Escape; 
        [SerializeField] private bool enableHotkey = true;

        private static BattlePauseController _instance;
        private float _prePauseTimeScale = 1f;
        private bool _paused;

        public static bool IsPaused => _instance != null && _instance._paused;

        private void Awake()
        {
            _instance = this;
            if (rootPanel == null) rootPanel = gameObject;
            if (startHidden && rootPanel != null) rootPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (!enableHotkey) return;
            
            var key = GetConfiguredPauseKey();
            if (Input.GetKeyDown(key)) TogglePause();
        }

        private static KeyCode GetConfiguredPauseKey()
        {
            
            if (PlayerPrefs.HasKey("tr_pause_hotkey"))
            {
                int hk = PlayerPrefs.GetInt("tr_pause_hotkey", 0);
                switch (Mathf.Clamp(hk, 0, 3))
                {
                    case 1: return KeyCode.P;
                    case 2: return KeyCode.Tab;
                    case 3: return KeyCode.Space;
                    default: return KeyCode.Escape;
                }
            }
            
            if (_instance != null) return _instance.toggleHotkey;
            return KeyCode.Escape;
        }

        
        public void TogglePause()
        {
            if (_paused) Resume(); else Pause();
        }

        public void Pause()
        {
            if (_paused) return;
            _paused = true;
            _prePauseTimeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true; 
            if (rootPanel != null) rootPanel.SetActive(true);
            
            
        }

        public void Resume()
        {
            if (!_paused) return;
            _paused = false;
            Time.timeScale = Mathf.Clamp(_prePauseTimeScale, 0.01f, 100f);
            AudioListener.pause = false;
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        
        public void OnClickResume() => Resume();

        
        public static void Toggle()
        {
            if (_instance == null) return;
            _instance.TogglePause();
        }

        public static void EnsureResumed()
        {
            if (_instance == null) return;
            if (_instance._paused) _instance.Resume();
        }
    }
}
