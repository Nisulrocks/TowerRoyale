using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TR.Audio
{
    // Plays one shared click sound for every uGUI Button in the game.
    //
    // uGUI gives no global "a button was clicked" event: Button consumes the pointer-click event
    // itself, so a handler on a parent canvas never sees it. Instead this wires Button.onClick
    // directly and rescans periodically, which also catches buttons spawned at runtime (list rows,
    // popups, shop items).
    public class UIClickSfx : MonoBehaviour
    {
        public static UIClickSfx Instance { get; private set; }

        [Header("SFX")]
        [Tooltip("Key from the SFX Library (Resources/SFX/SFXLibrary) played on any UI button click.")]
        [SerializeField] private string clickSfxKey = "ui_click";

        [Tooltip("Buttons created after the last scan are picked up within this many seconds.")]
        [SerializeField] private float rescanInterval = 0.75f;

        [Tooltip("Turn off to silence UI clicks without removing the component.")]
        [SerializeField] private bool enableClickSfx = true;

        // Instance ids of buttons already wired, so a rescan never stacks duplicate listeners.
        private readonly HashSet<int> _wired = new HashSet<int>();
        private float _nextScan;

        public string ClickSfxKey
        {
            get => clickSfxKey;
            set => clickSfxKey = value;
        }

        public static void Initialize(string sfxKey = null)
        {
            if (Instance == null)
            {
                var go = new GameObject("UIClickSfx");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<UIClickSfx>();
            }
            if (!string.IsNullOrEmpty(sfxKey)) Instance.clickSfxKey = sfxKey;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Ids are not reused across scenes in practice, but clearing keeps the set from growing
            // and forces a fresh wire-up of the new scene's buttons.
            _wired.Clear();
            _nextScan = 0f;
        }

        private void Update()
        {
            if (!enableClickSfx) return;
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + Mathf.Max(0.1f, rescanInterval);
            WireButtons();
        }

        private void WireButtons()
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null) continue;

                int id = button.GetInstanceID();
                if (!_wired.Add(id)) continue;

                // Opt out per button by adding a UIClickSfxIgnore component.
                if (button.GetComponent<UIClickSfxIgnore>() != null) continue;

                button.onClick.AddListener(PlayClick);
            }
        }

        public void PlayClick()
        {
            if (!enableClickSfx || string.IsNullOrEmpty(clickSfxKey)) return;
            SFXManager.Instance?.Play(clickSfxKey);
        }
    }

    // Add to a Button that should stay silent (or play its own sound instead).
    public class UIClickSfxIgnore : MonoBehaviour { }
}
