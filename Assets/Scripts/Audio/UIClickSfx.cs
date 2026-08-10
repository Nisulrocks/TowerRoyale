using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TR.Audio
{
    public class UIClickSfx : MonoBehaviour
    {
        public static UIClickSfx Instance { get; private set; }

        [Header("SFX")]
        [SerializeField] private string clickSfxKey = "ui_click";

        [SerializeField] private float rescanInterval = 0.75f;

        [SerializeField] private bool enableClickSfx = true;

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

    public class UIClickSfxIgnore : MonoBehaviour { }
}
