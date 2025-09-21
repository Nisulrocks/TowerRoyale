using UnityEngine;
using UnityEngine.EventSystems;
using TR.Audio;

namespace TR.UI
{
    // Simple in-battle pause controller.
    // Mirrors Settings functionality, but additionally pauses gameplay via Time.timeScale and AudioListener.pause.
    // Usage:
    // - Drop this on your Pause panel root.
    // - Wire up Resume button to Resume(), Pause button or hotkey calls TogglePause().
    // - Optionally assign a Settings panel or a PanelSwitcher handler to open settings from the pause menu.
    public class BattlePauseController : MonoBehaviour
    {
        [Header("Panel References")] 
        [SerializeField] private GameObject rootPanel;          // The pause menu window

        [Header("Behavior")] 
        [SerializeField] private bool startHidden = true;
        [SerializeField] private KeyCode toggleHotkey = KeyCode.Escape; // fallback if prefs missing
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
            // Read configured hotkey from PlayerPrefs each frame to reflect changes made in lobby settings
            var key = GetConfiguredPauseKey();
            if (Input.GetKeyDown(key)) TogglePause();
        }

        private static KeyCode GetConfiguredPauseKey()
        {
            // Mirror SettingsPanelController mapping: PREF_PAUSE_HOTKEY 0:Esc,1:P,2:Tab,3:Space
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
            // Fallback to instance-configured hotkey if no pref set
            if (_instance != null) return _instance.toggleHotkey;
            return KeyCode.Escape;
        }

        // Public API
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
            AudioListener.pause = true; // pause all audio without changing mixer volumes
            if (rootPanel != null) rootPanel.SetActive(true);
            // Optional: tell systems about pause (e.g., disable input gating)
            // InputLocks.SetPlacementDragging(false); // if desired
        }

        public void Resume()
        {
            if (!_paused) return;
            _paused = false;
            Time.timeScale = Mathf.Clamp(_prePauseTimeScale, 0.01f, 100f);
            AudioListener.pause = false;
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        // Hook this to a Resume button
        public void OnClickResume() => Resume();

        // Convenience global toggle (e.g., from anywhere)
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
