using UnityEngine;
using UnityEngine.UI;

namespace TR.Battle
{
    // Attach this to your Slider-based health bar prefab.
    // It follows an enemy in world space and updates the slider from EnemyBase2D.OnHealthChanged.
    public class EnemyHealthBarUI : MonoBehaviour
    {
        [Header("Binding")]
        [SerializeField] private Slider slider;
        [SerializeField] private Image fillImage; // optional: for color gradient

        [Header("Follow Settings")]
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private bool hideWhenFull = true;

        private EnemyBase2D _enemy;
        private Vector3 _worldOffset;
        private RectTransform _rt;
        private Canvas _canvas;

        // Optional gradient colors
        [Header("Fill Colors by Percent")] 
        [SerializeField] private Color colorFull = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] private Color colorMid = new Color(1f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color colorLow = new Color(1f, 0.2f, 0.2f, 1f);

        public void Bind(EnemyBase2D enemy, Vector3 worldOffset)
        {
            _enemy = enemy;
            _worldOffset = worldOffset;
            if (_enemy != null)
            {
                _enemy.OnHealthChanged -= HandleHealthChanged;
                _enemy.OnHealthChanged += HandleHealthChanged;
                HandleHealthChanged(_enemy.CurrentHealth, _enemy.MaxHealth);
            }
        }

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            if (slider == null) slider = GetComponentInChildren<Slider>(true);
        }

        private void OnDestroy()
        {
            if (_enemy != null)
            {
                _enemy.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void LateUpdate()
        {
            if (_enemy == null || _rt == null) return;
            var worldPos = _enemy.transform.position + _worldOffset;
            if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace)
            {
                _rt.position = worldPos;
                if (billboardToCamera && Camera.main != null)
                {
                    _rt.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                    _rt.right = Camera.main.transform.right; // face camera orthographically
                }
            }
            else
            {
                // Screen-space: convert to screen point
                if (Camera.main != null)
                {
                    Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, worldPos);
                    _rt.position = screenPos;
                }
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (slider == null) return;
            max = Mathf.Max(1f, max);
            float pct = Mathf.Clamp01(current / max);
            slider.normalizedValue = pct;

            if (hideWhenFull)
            {
                bool hide = pct >= 0.999f;
                if (gameObject.activeSelf != !hide)
                {
                    gameObject.SetActive(!hide);
                }
            }

            if (fillImage != null)
            {
                if (pct <= 0.5f)
                {
                    float t = Mathf.InverseLerp(0f, 0.5f, pct);
                    fillImage.color = Color.Lerp(colorLow, colorMid, t);
                }
                else
                {
                    float t = Mathf.InverseLerp(0.5f, 1f, pct);
                    fillImage.color = Color.Lerp(colorMid, colorFull, t);
                }
            }
        }
    }
}
