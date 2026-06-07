using System.Collections.Generic;
using UnityEngine;
using TR.Battle;

namespace TR.UI
{
    
    public class DamageNumbers : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private Canvas canvas; 
        [SerializeField] private FloatingDamageNumber numberPrefab; 
        [SerializeField] private FloatingBurstText critBurstPrefab; 
        [SerializeField] private FloatingDamageStyle defaultStyle;
        [SerializeField] private bool enabledByDefault = true;

        private const string PREF_SHOW_DAMAGE_NUMBERS = "tr_show_damage_numbers";

        private static DamageNumbers _instance;
        private static bool _enabled = true;

        
        private readonly Dictionary<Transform, FloatingDamageNumber> _active = new();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            _enabled = PlayerPrefs.GetInt(PREF_SHOW_DAMAGE_NUMBERS, enabledByDefault ? 1 : 0) != 0;
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }
        }

        
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

            
            if (numberPrefab == null || canvas == null)
            {
                Debug.LogWarning("[DamageNumbers] Missing prefab or canvas.");
                return;
            }
            var inst = Instantiate(numberPrefab, canvas.transform);
            var style = defaultStyle;
            inst.Init(target, Mathf.CeilToInt(amount), style, canvas);
            
            inst.AddValueFloat(0f);
            _active[key] = inst;

            
            StartCoroutine(CleanupWhenDestroyed(inst, key));
        }

        private System.Collections.IEnumerator CleanupWhenDestroyed(FloatingDamageNumber inst, Transform key)
        {
            
            while (inst != null)
            {
                yield return null;
            }
            _active.Remove(key);
        }

        
        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            PlayerPrefs.SetInt(PREF_SHOW_DAMAGE_NUMBERS, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
        public static bool GetEnabled() => _enabled;

        
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
