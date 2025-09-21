using System.Collections.Generic;
using UnityEngine;
using TR.Battle;

namespace TR.UI
{
    // Drop this into the Lobby or Battle scene. Assign Canvas (Screen Space Overlay/Camera) and Number Prefab.
    public class DamageNumbers : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private Canvas canvas; // target canvas for numbers
        [SerializeField] private FloatingDamageNumber numberPrefab; // prefab with TextMeshProUGUI child
        [SerializeField] private FloatingBurstText critBurstPrefab; // simple burst text for crits
        [SerializeField] private FloatingDamageStyle defaultStyle;
        [SerializeField] private bool enabledByDefault = true;

        private const string PREF_SHOW_DAMAGE_NUMBERS = "tr_show_damage_numbers";

        private static DamageNumbers _instance;
        private static bool _enabled = true;

        // One active number per target
        private readonly Dictionary<Transform, FloatingDamageNumber> _active = new();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            // Load enabled state
            _enabled = PlayerPrefs.GetInt(PREF_SHOW_DAMAGE_NUMBERS, enabledByDefault ? 1 : 0) != 0;
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }
        }

        // Expose style and clamp preference
        public static FloatingDamageStyle Style => _instance != null ? _instance.defaultStyle : null;
        public static bool ClampDisplayed => Style == null ? true : Style.clampDisplayedDamageToRemainingHealth;

        public static void Show(Transform target, int amount, DamageType type)
        {
            if (_instance == null)
            {
                Debug.LogWarning("[DamageNumbers] No instance in scene. Add DamageNumbers to a GameObject and assign Canvas + Prefab.");
                return;
            }
            if (!_enabled) return;
            _instance.InternalShowFloat(target, amount);
        }

        public static void ShowFloat(Transform target, float amount, DamageType type)
        {
            if (_instance == null)
            {
                Debug.LogWarning("[DamageNumbers] No instance in scene. Add DamageNumbers to a GameObject and assign Canvas + Prefab.");
                return;
            }
            if (!_enabled) return;
            _instance.InternalShowFloat(target, amount);
        }

        private void InternalShowFloat(Transform target, float amount)
        {
            if (target == null || amount <= 0f) return;
            var key = target;

            if (_active.TryGetValue(key, out var dn) && dn != null)
            {
                dn.AddValueFloat(amount);
                return;
            }

            // Create new instance
            if (numberPrefab == null || canvas == null)
            {
                Debug.LogWarning("[DamageNumbers] Missing prefab or canvas.");
                return;
            }
            var inst = Instantiate(numberPrefab, canvas.transform);
            var style = defaultStyle;
            inst.Init(target, Mathf.CeilToInt(amount), style, canvas);
            // immediately replace text with correct float display
            inst.AddValueFloat(0f);
            _active[key] = inst;

            // Clean up reference when destroyed
            StartCoroutine(CleanupWhenDestroyed(inst, key));
        }

        private System.Collections.IEnumerator CleanupWhenDestroyed(FloatingDamageNumber inst, Transform key)
        {
            // Wait until destroyed
            while (inst != null)
            {
                yield return null;
            }
            _active.Remove(key);
        }

        // Settings API
        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            PlayerPrefs.SetInt(PREF_SHOW_DAMAGE_NUMBERS, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
        public static bool GetEnabled() => _enabled;

        // One-shot crit burst that does not merge
        public static void ShowCrit(Transform target, string text = "CRIT!")
        {
            if (_instance == null)
            {
                Debug.LogWarning("[DamageNumbers] No instance in scene. Add DamageNumbers to a GameObject and assign Canvas + Prefab.");
                return;
            }
            if (!_enabled) return;
            if (_instance.critBurstPrefab == null || _instance.canvas == null) return;
            var inst = Object.Instantiate(_instance.critBurstPrefab, _instance.canvas.transform);
            inst.Init(target, text, _instance.canvas);
        }
    }
}
